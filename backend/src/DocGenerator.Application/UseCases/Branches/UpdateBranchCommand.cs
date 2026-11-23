using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Branches;

/// <summary>
/// تعديل فرع (اسم/كود/محافظة/حالة) — يملك معاملته وتدقيقه وإبطال كاش البيانات المرجعية.
/// غياب الفرع يعيد `null` بلا أثر جانبي (كالأصل).
/// </summary>
public sealed record UpdateBranchCommand(
    int BranchId,
    UpdateBranchRequest Request,
    string? ActorName) : ICommand<BranchDto?>, ITransactionalRequest;

public sealed class UpdateBranchCommandHandler
    : CommandHandlerBase<UpdateBranchCommand, BranchDto?>
{
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public UpdateBranchCommandHandler(
        IBranchRepository branches,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateBranchCommand, BranchDto?>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<BranchDto?> HandleCoreAsync(
        UpdateBranchCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var branch = await _branches.GetByIdAsync(command.BranchId, ct);
        if (branch is null)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(branch, request.Version, "الفرع");

        var name = BranchManagementService.NormalizeRequired(request.Name, "اسم الفرع مطلوب");
        var code = BranchManagementService.NormalizeRequired(request.Code, "كود الفرع مطلوب");
        // المحافظة إجبارية حتى في التعديل — إن كانت فرعًا قديمًا بلا محافظة فالتعديل يفرض اختيارها
        // (تُشفى البيانات القديمة تدريجيًا عبر نفس الشاشة بلا هجرة بيانات).
        var governorate = BranchManagementService.NormalizeRequired(
            request.Governorate, "المحافظة مطلوبة — اختر محافظة الفرع من القائمة");
        if (!GovernorateCatalog.IsGovernorate(governorate))
            throw new ArgumentException("المحافظة غير معتمدة — اختر محافظة الفرع من القائمة");

        if (await _branches.NameExistsAsync(name, branch.Id, ct))
            throw new ArgumentException("اسم الفرع مستخدم مسبقاً");
        if (await _branches.CodeExistsAsync(code, branch.Id, ct))
            throw new ArgumentException("كود الفرع مستخدم مسبقاً");

        branch.Name = name;
        branch.Code = code;
        branch.Address = BranchManagementService.NormalizeOptional(request.Address);
        branch.Phone = BranchManagementService.NormalizeOptional(request.Phone);
        branch.Governorate = governorate;
        branch.IsActive = request.IsActive;

        var userCounts = await _branches.CountUsersByBranchAsync(ct);
        var documentCounts = await _branches.CountDocumentsByBranchAsync(ct);

        _branches.Update(branch);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Branches);
        await _audit.LogAsync(command.ActorName, "update_branch",
            details: $"عدّل الفرع: {branch.Name} ({branch.Code})", ct: ct);
        return BranchManagementService.ToDto(
            branch,
            userCounts.GetValueOrDefault(branch.Id),
            documentCounts.GetValueOrDefault(branch.Id));
    }
}
