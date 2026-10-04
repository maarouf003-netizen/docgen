using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IEntityDelegateService
{
    Task<List<DelegateDto>> ListAsync(CancellationToken ct = default, EntityRegistryActor? actor = null);

    /// <summary>إنشاء حساب مندوب مربوط بنطاقه — يجب تحديد هوية أو قيدًا واحدًا حصرًا (د11).</summary>
    Task<DelegateDto> CreateAsync(CreateDelegateRequest request, string? actorName, CancellationToken ct = default, EntityRegistryActor? actor = null);

    /// <summary>تعديل حساب مندوب قائم (الاسم/التفعيل/كلمة المرور/نطاقه) — null إن لم يوجد.</summary>
    Task<DelegateDto?> UpdateAsync(int delegateUserId, UpdateDelegateRequest request, string? actorName, CancellationToken ct = default, EntityRegistryActor? actor = null);
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
    private readonly IRepository<Branch>? _branches;

    public EntityDelegateService(
        IUserRepository users,
        IPublicEntityRepository registry,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IRepository<Branch>? branches = null)
    {
        _users = users;
        _registry = registry;
        _hasher = hasher;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        // مستودع الفروع لنطاق المحافظة اختياري التركيب (اختبارات قديمة بلا فاعل) —
        // الإنتاج يمرره دائمًا عبر `DI`؛ غيابه مع فاعل رئيس = بلا قيد (موثق).
        _branches = branches;
    }

    public async Task<List<DelegateDto>> ListAsync(CancellationToken ct = default, EntityRegistryActor? actor = null)
    {
        var delegates = await _users.ListEntityManagersAsync(ct);
        var governorate = await HeadGovernorateAsync(actor, ct);
        var result = new List<DelegateDto>(delegates.Count);
        foreach (var d in delegates)
        {
            if (governorate is not null && !await IsScopeInGovernorateAsync(d.PortalGroupId, d.PortalEntryId, governorate, ct))
                continue;
            result.Add(await BuildDtoWithScopeAsync(d, ct));
        }
        return result;
    }

    public async Task<DelegateDto> CreateAsync(CreateDelegateRequest request, string? actorName, CancellationToken ct = default, EntityRegistryActor? actor = null)
    {
        var username = NormalizeUsername(request.Username);
        ValidateCredentials(username, request.Password, request.FullName);
        var (groupId, entryId) = await ResolveScopeAsync(request.PortalGroupId, request.PortalEntryId, ct);
        await EnsureHeadGovernorateAsync(actor, groupId, entryId, ct);

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

    public async Task<DelegateDto?> UpdateAsync(int delegateUserId, UpdateDelegateRequest request, string? actorName, CancellationToken ct = default, EntityRegistryActor? actor = null)
    {
        var user = await _users.GetByIdAsync(delegateUserId, ct);
        if (user is null || user.Role != UserRole.EntityManager)
            return null;

        // `BQ-002`: الرئيس لا يمس مندوبًا خارج محافظته — الحالي أولًا.
        await EnsureHeadGovernorateAsync(actor, user.PortalGroupId, user.PortalEntryId, ct);

        int? groupId = user.PortalGroupId;
        int? entryId = user.PortalEntryId;
        bool scopeChanged = request.PortalGroupId.HasValue || request.PortalEntryId.HasValue;
        if (scopeChanged)
        {
            // القيم المرسلة تحل محل النطاق كليًا (null يعني إزالة ذلك الطرف).
            groupId = request.PortalGroupId;
            entryId = request.PortalEntryId;
            (groupId, entryId) = await ResolveScopeAsync(groupId, entryId, ct);
            // ... ولا ينقله خارجها.
            await EnsureHeadGovernorateAsync(actor, groupId, entryId, ct);
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

    /// <summary>
    /// محافظة فرع الفاعل إن كان رئيس قسم بمستودع فروع حاضر — وإلا null (بلا قيد).
    /// </summary>
    private async Task<string?> HeadGovernorateAsync(EntityRegistryActor? actor, CancellationToken ct)
    {
        if (actor?.Role != UserRole.Head || !actor.BranchId.HasValue || _branches is null)
            return null;
        var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var governorate = branch?.Governorate?.Trim();
        return string.IsNullOrEmpty(governorate) ? null : governorate;
    }

    /// <summary>
    /// نطاق المندوب داخل محافظة (`BQ-002`): القيد بنفس محافظته؛ الهوية بقيودها
    /// النشطة كلها داخلها (الفارغة تُقبَل — لا بيانات تُسرَّب). المخالفة `403`.
    /// </summary>
    private async Task EnsureHeadGovernorateAsync(
        EntityRegistryActor? actor, int? groupId, int? entryId, CancellationToken ct)
    {
        var governorate = await HeadGovernorateAsync(actor, ct);
        if (governorate is null)
            return;
        if (!await IsScopeInGovernorateAsync(groupId, entryId, governorate, ct))
            throw new UnauthorizedAccessException("نطاق المندوب خارج محافظة فرعك");
    }

    private async Task<bool> IsScopeInGovernorateAsync(
        int? groupId, int? entryId, string governorate, CancellationToken ct)
    {
        // `PB-001`: مقارنة معيارية للطرفين (كانت `Trim` فقط).
        var normGov = ArabicNameNormalizer.Normalize(governorate);
        if (entryId.HasValue)
        {
            var entry = await _registry.GetEntryAsync(entryId.Value, ct);
            return entry is not null
                && string.Equals(ArabicNameNormalizer.Normalize(entry.Governorate), normGov, StringComparison.Ordinal);
        }
        if (groupId.HasValue)
        {
            var entries = await _registry.ListEntriesByGroupAsync(groupId.Value, ct);
            var actives = entries.Where(e => e.IsActive).ToList();
            return actives.Count == 0
                || actives.All(e => string.Equals(ArabicNameNormalizer.Normalize(e.Governorate), normGov, StringComparison.Ordinal));
        }
        return false;
    }

    // ── مساعدات خاصة ──

    /// <summary>يبني الـDTO مع أسماء النطاق، محمّلًا إياها عند غيابها عن الكيان المتتبَّع.</summary>
    private async Task<DelegateDto> BuildDtoWithScopeAsync(User user, CancellationToken ct)
    {
        var groupName = user.PortalGroup?.CanonicalName;
        // قد يملأ EF navigation user.PortalEntry عبر fix-up بكيان مُتتبَّع حُمِّل
        // بلا Include(Group) (كما في ResolveScopeAsync عبر GetEntryAsync)،
        // فتكون Group null رغم أن PortalEntry نفسها غير null — لذا الفحص على Group.
        var entryLabel = user.PortalEntry?.Group is null
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
            entryLabel = entry?.Group is null ? null : $"{entry.Group.CanonicalName} / {entry.BranchName}";
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
            // شرط الظهور نفسه في البوابة (PortalRepository.ResolveForUserAsync):
            // نهائي + نشط + بلا مراجعة معلقة — وإلا قُبِل الربط وبقيت بوابة
            // المندوب فارغة بصمت. الرسائل على نسق PublicEntityService.
            if (entry.Status != EntityStatusCatalog.Final)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد بانتظار الاعتماد");
            if (entry.NeedsReview)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد بانتظار المراجعة؛ اعتمده أولًا");
            if (!entry.IsActive)
                throw new ArgumentException("لا يمكن ربط مندوب بقيد غير نشط");
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
