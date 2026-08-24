using System.Text.Json;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// بناء نص البحث الموحّد للملف (SearchText): أسماء الأطراف والأرقام والحقول
/// المفتاحية في نص واحد يغذي البحث السريع. الصيغة الوحيدة المشتركة بين إنشاء/
/// تعديل الملفات ومزامنة أسماء الجهات من السجل المرجعي.
/// </summary>
public static class DocumentSearchTextBuilder
{
    public const int MaxLength = 1000;

    public static string Build(Document doc)
    {
        var parts = new[] { doc.BorrowerName, doc.BorrowerFamily, doc.Applicant, doc.Lawyer,
            doc.Court, doc.FileNumber, doc.ContractNumber, doc.AnnexNumber, doc.BorrowerNationalId,
            doc.BorrowerRegistrationNumber, doc.BorrowerRepresentedBy,
            doc.FileArrivalNumber, doc.FileArrivalDate }
            .Where(v => !string.IsNullOrWhiteSpace(v));
        // أسماء ورثة المتوفين (المقترض/الكفلاء) تنضم إلى نص البحث ليكون البحث بأسماء الورثة
        // متسقًا عبر SearchText وفلتر الورثة المباشر في المستودع.
        var applicantHeirNames = doc.Heirs
            .Select(h => string.Join(' ', h.HeirName, h.HeirFather, h.HeirFamily))
            .Where(v => !string.IsNullOrWhiteSpace(v));
        parts = parts.Concat(applicantHeirNames);
        // أسماء الكفلاء الاعتباريين وأرقام تسجيلهم تنضم إلى نص البحث.
        var guarantorLegalNames = doc.Guarantors
            .SelectMany(g => new[] { g.GuarantorName, g.GuarantorRegistrationNumber, g.GuarantorRepresentedBy })
            .Where(v => !string.IsNullOrWhiteSpace(v));
        parts = parts.Concat(guarantorLegalNames);
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
        {
            // ملف «منفذ عليه»/«عرض وايداع»: اسم البحث يضم أسماء طلبات التنفيذ/العرض
            // والجهات/الأشخاص المنفذ عليهم وأرقام تسجيلهم وورثة الجهات.
            var applicantNames = doc.ExecutionApplicants
                .Select(a => string.Join(' ', a.Name, a.Father, a.Family))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var applicantLegalFields = doc.ExecutionApplicants
                .SelectMany(a => new[] { a.ApplicantRegistrationNumber, a.ApplicantRepresentedBy })
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedNames = doc.ExecutedPublicEntities
                .Select(e => string.Join(' ', e.EntityName, e.Governorate))
                .Concat(doc.ExecutedNaturalPersons.Select(p => string.Join(' ', p.Name, p.Father, p.Family)))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var entityLegalFields = doc.ExecutedPublicEntities
                .SelectMany(e => new[] { e.RegistrationNumber, e.RepresentedBy })
                .Where(v => !string.IsNullOrWhiteSpace(v));
            var executedHeirNames = doc.ExecutedHeirs
                .Select(h => string.Join(' ', h.HeirName, h.HeirFather, h.HeirFamily))
                .Where(v => !string.IsNullOrWhiteSpace(v));
            parts = parts
                .Concat(applicantNames)
                .Concat(applicantLegalFields)
                .Concat(executedNames)
                .Concat(entityLegalFields)
                .Concat(executedHeirNames);
        }
        return Truncate(string.Join(' ', parts));
    }

    /// <summary>
    /// SearchText معرّف بحد طول 1000 (HasMaxLength)؛ PostgreSQL يرفض القيم الأطول عند
    /// الإدراج/التحديث بخلاف SQLite. يُقتطع إلى الحد الأقصى ليبقى عمود البحث متسقًا.
    /// </summary>
    public static string Truncate(string value)
    {
        if (value.Length <= MaxLength)
            return value;

        // تجنّب قصّ بداية زوج بديل UTF-16 (surrogate pair) في النهاية.
        var end = MaxLength;
        if (end > 0 && char.IsHighSurrogate(value[end - 1]) && end < value.Length && char.IsLowSurrogate(value[end]))
            end--;
        return value[..end];
    }

    /// <summary>لقطات FullData المختصرة المعروضة في سجل التدقيق.</summary>
    public static string BuildFullData(Document doc) =>
        JsonSerializer.Serialize(new
        {
            doc.BorrowerName, doc.BorrowerFamily, doc.AmountNumeric, doc.Currency,
            doc.ContractNumber, doc.Court, doc.Applicant, doc.Lawyer
        });
}
