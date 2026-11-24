using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Auth;

/// <summary>تغيير كلمة مرور المستخدم بعد التحقق من القديمة — معاملة واحدة وتدقيق.</summary>
public sealed record ChangePasswordCommand(
    int UserId,
    string OldPassword,
    string NewPassword) : ICommand<bool>;

internal sealed class ChangePasswordCommandHandler
    : CommandHandlerBase<ChangePasswordCommand, bool>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ChangePasswordCommandHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ChangePasswordCommand, bool>> behaviors)
        : base(behaviors)
    {
        _users = users;
        _hasher = hasher;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        ChangePasswordCommand command, CancellationToken ct)
    {
        // M-02: القاعدة المعيارية — ما يُفحَص هو ما يُخزَّن. نقصّ أولًا ثم نتحقق ثم نخزّن
        // المقصوص نفسه، وبنفس ترتيب الأخطاء السابق (400 قبل فشل التحقق من القديمة).
        var candidate = command.NewPassword?.Trim() ?? string.Empty;
        PasswordPolicy.EnsureValid(candidate);

        var user = await _users.GetByIdAsync(command.UserId, ct);
        if (user is null || !_hasher.Verify(command.OldPassword, user.PasswordHash))
            return false;

        if (PasswordPolicy.Validate(candidate, user.Username) is { } usernameError)
            throw new ArgumentException(usernameError);

        return await _tx.RunAsync(async token =>
        {
            user.PasswordHash = _hasher.Hash(candidate);
            user.TokenVersion++;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(user.Username, "change_password", details: "تغيير كلمة المرور", ct: token);
            return true;
        }, ct);
    }
}
