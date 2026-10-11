using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// النصوص والحراس الخالصة المشتركة لخدمات الملفات (المرحلة 1 — البيت المشترك):
/// بلا حالة ولا تبعيات. تُستهلك من النواة والخدمات المستخرجة (إجراءات/بحث/حالات/إنابة)
/// بدل تكرار المنطق — أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة.
/// </summary>
public static class DocumentText
{
    /// <summary>
    /// الاسم الثلاثي للمنفذ عليه من بيانات المستند، يُلحق بكل أحداث التدقيق
    /// ليُعرف أي ملف وقع عليه الإجراء دون فتح صفحة المستند.
    /// </summary>
    public static string ActorFullName(Document doc) => string.Join(' ',
        new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(v => !string.IsNullOrWhiteSpace(v)));

    public static string AuditWithActor(string action, Document doc) =>
        string.IsNullOrWhiteSpace(ActorFullName(doc))
            ? action
            : $"{action} — المنفذ عليه: {ActorFullName(doc)}";

    public static string TypeLabel(string type) => type == "note" ? "ملاحظة" : "إجراء";

    /// <summary>
    /// اسم العرض للملف في صفحة التدوير. لعائلتي وضع «منفذ عليه» (Executed + Deposit) لا يوجد
    /// مقترض، فأُعرض اسم طالب العرض (الجهة العامة أو الشخص الطبيعي) كما تُعرض في ملفات الإكسل
    /// والطباعة، ولو تعذّر توافره فلا أسمًا. ولنظام «طالبة تنفيذ» تُستخدم أسماء المقترض المعتادة.
    /// </summary>
    public static string RotationDisplayName(Document d)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(d.GeneralEntitySide))
            return FormatName(d.BorrowerName, d.BorrowerFather, d.BorrowerFamily);

        var applicant = d.ExecutionApplicants
            .Select(a => FormatName(a.Name, a.Father, a.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(applicant))
            return applicant;

        var entity = d.ExecutedPublicEntities
            .Select(e => e.EntityName)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(entity))
            return entity;

        return d.ExecutedNaturalPersons
            .Select(p => FormatName(p.Name, p.Father, p.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    public static string FormatName(string? first, string? father, string? family) =>
        string.Join(' ', new[] { first, father, family }.Where(v => !string.IsNullOrWhiteSpace(v)));

    public static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? DateText(DateTime? value) =>
        value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static void ApplyCircuitDerivation(Document doc, ExecutionCircuit? circuit, DocumentUpsertRequest request)
    {
        if (circuit is not null)
        {
            doc.ExecutionCircuitId = circuit.Id;
            doc.Court = circuit.Name;
            doc.CourtNorm = ArabicNameNormalizer.Normalize(circuit.Name);
        }
        else
        {
            // انتقالي (فرع بلا سجل): إبقاء النص الحر المرسل — يُتجاهل فقط عند وجود دائرة.
            doc.ExecutionCircuitId = request.ExecutionCircuitId;
            doc.Court = request.Court;
        }
    }

    /// <summary>حارس المعلق الصريح: تُمنع عليه العمليات التي تلزم الهوية الرقمية.</summary>
    public static void EnsureNotPendingRegistration(Document doc, string actionLabel)
    {
        if (doc.NeedsRegistration)
            throw new ArgumentException($"لا يمكن {actionLabel} على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");
    }
}
