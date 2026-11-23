using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IBranchManagementService
{
    Task<List<BranchDto>> ListBranchesAsync(CancellationToken ct = default);
}

/// <summary>
/// إدارة الفروع (قراءة القائمة؛ الإنشاء/التعديل/الحذف في Commands `P4-01`).
/// التحقق من الصلاحية في المتحكم، والتحقق المنطقي والكتابة في المعالجات ضمن معاملة مع سجل التدقيق.
/// المحافظة إجبارية في الإنشاء والتعديل (تحدد نطاق رئيس القسم في سجل الجهات العامة).
/// الحذف النهائي محصور بالفروع غير المستخدمة؛ الفرع المستخدم يُعطَّل (IsActive) بدلاً من الحذف.
/// </summary>
public sealed class BranchManagementService : IBranchManagementService
{
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public BranchManagementService(
        IBranchRepository branches,
        IUnitOfWork uow,
        IAuditLogger audit,
        IReferenceDataCache? cache = null)
    {
        _branches = branches;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    // الأسماء/الترتيب مرجعي؛ عدّادات المستخدمين/المستندات تُحسب حيّة بعد القراءة.
    private Task<List<Branch>> ListBranchesTrackedAsync(CancellationToken ct)
        => _cache is null
            ? _branches.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Branches, "all", _branches.ListNoTrackingAsync, ct);

    public async Task<List<BranchDto>> ListBranchesAsync(CancellationToken ct = default)
    {
        var branches = await ListBranchesTrackedAsync(ct);
        var userCounts = await _branches.CountUsersByBranchAsync(ct);
        var documentCounts = await _branches.CountDocumentsByBranchAsync(ct);

        return branches
            .OrderBy(b => b.Name)
            .Select(b => ToDto(
                b,
                userCounts.GetValueOrDefault(b.Id),
                documentCounts.GetValueOrDefault(b.Id)))
            .ToList();
    }


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
