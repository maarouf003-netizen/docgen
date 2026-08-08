using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public interface IBranchManagementService
{
    Task<List<BranchDto>> ListBranchesAsync(CancellationToken ct = default);
    Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, string? actorName, CancellationToken ct = default);
    Task<BranchDto?> UpdateBranchAsync(int branchId, UpdateBranchRequest request, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteBranchAsync(int branchId, string? actorName, CancellationToken ct = default);
}

/// <summary>
/// إدارة الفروع (إضافة/تعديل/حذف) — مشرف النظام فقط.
/// التحقق من الصلاحية في المتحكم، والتحقق المنطقي والكتابة هنا ضمن معاملة مع سجل التدقيق.
/// الحذف النهائي محصور بالفروع غير المستخدمة؛ الفرع المستخدم يُعطَّل (IsActive) بدلاً من الحذف.
/// </summary>
public sealed class BranchManagementService : IBranchManagementService
{
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public BranchManagementService(
        IBranchRepository branches,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _branches = branches;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<BranchDto>> ListBranchesAsync(CancellationToken ct = default)
    {
        var branches = await _branches.ListAsync(ct);
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

    public async Task<BranchDto> CreateBranchAsync(CreateBranchRequest request, string? actorName, CancellationToken ct = default)
    {
        var name = NormalizeRequired(request.Name, "اسم الفرع مطلوب");
        var code = NormalizeRequired(request.Code, "كود الفرع مطلوب");

        if (await _branches.NameExistsAsync(name, null, ct))
            throw new ArgumentException("اسم الفرع مستخدم مسبقاً");
        if (await _branches.CodeExistsAsync(code, null, ct))
            throw new ArgumentException("كود الفرع مستخدم مسبقاً");

        var branch = new Branch
        {
            Name = name,
            Code = code,
            Address = NormalizeOptional(request.Address),
            Phone = NormalizeOptional(request.Phone),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _branches.AddAsync(branch, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_branch",
                details: $"أنشأ فرعاً: {branch.Name} ({branch.Code})", ct: token);
        }, ct);

        return ToDto(branch, 0, 0);
    }

    public async Task<BranchDto?> UpdateBranchAsync(int branchId, UpdateBranchRequest request, string? actorName, CancellationToken ct = default)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            return null;

        var name = NormalizeRequired(request.Name, "اسم الفرع مطلوب");
        var code = NormalizeRequired(request.Code, "كود الفرع مطلوب");

        if (await _branches.NameExistsAsync(name, branch.Id, ct))
            throw new ArgumentException("اسم الفرع مستخدم مسبقاً");
        if (await _branches.CodeExistsAsync(code, branch.Id, ct))
            throw new ArgumentException("كود الفرع مستخدم مسبقاً");

        branch.Name = name;
        branch.Code = code;
        branch.Address = NormalizeOptional(request.Address);
        branch.Phone = NormalizeOptional(request.Phone);
        branch.IsActive = request.IsActive;

        var userCounts = await _branches.CountUsersByBranchAsync(ct);
        var documentCounts = await _branches.CountDocumentsByBranchAsync(ct);

        return await _tx.RunAsync(async token =>
        {
            _branches.Update(branch);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_branch",
                details: $"عدّل الفرع: {branch.Name} ({branch.Code})", ct: token);
            return ToDto(
                branch,
                userCounts.GetValueOrDefault(branch.Id),
                documentCounts.GetValueOrDefault(branch.Id));
        }, ct);
    }

    public async Task<bool> DeleteBranchAsync(int branchId, string? actorName, CancellationToken ct = default)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            return false;

        if (await _branches.HasUsersAsync(branchId, ct))
            throw new ArgumentException("لا يمكن حذف فرع يحتوي على مستخدمين؛ عطّل الفرع بدلاً من ذلك");
        if (await _branches.HasDocumentsAsync(branchId, ct))
            throw new ArgumentException("لا يمكن حذف فرع يحتوي على مستندات؛ عطّل الفرع بدلاً من ذلك");

        return await _tx.RunAsync(async token =>
        {
            _branches.Remove(branch);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_branch",
                details: $"حذف الفرع: {branch.Name} ({branch.Code})", ct: token);
            return true;
        }, ct);
    }

    private static string NormalizeRequired(string? value, string message)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException(message);
        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static BranchDto ToDto(Branch branch, int userCount, int documentCount) => new(
        branch.Id,
        branch.Name,
        branch.Code,
        branch.Address,
        branch.Phone,
        branch.IsActive,
        userCount,
        documentCount);
}
