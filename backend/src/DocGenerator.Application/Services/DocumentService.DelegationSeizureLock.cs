using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// «الحجز والأصل بعد التسطير» على الملف المنيب (قرار المستخدم): تاريخ القاء الحجز على
/// أصلٍ تحجبه إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه مرفوض ما دامت
/// الإنابة سارية. قفل خلفي مكمل لقفل الواجهة — يُطبَّق على الكتابة الجديدة فقط
/// (لا أثر رجعي على البيانات القديمة).
/// </summary>
public sealed partial class DocumentService
{
    /// <summary>
    /// حارس «تاريخ القاء الحجز لا يُحذف بعد التسطير» و«المال المسطر به إنابة لا يُحذف»:
    /// أي أصلٍ مخزَّن مربوطٍ بإنابة حاجبة (مطابقة اللقطات واحد-لواحد نفسها المستعملة في
    /// حارس التسطير — عبر الحمل الداخلي المشترك) يُمنع إرساله في الطلب فارغ الحجز
    /// متى كان له حجز مخزَّن، ويُمنع غياب صفه من الطلب مطلقًا (الصف بلا معرف مطابق
    /// يُعد حذفًا مرفوضًا). تعديل قيمة الحجز بموعدٍ آخر مسموح (يُحلله BuildAsset لاحقًا).
    /// تُجلب الإنابات مع مناباتها بقراءة مستقلة لأن قراءة التعديل لا تشمل المناب،
    /// وبدونه تُعامل المعلقة بلا مناب حاجبةً دائمًا فيُتسع القفل خطأً على المحرر منها.
    /// يُستدعى في UpdateAsync قبل ApplyRequest — خارج المعاملة كسائر حراس التعديل.
    /// مخاطرة متبقية موثقة ومقبولة: لا تسلسل بين تعديل المستند وتسطير إنابة متزامنين،
    /// فالتداخل اللحظي النادر يُترك لسجل التدقيق بدل حصرٍ مكلف (كسباق الحجب المحسوم بقيد فريد).
    /// </summary>
    private async Task ValidateDelegationSeizureLockAsync(
        Document doc, DocumentUpsertRequest request, CancellationToken ct)
    {
        if (doc.Delegations.Count == 0 || doc.Assets.Count == 0)
            return;

        var delegations = await _delegations.ListBySourceAsync(doc.Id, ct);
        var referencedIds = DocumentDelegationService.ComputeBlockedIds(doc.Assets, delegations, excludeDelegationId: null);
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
