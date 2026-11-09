using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common;

/// <summary>
/// اسم عرض موحّد للملف يُستخدم في التذكيرات وبطاقات الاستئناف والتنبيهات.
/// يعوّض الاختلاف البنيوي بين عائلة «طالبة تنفيذ» (اسم المقترض) وعائلة
/// «منفذ عليها/عرض وايداع» (طالب/منفذ عليه من مجموعات الأطراف)، فلا يعود
/// الاسم يسقط إلى نوع المستند أو حالته.
/// </summary>
public static class DocumentDisplayName
{
    public static string Build(Document doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return BuildExecutedLike(doc);

        var borrower = JoinName(doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily);
        if (!string.IsNullOrWhiteSpace(borrower))
            return borrower;

        if (!string.IsNullOrWhiteSpace(doc.Applicant))
            return doc.Applicant.Trim();

        return Fallback(doc);
    }

    private static string BuildExecutedLike(Document doc)
    {
        // الأولوية مطابقة لعنوان عائلة «منفذ عليه» في الواجهة:
        // أول منفذ عليه طبيعي ← أول منفذ عليه اعتباري ← أول طالب تنفيذ/عرض.
        var executedPerson = doc.ExecutedNaturalPersons
            .Select(p => JoinName(p.Name, p.Father, p.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(executedPerson))
            return executedPerson;

        var executedEntity = doc.ExecutedPublicEntities
            .Select(e => e.EntityName?.Trim())
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(executedEntity))
            return executedEntity!;

        var applicant = doc.ExecutionApplicants
            .Select(a => PartyNatureCatalog.IsLegal(a.ApplicantNature)
                ? a.Name?.Trim()
                : JoinName(a.Name, a.Father, a.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(applicant))
            return applicant!;

        if (!string.IsNullOrWhiteSpace(doc.Applicant))
            return doc.Applicant.Trim();

        return Fallback(doc);
    }

    private static string Fallback(Document doc)
    {
        var side = GeneralEntitySideCatalog.ToLabel(doc.GeneralEntitySide);
        return string.IsNullOrWhiteSpace(side) ? $"ملف رقم {doc.Id}" : side;
    }

    private static string JoinName(params string?[] parts) =>
        string.Join(' ', parts
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim()));
}
