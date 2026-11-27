using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Auth;

/// <summary>
/// تسجيل خروج مُبطل خادميًا (W2/F3): رفع `TokenVersion` يُسقط كل الرموز الصادرة
/// للحساب، فلا يبقى رمز مسروق صالحًا حتى انتهاء عمره بعد أن ضغط صاحبه «خروج».
/// </summary>
public sealed record LogoutCommand(
    int UserId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

internal sealed class LogoutCommandHandler : CommandHandlerBase<LogoutCommand, bool>
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public LogoutCommandHandler(
        IUserRepository users,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<LogoutCommand, bool>> behaviors)
        : base(behaviors)
    {
        _users = users;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(LogoutCommand command, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(command.UserId, ct);
        // حساب محذوف/غير موجود: لا شيء لإبطاله، والخروج يبقى ناجحًا (حذف الكوكي في المتحكم).
        if (user is null)
            return false;

        if (user.IsActive)
        {
            user.TokenVersion++;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
        }

        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(
            command.ActorName ?? user.Username,
            "logout",
            details: "تسجيل خروج — أُبطلت الجلسات الصادرة",
            ct: ct);
        return true;
    }
}
