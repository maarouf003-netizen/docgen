using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken ct = default);
    Task<UserDto?> GetUserAsync(int userId, CancellationToken ct = default);
}

public sealed class AuthService : IAuthService
{
    private const int MinPasswordLength = 6;

    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokenService;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly LockoutOptions _lockout;

    public AuthService(IUserRepository users, IUnitOfWork uow, IPasswordHasher hasher, ITokenService tokenService, ITransactionRunner tx, IAuditLogger audit, IOptions<LockoutOptions> lockout)
    {
        _users = users;
        _uow = uow;
        _hasher = hasher;
        _tokenService = tokenService;
        _tx = tx;
        _audit = audit;
        _lockout = lockout.Value;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var matches = await _users.FindByUsernameAllAsync(username, ct);
        var candidates = matches.Where(m => m.IsActive).ToList();

        User? user;
        if (candidates.Count == 1)
        {
            user = candidates[0];
        }
        else if (candidates.Count > 1)
        {
            if (request.BranchId.HasValue)
            {
                // 0 يُرسل عند اختيار الحساب الذي لا يتبع فرعاً؛ والقيمة الموجبة تطابق معرّف الفرع.
                user = request.BranchId == 0
                    ? candidates.FirstOrDefault(m => m.BranchId is null)
                    : candidates.FirstOrDefault(m => m.BranchId == request.BranchId);
            }
            else
            {
                // القرار المتعمّد: اختيار الفرع يسبق التحقق من كلمة المرور، لأن كلمة المرور
                // لا يمكن التحقق منها قبل معرفة الحساب المقصود. بهذا لا يُكشَف أي شيء عن صحة
                // كلمة المرور في هذه المرحلة (المحاولة الخاطئة تمرّ بمرحلة الاختيار ثم تفشل)،
                // والاسم الثلاثي نفسه معلن أصلاً في الملفات، لذا كشف وجود حسابات به ضمن فروع
                // مختلفة غير مؤثر أمنياً. التخمين الفعلي لكلمة المرور يبقى مقيداً بمحدد المحاولات
                // وبقفل الحساب بمجرد اختيار الفرع.
                var branches = candidates
                    .Select(m => new LoginBranchChoiceDto(m.BranchId, m.Branch?.Name))
                    .ToList();
                return new LoginResult(LoginStatus.BranchSelectionRequired, null, branches);
            }
        }
        else
        {
            user = null;
        }

        if (user is null)
        {
            await _audit.LogAsync(username, "login_failed", details: "محاولة دخول فاشلة", ct: ct);
            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEndUtc is DateTime lockoutEnd && lockoutEnd > now)
            return new LoginResult(LoginStatus.LockedOut, null);

        // انتهت مدة القفل: تصفير العداد وتحرير الحساب قبل معالجة المحاولة
        if (user.LockoutEndUtc is not null)
        {
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.UpdatedAt = now;
            _users.Update(user);
            await _uow.SaveChangesAsync(ct);
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            user.UpdatedAt = now;
            var locked = false;
            if (user.FailedLoginCount >= Math.Max(1, _lockout.MaxFailedAttempts))
            {
                user.LockoutEndUtc = now.AddMinutes(Math.Max(1, _lockout.LockoutMinutes));
                user.FailedLoginCount = 0;
                locked = true;
            }
            _users.Update(user);
            await _uow.SaveChangesAsync(ct);
            await _audit.LogAsync(username, "login_failed", details: "محاولة دخول فاشلة", ct: ct);
            if (locked)
            {
                await _audit.LogAsync(username, "login_locked",
                    details: $"قفل الحساب مؤقتاً بعد {Math.Max(1, _lockout.MaxFailedAttempts)} محاولات فاشلة", ct: ct);
            }
            return new LoginResult(LoginStatus.InvalidCredentials, null);
        }

        return await _tx.RunAsync(async token =>
        {
            user.FailedLoginCount = 0;
            user.LockoutEndUtc = null;
            user.LastLogin = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(user.Username, "login", details: "تسجيل دخول ناجح", ct: token);
            return new LoginResult(LoginStatus.Success,
                new LoginResponse(_tokenService.CreateToken(user), ToDto(user)));
        }, ct);
    }

    public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinPasswordLength)
            throw new ArgumentException($"كلمة المرور يجب أن تكون {MinPasswordLength} أحرف على الأقل");

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || !_hasher.Verify(oldPassword, user.PasswordHash))
            return false;

        return await _tx.RunAsync(async token =>
        {
            user.PasswordHash = _hasher.Hash(newPassword);
            user.TokenVersion++;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(user.Username, "change_password", details: "تغيير كلمة المرور", ct: token);
            return true;
        }, ct);
    }

    public async Task<UserDto?> GetUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        return user is null ? null : ToDto(user);
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Role.ToString().ToLowerInvariant(),
        user.BranchId,
        user.Branch?.Name);
}
