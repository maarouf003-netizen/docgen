using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>
/// مساعدات الشعب المشتركة (`P4-01cs`): تطبيع الاسم، ووحدانية الاسم × الفرع،
/// والإسقاط الموحّد — نقطة واحدة لمعالجَي القراءة ومعالجات الإنشاء/التسمية/التفعيل
/// بلا تكرار. القراءات المرجعية المخزّنة في `ReferenceDataQueries` المشترك.
/// </summary>
internal static class SectionProjection
{
    internal static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الشعبة مطلوب");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("اسم الشعبة أطول من 200 حرف");
        return trimmed;
    }

    /// <summary>وحدانية الاسم × الفرع (بلا حساسية للحالة/التشكيل عبر `NameNorm`) — مشتركة مع معالج الإنشاء.</summary>
    internal static bool NameExists(
        IEnumerable<Section> all, int branchId, string norm, int? excludeId)
        => all.Any(s => s.BranchId == branchId
            && s.NameNorm == norm
            && (excludeId == null || s.Id != excludeId.Value));

    /// <summary>
    /// إسقاط الشعبة الموحّد (عدّاد الدوائر + رئيسها الشاغل) — نقطة واحدة للقراءة
    /// ولمعالجات التسمية/التفعيل فلا تنحرف الحقول بين المسارات.
    /// </summary>
    internal static SectionDto ToDto(
        Section section,
        string? branchName,
        IReadOnlyList<ExecutionCircuit> circuits,
        IReadOnlyList<User> users)
    {
        var head = users
            .Where(u => u.IsActive && u.Role == UserRole.SubHead)
            .OrderBy(u => u.Id)
            .FirstOrDefault();
        return new SectionDto(
            section.Id,
            section.BranchId,
            branchName,
            section.Name,
            section.IsActive,
            circuits.Count(c => c.SectionId == section.Id),
            head?.FullName,
            section.Version);
    }
}
