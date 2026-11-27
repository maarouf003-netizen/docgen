using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>
/// تفعيل/إيقاف محامٍ ضمن نطاق الفرع (ولرئيس الشعبة ضمن شعبته — W2/B3) — يملك معاملته
/// وتدقيقه، ويعيد `false` للغياب/الدور غير المطابق/الخروج عن النطاق (يُترجم 404).
/// </summary>
public sealed record SetLawyerActiveCommand(
    int UserId,
    bool IsActive,
    int? ScopeBranchId,
    string? ActorName,
    int? ScopeSectionId = null,
    int? ActorUserId = null) : ICommand<bool>, ITransactionalRequest;

internal sealed class SetLawyerActiveCommandHandler
    : CommandHandlerBase<SetLawyerActiveCommand, bool>
{
    private readonly IUserManagementLawyerWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public SetLawyerActiveCommandHandler(
        IUserManagementLawyerWriteRules rules,
        IUserRepository users,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SetLawyerActiveCommand, bool>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(SetLawyerActiveCommand command, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(command.UserId, ct);
        if (user is null || user.Role != UserRole.Lawyer)
            return false;

        // رئيس القسم يستطيع التحكم بمحامي فرعه فقط، ورئيس الشعبة بمحامي شعبته فقط.
        if (command.ScopeBranchId.HasValue && user.BranchId != command.ScopeBranchId)
            return false;
        if (command.ScopeSectionId.HasValue)
        {
            if (command.ActorUserId is null)
                return false;
            var inSectionScope = await _rules.IsLawyerInSectionScopeAsync(
                user, command.ActorUserId.Value, command.ScopeBranchId, command.ScopeSectionId, ct);
            if (!inSectionScope)
                return false;
        }

        if (user.IsActive == command.IsActive)
            return true;

        user.IsActive = command.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        // إبطال الرموز الصادرة سابقاً عند الإيقاف.
        if (!command.IsActive)
            user.TokenVersion++;
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "update_user",
            details: $"{(command.IsActive ? "أعاد تفعيل" : "أوقف")} المحامي: {user.FullName} ({user.Username})", ct: ct);
        return true;
    }
}
