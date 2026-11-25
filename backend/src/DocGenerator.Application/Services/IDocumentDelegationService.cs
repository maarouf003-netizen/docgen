using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الإنابات التنفيذية: تسطير إنابة من محامي الملف المنيب، واعتمادها من رئيس القسم
/// (اختيار المحامي المختص وإنشاء الملف المناب تلقائيًا)، وتسجيلها أصولًا من محامي الفرع
/// المناب، ثم إتمامها ببيع الأموال موضوع الإنابة بالمزاد العلني وإعادة الملف للدائرة المنيبة.
/// </summary>
public interface IDocumentDelegationService
{
    /// <summary>
    /// مزامنة المحامي المختص للإنابات المرتبطة بملفات منابة نُقلت ملكيتها (داخل معاملة
    /// النقل): الحقل المخزّن على الإنابة يصبح المالك الجديد بدل أول محامٍ أُسندت إليه.
    /// لا يمسّ الملف المنيب ولا التنبيهات (تلك مسؤولية مسار النقل/التنبيهات).
    /// </summary>
    Task SyncTargetsTransferredAsync(IReadOnlyCollection<int> targetDocumentIds, int newLawyerId,
        string? actorName, int? actorUserId = null, CancellationToken ct = default);

    /// <summary>
    /// حارس تعديل الملف المناب («الملف المناب مرآةً للمنيب»): يرفض الفروق القيمية في الحقول
    /// المقفولة، ويرصد الإضافات المحلية المسموحة (وريث جديد/ممثل جديد) تقريرًا للتنبيه العكسي.
    /// حارس قرائي قبل الكتابة — خارج المعاملة كسائر حراس التعديل.
    /// </summary>
    TargetEditReport ValidateTargetEdit(Document doc, DocumentUpsertRequest request);

    /// <summary>
    /// قفل «الحجز والأصل بعد التسطير» على الملف المنيب: تاريخ القاء الحجز على أصلٍ تحجبه
    /// إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه مرفوض.
    /// حارس قرائي قبل الكتابة — خارج المعاملة كسائر حراس التعديل.
    /// </summary>
    Task ValidateSeizureLockAsync(Document doc, DocumentUpsertRequest request, CancellationToken ct);

    /// <summary>
    /// مزامنة لقطات أصول الإنابات غير المنفذة للملف المنيب بعد تعديل أصوله —
    /// داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    Task SyncSnapshotsAsync(Document doc, CancellationToken ct);

    /// <summary>
    /// نسخ تغييرات المنيب إلى مناباته المعلقة (داخل معاملة المستدعي حصرًا) وإعادة اشتقاق
    /// الحقول المحسوبة فيها — تعيد إشعارات تُطلق بعد نجاح المعاملة (نقطة خروج واحدة).
    /// </summary>
    Task<List<MirrorNotification>> MirrorSourceEditsAsync(Document doc, CancellationToken ct);

    /// <summary>
    /// إطلاق إشعارات المرآة المجمعة — بعد تثبيت معاملة الحفظ حصرًا
    /// (كل فشل معزول ولا يُفشل الحفظ).
    /// </summary>
    Task FireMirrorNotificationsAsync(IReadOnlyList<MirrorNotification> notifications, CancellationToken ct);

    /// <summary>
    /// تثبيت الإضافات المحلية (وريث/ممثل) في سجل الإنابة نفسه — عدّاد تراكمي + ملخص آخر دفعة +
    /// وقت UTC — ثم إطلاق تنبيه المنيب. تُستدعى **داخل معاملة تعديل الملف المناب**: تثبيت السجل
    /// وحجز رسالة الـOutbox يسقطان مع المعاملة عند أي فشل (لا إضافة بلا إشعار ولا العكس).
    /// الاسم صريح في عقديه: الكتابة الدائمة (داخل المعاملة) ثم الإطلاق.
    ///
    /// السجل يحرّك توكن صف الإنابة (`Version`) لأنه كتابة عليه: بلا ذلك لا يكشفه EF عند
    /// تزامن كاتبَين (شرط <c>WHERE Version = old</c> يوافق عندهما ⇒ آخر كاتب يفوز ويُفقد
    /// تحديث بصمت). الأثر المقصود: نافذة إنابة مفتوحة على نسخة قديمة تحصل على <c>409</c>
    /// ودّي بعد تسجيل إضافة محلية — سلوك تزامن صحيح لأن الصف تغيّر فعلًا.
    /// </summary>
    Task RecordLocalAdditionsAndFireReportAsync(
        Document target, TargetEditReport report, CancellationToken ct);

    /// <summary>
    /// تنبيه تغيّر حالة المنيب إلى مناباته المعلقة (الصيغ T2-T5 بصياغات المستخدم المقفلة؛
    /// المشطوب والجزئيا لا يصلان هنا) — بعد تثبيت معاملة الحالة حصرًا.
    /// </summary>
    Task FireStatusChangeAlertsAsync(Document doc, DelegationStatusAlertKind kind, CancellationToken ct);
}
