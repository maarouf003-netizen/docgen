using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService
{
    /// <summary>
    /// مزامنة لقطات أصول الإنابات غير المنفذة للملف المنيب بعد تعديل أصوله: أصول الملف
    /// تُعاد بناؤها بمعرفات جديدة عند كل تعديل، فتُطابَق اللقطات مع الأصول الحالية بالنوع
    /// ثم الوصف — الأصل المطابق تمامًا (نوع + وصف) يبقى بلا تغيير، والأصل الذي تغيّر وصفه
    /// من النوع نفسه تُحدَّث لقطته وتُعلَّم بأن بياناته عُدّلت بعد التسطير (يظهر تنبيه في
    /// بطاقة «تشعبات الملف») — إلا إذا تغيّر عدد النوع (C2: تجميد — اللقطة اليتيمة تبقى
    /// كما سُطّرت سجلًا تاريخيًا مع تحذيرها في الواجهة). الإنابات المنفذة سجل نهائي
    /// بالبدل فلا تُمسّ لقطاتها.
    /// داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة) — تُستدعى بعد حفظ المنيب.
    /// </summary>
    public async Task SyncSnapshotsAsync(Document doc, CancellationToken token)
    {
        var delegations = await _delegations.ListBySourceAsync(doc.Id, token);
        var pending = delegations.Where(DelegationActivityPolicy.IsLifecycleActive).ToList();
        if (pending.Count == 0)
            return;

        // C2: تجميد النقل التلقائي عند تغيّر عدد النوع — لقطة يتيمة (حُذف أصلها) لا تُعاد
        // تسميتها إلى أصل آخر من النوع نفسه عند اختلاف العدد (إضافة/حذف)، بل تبقى سجلًا
        // تاريخيًا كما سُطّرت. يُسمح بالنقل فقط عند تساوي العدد (تعديل وصف بحت — آمن).
        var snapshots = pending.SelectMany(d => d.Assets).OrderBy(s => s.Id).ToList();
        var currentByKind = doc.Assets
            .GroupBy(a => a.AssetKind)
            .ToDictionary(g => g.Key, g => g.Count());
        var frozenKinds = snapshots
            .GroupBy(s => s.AssetKind)
            .Where(g => !currentByKind.TryGetValue(g.Key, out var currentCount) || currentCount != g.Count())
            .Select(g => g.Key)
            .ToHashSet();

        // C1: الفرز بالمفتاح قبل الجشع في المرحلتين — حتمية أي لقطة تُطابَق/تُعاد تسميتها.
        var remaining = doc.Assets.OrderBy(a => a.Id).ToList();
        var changed = false;
        foreach (var snapshot in snapshots)
        {
            // تطابق تام (نوع + وصف): لم تتغير بيانات الأصل — تُستهلك وتُترك بلا تغيير.
            var exact = remaining.FindIndex(a => a.AssetKind == snapshot.AssetKind && AssetDisplay.Label(a) == snapshot.AssetLabel);
            if (exact >= 0)
            {
                remaining.RemoveAt(exact);
                continue;
            }
            // C2: النوع المجمّد لا يُنقَل تلقائيًا — اللقطة اليتيمة تبقى كما سُطّرت.
            if (frozenKinds.Contains(snapshot.AssetKind))
                continue;
            // تعديل وصف أصل من النوع نفسه بعد التسطير: تُحدَّث اللقطة ويُعلَّم التعديل.
            var sameKind = remaining.FindIndex(a => a.AssetKind == snapshot.AssetKind);
            if (sameKind >= 0)
            {
                var assetLabel = AssetDisplay.Label(remaining[sameKind]);
                remaining.RemoveAt(sameKind);
                if (snapshot.AssetLabel != assetLabel)
                {
                    snapshot.AssetLabel = assetLabel;
                    snapshot.SnapshotAdjusted = true;
                    changed = true;
                }
            }
            // بلا أصل من النوع نفسه في الملف الحالي: تبقى اللقطة كما سُطّرت (سجل التسطير).
        }

        if (changed)
            await _uow.SaveChangesAsync(token);
    }

    /// <summary>
    /// «الحجز والأصل بعد التسطير» على الملف المنيب (قرار المستخدم): تاريخ القاء الحجز على
    /// أصلٍ تحجبه إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه مرفوض ما دامت
    /// الإنابة سارية. قفل خلفي مكمل لقفل الواجهة — يُطبَّق على الكتابة الجديدة فقط
    /// (لا أثر رجعي على البيانات القديمة).
    /// حارس «تاريخ القاء الحجز لا يُحذف بعد التسطير» و«المال المسطر به إنابة لا يُحذف»:
    /// أي أصلٍ مخزَّن مربوطٍ بإنابة حاجبة (مطابقة اللقطات واحد-لواحد نفسها المستعملة في
    /// حارس التسطير — عبر الحمل الداخلي المشترك) يُمنع إرساله في الطلب فارغ الحجز
    /// متى كان له حجز مخزَّن، ويُمنع غياب صفه من الطلب مطلقًا (الصف بلا معرف مطابق
    /// يُعد حذفًا مرفوضًا). تعديل قيمة الحجز بموعدٍ آخر مسموح (يُحلله البناء لاحقًا).
    /// تُجلب الإنابات مع مناباتها بقراءة مستقلة لأن قراءة التعديل لا تشمل المناب،
    /// وبدونه تُعامل المعلقة بلا مناب حاجبةً دائمًا فيُتسع القفل خطأً على المحرر منها.
    /// يُستدعى قبل الكتابة — خارج المعاملة كسائر حراس التعديل.
    /// مخاطرة متبقية موثقة ومقبولة: لا تسلسل بين تعديل المستند وتسطير إنابة متزامنين،
    /// فالتداخل اللحظي النادر يُترك لسجل التدقيق بدل حصرٍ مكلف (كسباق الحجب المحسوم بقيد فريد).
    /// </summary>
    public async Task ValidateSeizureLockAsync(
        Document doc, DocumentUpsertRequest request, CancellationToken ct)
    {
        if (doc.Delegations.Count == 0 || doc.Assets.Count == 0)
            return;

        var delegations = await _delegations.ListBySourceAsync(doc.Id, ct);
        var referencedIds = ComputeBlockedIds(doc.Assets, delegations, excludeDelegationId: null);
        if (referencedIds.Count == 0)
            return;

        // مطابقة القديم بالطلب عبر AssetDto.Id — تحفظه الواجهة عند التحميل.
        var byId = request.Assets
            .Where(re => re.Id is not null)
            .GroupBy(re => re.Id!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var deletedAssets = new List<string>();
        var clearedDates = new List<string>();
        foreach (var stored in doc.Assets.Where(a => referencedIds.Contains(a.Id)))
        {
            if (!byId.TryGetValue(stored.Id, out var requested))
            {
                deletedAssets.Add(AssetDisplay.Label(stored));
                continue;
            }
            if (stored.SeizureDate is not null && string.IsNullOrWhiteSpace(requested.SeizureDate))
                clearedDates.Add(AssetDisplay.Label(stored));
        }

        if (deletedAssets.Count > 0)
            throw new ArgumentException($"لا يمكن حذف هذا المال لأن هناك انابة سارية: {string.Join("، ", deletedAssets)}");
        if (clearedDates.Count > 0)
            throw new ArgumentException($"لا يمكن حذف تاريخ القاء الحجز على مال مسطر به انابة: {string.Join("، ", clearedDates)}");
    }
}
