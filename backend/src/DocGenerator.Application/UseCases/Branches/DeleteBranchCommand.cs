using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Branches;

/// <summary>
/// حذف فرع غير مستخدم (لا مستخدمون ولا مستندات) — يملك معاملته وتدقيقه
/// وإبطال كاش البيانات المرجعية.
/// </summary>
public sealed record DeleteBranchCommand(int BranchId, string? ActorName)
    : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteBranchCommandHandler : CommandHandlerBase<DeleteBranchCommand, bool>
{
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public DeleteBranchCommandHandler(
        IBranchRepository branches,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteBranchCommand, bool>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<bool> HandleCoreAsync(DeleteBranchCommand command, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(command.BranchId, ct);
        if (branch is null)
            return false;

        if (await _branches.HasUsersAsync(command.BranchId, ct))
            throw new ArgumentException("لا يمكن حذف فرع يحتوي على مستخدمين؛ عطّل الفرع بدلاً من ذلك");
        if (await _branches.HasDocumentsAsync(command.BranchId, ct))
            throw new ArgumentException("لا يمكن حذف فرع يحتوي على مستندات؛ عطّل الفرع بدلاً من ذلك");

        _branches.Remove(branch);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Branches);
        await _audit.LogAsync(command.ActorName, "delete_branch",
            details: $"حذف الفرع: {branch.Name} ({branch.Code})", ct: ct);
        return true;
    }
}
