using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IUserManagementService
{
    Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default);
    Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default);
    Task<LawyerListItemDto?> UpdateLawyerAsync(int userId, UpdateLawyerRequest request, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<bool> SetLawyerActiveAsync(int userId, bool isActive, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<List<UserListItemDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, string? actorName, CancellationToken ct = default);
    Task<UserListItemDto?> UpdateUserAsync(int userId, UpdateUserRequest request, int actorUserId, string? actorName, CancellationToken ct = default);
}

/// <summary>
/// إدارة محامي الفرع (رئيس القسم/مشرف) وإدارة المستخدمين الكاملة (مشرف).
/// التحقق من الصلاحية/النطاق في المتحكم، والتحقق المنطقي والكتابة هنا.
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private const int MinPasswordLength = 6;

    private static readonly UserRole[] BranchRoles = { UserRole.Lawyer, UserRole.Head };

    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UserManagementService(
        IUserRepository users,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _users = users;
        _branches = branches;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default)
    {
        var lawyers = await _users.ListLawyersAsync(branchId, ct);
        return lawyers
            .OrderBy(l => l.FullName)
            .Select(ToLawyerDto)
            .ToList();
    }

    public async Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Username);
        ValidateUsername(username);
        ValidatePassword(request.Password);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        if (await _users.UsernameExistsAsync(username, branchId, null, ct))
            throw new ArgumentException(DuplicateUsernameMessage(branchId));

        var branch = await _branches.GetByIdAsync(branchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            IsActive = true,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_user", details: $"أنشأ محامياً: {user.FullName} ({user.Username}) في فرع {branch.Name}", ct: token);
        }, ct);

        return new LawyerListItemDto(user.Id, user.Username, user.FullName, user.IsActive, user.BranchId, branch.Name);
    }

    public async Task<LawyerListItemDto?> UpdateLawyerAsync(int userId, UpdateLawyerRequest request, int? scopeBranchId, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.Role != UserRole.Lawyer)
            return null;

        // رئيس القسم يعدّل محامي فرعه فقط.
        if (scopeBranchId.HasValue && user.BranchId != scopeBranchId)
            return null;

        if (string.IsNullOrWhiteSpace(request.FullName) && string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("لا يوجد تغيير لإجرائه — حدّد اسماً جديداً أو كلمة مرور جديدة");

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = NormalizeUsername(request.FullName);
            ValidateUsername(newUsername);
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, user.BranchId, user.Id, ct))
                throw new ArgumentException(DuplicateUsernameMessage(user.BranchId));

            user.FullName = request.FullName.Trim();
            user.Username = newUsername;
        }

        user.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                ValidatePassword(request.Password);
                user.PasswordHash = _hasher.Hash(request.Password);
                // إبطال الرموز الصادرة سابقاً عند تغيير كلمة المرور.
                user.TokenVersion++;
            }

            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_user",
                details: $"عدّل المحامي: {user.FullName} ({user.Username})", ct: token);
            return ToLawyerDto(user);
        }, ct);
    }

    public async Task<bool> SetLawyerActiveAsync(int userId, bool isActive, int? scopeBranchId, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null || user.Role != UserRole.Lawyer)
            return false;

        // رئيس القسم يستطيع التحكم بمحامي فرعه فقط.
        if (scopeBranchId.HasValue && user.BranchId != scopeBranchId)
            return false;

        if (user.IsActive == isActive)
            return true;

        return await _tx.RunAsync(async token =>
        {
            user.IsActive = isActive;
            user.UpdatedAt = DateTime.UtcNow;
            // إبطال الرموز الصادرة سابقاً عند الإيقاف.
            if (!isActive)
                user.TokenVersion++;
            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_user",
                details: $"{(isActive ? "أعاد تفعيل" : "أوقف")} المحامي: {user.FullName} ({user.Username})", ct: token);
            return true;
        }, ct);
    }

    public async Task<List<UserListItemDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var users = await _users.ListAllUsersAsync(ct);
        return users.Select(ToUserDto).ToList();
    }

    public async Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, string? actorName, CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Username);
        ValidateUsername(username);
        ValidatePassword(request.Password);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        var role = ParseRole(request.Role);
        var branchId = await ResolveBranchAsync(request.BranchId, role, ct);

        if (await _users.UsernameExistsAsync(username, branchId, null, ct))
            throw new ArgumentException(DuplicateUsernameMessage(branchId));

        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Role = role,
            BranchId = branchId,
            IsActive = true,
            PasswordHash = _hasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_user",
                details: $"أنشأ مستخدماً: {user.FullName} ({user.Username}) بدور {role}", ct: token);
        }, ct);

        return ToUserDto(user);
    }

    public async Task<UserListItemDto?> UpdateUserAsync(int userId, UpdateUserRequest request, int actorUserId, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return null;

        var role = request.Role is null ? user.Role : ParseRole(request.Role);
        var branchId = await ResolveBranchAsync(request.BranchId ?? user.BranchId, role, ct);

        // انضباط بيانات (بوابة الجهات): الانتقال بعيدًا عن دور المندوب يفكّ نطاق
        // البوابة كليًا فلا تبقى ارتباطات خاملة تتراكم بلا دور يستخدمها.
        if (user.Role == UserRole.EntityManager && role != UserRole.EntityManager)
        {
            user.PortalGroupId = null;
            user.PortalEntryId = null;
        }

        // منع المشرف من قفل حسابه أو خفض دوره بنفسه (تفادي فقدان الوصول).
        if (userId == actorUserId && (!request.IsActive || role != UserRole.Admin))
            throw new ArgumentException("لا يمكنك إيقاف حسابك أو تغيير دورك أنت بنفسك");

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = ArabicNameNormalizer.Normalize(request.FullName.Trim());
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, branchId, user.Id, ct))
                throw new ArgumentException(DuplicateUsernameMessage(branchId));

            user.FullName = request.FullName.Trim();
            user.Username = newUsername;
        }
        user.Role = role;
        user.BranchId = branchId;
        user.UpdatedAt = DateTime.UtcNow;

        return await _tx.RunAsync(async token =>
        {
            if (user.IsActive != request.IsActive)
            {
                user.IsActive = request.IsActive;
                // إبطال الرموز الصادرة سابقاً عند الإيقاف.
                if (!request.IsActive)
                    user.TokenVersion++;
            }

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                ValidatePassword(request.Password);
                user.PasswordHash = _hasher.Hash(request.Password);
                user.TokenVersion++;
            }

            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_user",
                details: $"عدّل المستخدم: {user.FullName} ({user.Username})", ct: token);
            return ToUserDto(user);
        }, ct);
    }

    private async Task<int?> ResolveBranchAsync(int? branchId, UserRole role, CancellationToken ct)
    {
        if (!BranchRoles.Contains(role))
            return null;

        if (branchId is null)
            throw new ArgumentException("يجب تحديد الفرع لهذا الدور");

        var branch = await _branches.GetByIdAsync(branchId.Value, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        return branch.Id;
    }

    private static UserRole ParseRole(string? role)
    {
        if (!Enum.TryParse<UserRole>(role?.Trim(), ignoreCase: true, out var parsed))
            throw new ArgumentException("دور غير صالح");
        return parsed;
    }

    private static string NormalizeUsername(string username)
    {
        var normalized = ArabicNameNormalizer.Normalize(username);
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("الاسم الثلاثي مطلوب");
        return normalized;
    }

    private static void ValidateUsername(string username)
    {
        if (username.Length > 50)
            throw new ArgumentException("الاسم الثلاثي أطول من المسموح (50 حرفاً)");
    }

    private static string DuplicateUsernameMessage(int? branchId) => branchId is null
        ? "يوجد مستخدم بنفس الاسم الثلاثي، يرجى اختيار اسم مختلف"
        : "يوجد مستخدم بنفس الاسم الثلاثي في نفس الفرع، يرجى اختيار اسم مختلف";

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"كلمة المرور يجب أن تكون {MinPasswordLength} أحرف على الأقل");
    }

    private static LawyerListItemDto ToLawyerDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.IsActive,
        user.BranchId,
        user.Branch?.Name);

    private static UserListItemDto ToUserDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Role.ToString().ToLowerInvariant(),
        user.BranchId,
        user.Branch?.Name,
        user.IsActive);
}
