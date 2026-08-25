using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IEntityDelegateService
{
    Task<List<DelegateDto>> ListAsync(CancellationToken ct = default);

    /// <summary>إنشاء حساب مندوب مربوط بنطاقه — يجب تحديد هوية أو قيدًا واحدًا حصرًا (د11).</summary>
    Task<DelegateDto> CreateAsync(CreateDelegateRequest request, string? actorName, CancellationToken ct = default);

    /// <summary>تعديل حساب مندوب قائم (الاسم/التفعيل/كلمة المرور/نطاقه) — null إن لم يوجد.</summary>
    Task<DelegateDto?> UpdateAsync(int delegateUserId, UpdateDelegateRequest request, string? actorName, CancellationToken ct = default);
}

/// <summary>
/// إدارة حسابات مندوبي الجهات العامة داخل النظام نفسه (د11): يضيفها المدير/
/// المشرف/رئيس القسم ويربط كل حساب بنطاقه (هوية أم أو قيد بعينه) — والتحقق
/// من الصلاحية في المتحكم عبر RolePermissions.CanManageDelegates.
/// </summary>
public sealed class EntityDelegateService : IEntityDelegateService
{
    private const int MaxUsernameLength = 50;

    private readonly IUserRepository _users;
    private readonly IPublicEntityRepository _registry;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public EntityDelegateService(
        IUserRepository users,
        IPublicEntityRepository registry,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _users = users;
        _registry = registry;
        _hasher = hasher;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<DelegateDto>> ListAsync(CancellationToken ct = default)
    {
        var delegates = await _users.ListEntityManagersAsync(ct);
        var result = new List<DelegateDto>(delegates.Count);
        foreach (var d in delegates)
            result.Add(await BuildDtoWithScopeAsync(d, ct));
        return result;
    }

    public async Task<DelegateDto> CreateAsync(CreateDelegateRequest request, string? actorName, CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Username);
        ValidateCredentials(username, request.Password, request.FullName);
        var (groupId, entryId) = await ResolveScopeAsync(request.PortalGroupId, request.PortalEntryId, ct);

        if (await _users.UsernameExistsAsync(username, branchId: null, excludeUserId: null, ct))
            throw new ArgumentException("يوجد مستخدم بنفس اسم الدخول، يرجى اختيار اسم مختلف");

        return await _tx.RunAsync(async token =>
        {
            var user = new User
            {
                Username = username,
                FullName = request.FullName.Trim(),
                Role = UserRole.EntityManager,
                BranchId = null,
                IsActive = true,
                PortalGroupId = groupId,
                PortalEntryId = entryId,
                PasswordHash = _hasher.Hash(request.Password),
            };
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_delegate",
                details: $"أضاف مندوب جهة: {username} ({DescribeScope(groupId, entryId)})", ct: token);
            return await BuildDtoWithScopeAsync(user, ct);
        }, ct);
    }

    public async Task<DelegateDto?> UpdateAsync(int delegateUserId, UpdateDelegateRequest request, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(delegateUserId, ct);
        if (user is null || user.Role != UserRole.EntityManager)
            return null;

        int? groupId = user.PortalGroupId;
        int? entryId = user.PortalEntryId;
        bool scopeChanged = request.PortalGroupId.HasValue || request.PortalEntryId.HasValue;
        if (scopeChanged)
        {
            // القيم المرسلة تحل محل النطاق كليًا (null يعني إزالة ذلك الطرف).
            groupId = request.PortalGroupId;
            entryId = request.PortalEntryId;
            (groupId, entryId) = await ResolveScopeAsync(groupId, entryId, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword!.Trim().Length < 6)
                throw new ArgumentException("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
            user.PasswordHash = _hasher.Hash(request.NewPassword.Trim());
            // إبطال الجلسات القائمة بعد تغيير كلمة المرور.
            user.TokenVersion++;
        }
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();
        if (request.IsActive.HasValue && user.IsActive != request.IsActive.Value)
        {
            user.IsActive = request.IsActive.Value;
            // إيقاف الحساب يبطل رموزه الصادرة سابقًا (مطابق لسلوك إدارة المستخدمين).
            if (!user.IsActive)
                user.TokenVersion++;
        }
        user.PortalGroupId = groupId;
        user.PortalEntryId = entryId;

        await _tx.RunAsync(async token =>
        {
            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "update_delegate",
                details: $"عدّل مندوب الجهة: {user.Username} ({DescribeScope(user.PortalGroupId, user.PortalEntryId)})", ct: token);
        }, ct);

        return await BuildDtoWithScopeAsync(user, ct);
    }

    // ── مساعدات خاصة ──

    /// <summary>يبني الـDTO مع أسماء النطاق، محمّلًا إياها عند غيابها عن الكيان المتتبَّع.</summary>
    private async Task<DelegateDto> BuildDtoWithScopeAsync(User user, CancellationToken ct)
    {
        var groupName = user.PortalGroup?.CanonicalName;
        var entryLabel = user.PortalEntry is null
            ? null
            : $"{user.PortalEntry.Group.CanonicalName} / {user.PortalEntry.BranchName}";

        if (user.PortalGroupId.HasValue && groupName is null)
        {
            var group = await _registry.GetGroupAsync(user.PortalGroupId.Value, ct);
            groupName = group?.CanonicalName;
        }
        if (user.PortalEntryId.HasValue && entryLabel is null)
        {
            var entry = await _registry.GetEntryWithDetailsAsync(user.PortalEntryId.Value, ct);
            entryLabel = entry is null ? null : $"{entry.Group.CanonicalName} / {entry.BranchName}";
        }

        return new DelegateDto(
            user.Id, user.Username, user.FullName, user.IsActive,
            user.PortalGroupId, groupName,
            user.PortalEntryId, entryLabel,
            user.CreatedAt);
    }

    private async Task<(int? GroupId, int? EntryId)> ResolveScopeAsync(int? groupId, int? entryId, CancellationToken ct)
    {
        if (groupId is null && entryId is null)
            throw new ArgumentException("حدّد نطاق المندوب: هوية أم قيدًا بعينه");
        if (groupId.HasValue && entryId.HasValue)
            throw new ArgumentException("حدّد نطاقًا واحدًا فقط: هوية أم قيدًا بعينه");

        if (entryId.HasValue)
        {
            var entry = await _registry.GetEntryAsync(entryId.Value, ct)
                ?? throw new ArgumentException("قيد الجهة غير موجود في السجل");
            return (null, entry.Id);
        }

        var group = await _registry.GetGroupAsync(groupId!.Value, ct)
            ?? throw new ArgumentException("هوية الجهة غير موجودة في السجل");
        return (group.Id, null);
    }

    private static void ValidateCredentials(string username, string password, string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("الاسم الكامل مطلوب");
        if (username.Length == 0)
            throw new ArgumentException("اسم الدخول مطلوب");
        if (username.Length > MaxUsernameLength)
            throw new ArgumentException($"اسم الدخول أطول من المسموح ({MaxUsernameLength} حرفاً)");
        if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 6)
            throw new ArgumentException("كلمة المرور يجب أن تكون 6 أحرف على الأقل");
    }

    private static string NormalizeUsername(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        return ArabicNameNormalizer.Normalize(normalized);
    }

    private static string DescribeScope(int? groupId, int? entryId)
        => entryId.HasValue ? $"قيد #{entryId}" : $"هوية #{groupId}";
}
