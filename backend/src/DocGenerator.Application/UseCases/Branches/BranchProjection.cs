using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Branches;

/// <summary>
/// مساعدات الفروع المشتركة (`P4-01cs`): تطبيع المدخلات وإسقاط `BranchDto`
/// يستهلكها معالج القراءة ومعالجا الإنشاء/التعديل معًا بلا تكرار.
/// </summary>
internal static class BranchProjection
{
    internal static string NormalizeRequired(string? value, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException(message);
        return normalized;
    }

    internal static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    internal static BranchDto ToDto(Branch branch, int userCount, int documentCount) => new(
        branch.Id,
        branch.Name,
        branch.Code,
        branch.Address,
        branch.Phone,
        branch.Governorate,
        branch.IsActive,
        userCount,
        documentCount,
        branch.Version);
}
