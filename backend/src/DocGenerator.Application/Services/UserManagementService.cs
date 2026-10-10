using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IUserManagementService
{
    Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default);
    Task<List<LawyerListItemDto>> ListLawyersAsync(
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default);
    Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null);
    Task<LawyerListItemDto?> UpdateLawyerAsync(int userId, UpdateLawyerRequest request, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<bool> SetLawyerActiveAsync(int userId, bool isActive, int? scopeBranchId, string? actorName, CancellationToken ct = default);
    Task<List<UserListItemDto>> ListUsersAsync(CancellationToken ct = default);
    Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null);
    Task<UserListItemDto?> UpdateUserAsync(int userId, UpdateUserRequest request, int actorUserId, string? actorName, CancellationToken ct = default);
    Task<List<HeadSuccessionDto>> ListSuccessionAsync(int? branchId, CancellationToken ct = default);
}

/// <summary>
/// إدارة محامي الفرع (رئيس القسم/مشرف) وإدارة المستخدمين الكاملة (مشرف).
/// التحقق من الصلاحية/النطاق في المتحكم، والتحقق المنطقي والكتابة هنا.
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private const int MinPasswordLength = 6;

    private static readonly UserRole[] BranchRoles = { UserRole.Lawyer, UserRole.Head, UserRole.SubHead };

    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IRepository<Correspondence> _correspondences;
    private readonly IRepository<CorrespondenceMessage> _messages;
    private readonly IRepository<HeadAlert> _alerts;
    private readonly IRepository<HeadAlertRecipient> _recipients;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UserManagementService(
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IRepository<ExecutionCircuit> circuits,
        IRepository<HeadSuccession> successions,
        IRepository<Correspondence> correspondences,
        IRepository<CorrespondenceMessage> messages,
        IRepository<HeadAlert> alerts,
        IRepository<HeadAlertRecipient> recipients,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _users = users;
        _branches = branches;
        _sections = sections;
        _circuits = circuits;
        _successions = successions;
        _correspondences = correspondences;
        _messages = messages;
        _alerts = alerts;
        _recipients = recipients;
        _documents = documents;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    public async Task<List<LawyerListItemDto>> ListLawyersAsync(int? branchId, CancellationToken ct = default)
        => await ListLawyersAsync(branchId, "branch", 0, null, false, true, ct);

    /// <summary>
    /// قائمتا المحامين (§5.4 — قرار §2.9): `branch` (الافتراضي للنوافذ: كل محامي
    /// الفرع — تسريب الأسماء موثق ومقبول) و`mine` (الافتراضي: له ملف في دوائري
    /// أو أنا أنشأته أو قديم بلا منشئ — القدامى يُرون دائمًا). المدير/المشرف
    /// خارج القاعدتين (بلا دوائر): قائمة الفرع الكاملة. `includeInactive`
    /// افتراضيًا صحيح حفظًا للسلوك القائم (إعادة التفعيل تحتاجه) — يُعلن هنا.
    /// </summary>
    public async Task<List<LawyerListItemDto>> ListLawyersAsync(
        int? branchId,
        string? mode,
        int callerUserId,
        int? callerSectionId,
        bool callerIsHeadOrSubHead,
        bool includeInactive,
        CancellationToken ct = default)
    {
        var normalizedMode = (mode ?? "mine").Trim().ToLowerInvariant();
        if (normalizedMode is not ("mine" or "branch"))
            throw new ArgumentException("وضع غير صالح — mine أو branch");

        if (!callerIsHeadOrSubHead || normalizedMode == "branch")
            return await BranchLawyerListAsync(branchId, includeInactive, ct);

        var circuits = await _circuits.ListAsync(ct);
        var myCircuitIds = circuits
            .Where(c => branchId == null || c.BranchId == branchId)
            .Where(c => c.SectionId == callerSectionId)
            .Select(c => c.Id)
            .ToList();
        var ownerIds = await _documents.ListOwnerIdsByCircuitIdsAsync(myCircuitIds, ct);
        var inBranch = await BranchLawyerListAsync(branchId, true, ct);
        // القدامى بلا منشئ ضمن نطاق الفرع (قرار §2.10 — والنطاق الفرعي للendpoint
        // ثابت: `Head_ListsLawyers_OnlyOwnBranch`).
        var veterans = inBranch.Where(l => l.CreatedById == null);
        return inBranch
            .Concat(veterans)
            .GroupBy(l => l.Id)
            .Select(g => g.First())
            .Where(l => includeInactive || l.IsActive)
            .Where(l => ownerIds.Contains(l.Id) || l.CreatedById == callerUserId || l.CreatedById == null)
            .OrderBy(l => l.FullName)
            .ToList();
    }

    private async Task<List<LawyerListItemDto>> BranchLawyerListAsync(int? branchId, bool includeInactive, CancellationToken ct)
    {
        var lawyers = await _users.ListLawyersAsync(branchId, ct);
        return lawyers
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.FullName)
            .Select(ToLawyerDto)
            .ToList();
    }

    public async Task<LawyerListItemDto> CreateLawyerAsync(int branchId, CreateLawyerRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null)
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
            // منشئ المحامي (قرار §2.10): مباشَرة محامي الصفر ملفات — القدامى `null`.
            CreatedById = actorUserId,
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

            // (R2) اسم الدخول جزء من هوية التوكن (Name/UniqueName): تغيّره يُبطل الجلسات كالدور/الفرع.
            if (newUsername != user.Username)
                user.TokenVersion++;
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

    public async Task<UserListItemDto> CreateUserAsync(CreateUserRequest request, string? actorName, CancellationToken ct = default, int? actorUserId = null)
    {
        var username = NormalizeUsername(request.Username);
        ValidateUsername(username);
        ValidatePassword(request.Password);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        var role = ParseRole(request.Role);
        await GuardManagerAdminBoundaryAsync(actorUserId, requestedIsAdmin: role == UserRole.Admin, targetIsAdmin: false, ct);
        var branchId = await ResolveBranchAsync(request.BranchId, role, ct);
        // شعبة الحساب (§9.1): إلزامية ومحققة لرئيس الشعبة، ومرفوضة لغيره.
        var sectionId = await ResolveSectionAsync(role, branchId, request.SectionId, ct);
        await GuardSingleActiveHeadAsync(role, branchId, sectionId, excludeUserId: null, ct);

        if (await _users.UsernameExistsAsync(username, branchId, null, ct))
            throw new ArgumentException(DuplicateUsernameMessage(branchId));

        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Role = role,
            BranchId = branchId,
            SectionId = sectionId,
            // منشئ المحامي فقط (§2.10) — الرؤساء يُتبعون بسجل التعاقب.
            CreatedById = role == UserRole.Lawyer ? actorUserId : null,
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
            // سجل التعاقب عند التعيين (§9.5).
            if (role is UserRole.Head or UserRole.SubHead)
            {
                await _successions.AddAsync(new HeadSuccession
                {
                    BranchId = branchId!.Value,
                    SectionId = sectionId,
                    UserId = user.Id,
                    Role = role,
                    Event = HeadSuccessionEventCatalog.Appointed,
                    At = DateTime.UtcNow,
                    ActorName = actorName,
                }, token);
                await _uow.SaveChangesAsync(token);
            }
        }, ct);

        return await ToUserDtoAsync(user, ct);
    }

    public async Task<UserListItemDto?> UpdateUserAsync(int userId, UpdateUserRequest request, int actorUserId, string? actorName, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null)
            return null;

        var role = request.Role is null ? user.Role : ParseRole(request.Role);
        await GuardManagerAdminBoundaryAsync(actorUserId, requestedIsAdmin: role == UserRole.Admin, targetIsAdmin: user.Role == UserRole.Admin, ct);
        var branchId = await ResolveBranchAsync(request.BranchId ?? user.BranchId, role, ct);
        // الشعبة عند التحديث: تُحفظ الحالية عند الغياب، وتُنقَّل صراحةً (نقل
        // الرئيس بين الشعب)، وتُصفَّر تلقائيًا عند مغادرة دور الشعبة.
        int? sectionId;
        if (role != UserRole.SubHead)
        {
            if (request.SectionId.HasValue)
                throw new ArgumentException("الشعبة مخصصة لرئيس الشعبة فقط");
            sectionId = null;
        }
        else
        {
            sectionId = request.SectionId ?? user.SectionId;
            sectionId = await ResolveSectionAsync(role, branchId, sectionId, ct);
        }
        await GuardSingleActiveHeadAsync(role, branchId, sectionId, user.Id, ct);

        // التعطيل بخلف إجباري (قرار §2.18): تعطيل رئيس (قسم/شعبة) يتطلب خلفًا
        // يحل محله بكل شيء — في نفس المعاملة. لغير الرؤساء: الخلف مرفوض.
        var deactivating = user.IsActive && !request.IsActive;
        User? successor = null;
        if (deactivating && role is UserRole.Head or UserRole.SubHead)
        {
            if (request.SuccessorId is null)
                throw new ArgumentException("التعطيل يتطلب خلفًا إجباريًا — حدد الخلف");
            successor = await ResolveSuccessorAsync(user, request.SuccessorId.Value, ct);
        }
        else if (request.SuccessorId.HasValue)
        {
            throw new ArgumentException("تعيين الخلف مخصص لحسابات الرئاسة");
        }

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

        var usernameChanged = false;
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = ArabicNameNormalizer.Normalize(request.FullName.Trim());
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, branchId, user.Id, ct))
                throw new ArgumentException(DuplicateUsernameMessage(branchId));

            // (R2) اسم الدخول جزء من هوية التوكن (Name/UniqueName): تغيّره يُبطل الجلسات كالدور/الفرع.
            if (newUsername != user.Username)
                usernameChanged = true;
            user.FullName = request.FullName.Trim();
            user.Username = newUsername;
        }
        // إبطال أمني (S1): تغيير الدور أو الفرع أو الشعبة يُسقط التوكنات الصادرة
        // سابقًا، وإلا بقيت صلاحيات النطاق القديم صالحة حتى انتهاء التوكن
        // (الـ claims تُقرأ من التوكن حصرًا).
        var roleOrBranchChanged = user.Role != role || user.BranchId != branchId || user.SectionId != sectionId;
        if (roleOrBranchChanged || usernameChanged)
            user.TokenVersion++;
        user.Role = role;
        user.BranchId = branchId;
        user.SectionId = sectionId;
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
            if (successor is not null)
                await ApplySuccessionAsync(user, successor, actorName, token);
            await _audit.LogAsync(actorName, "update_user",
                details: $"عدّل المستخدم: {user.FullName} ({user.Username})"
                    + (roleOrBranchChanged || usernameChanged ? " — أُبطلت الجلسات السابقة لتغيّر الدور/الفرع/اسم الدخول" : "")
                    + (successor is not null ? $" — خلفه {successor.FullName} ({successor.Username})" : ""), ct: token);
            return await ToUserDtoAsync(user, token);
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

    /// <summary>
    /// شعبة الحساب (§9.1 + §4.3): إلزامية لرئيس الشعبة (وجودًا وتطابق فرع)،
    /// ومرفوضة لغيره. تُستدعى بعد حل الفرع لأن التطابق عليه.
    /// </summary>
    private async Task<int?> ResolveSectionAsync(UserRole role, int? branchId, int? sectionId, CancellationToken ct)
    {
        if (role != UserRole.SubHead)
        {
            if (sectionId.HasValue)
                throw new ArgumentException("الشعبة مخصصة لرئيس الشعبة فقط");
            return null;
        }
        if (sectionId is null)
            throw new ArgumentException("الشعبة إلزامية لرئيس الشعبة — اختر شعبة الفرع");
        var section = await _sections.GetByIdAsync(sectionId.Value, ct);
        if (section is null)
            throw new ArgumentException("الشعبة غير موجودة");
        if (section.BranchId != branchId)
            throw new ArgumentException("الشعبة ليست ضمن فرع الحساب");
        return section.Id;
    }

    private static UserRole ParseRole(string? role)
    {
        if (!Enum.TryParse<UserRole>(role?.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException("دور غير صالح");
        return parsed;
    }

    /// <summary>
    /// وحدانية الرئاسة المفعّلة (قرار §2.3/§2.26 + §9.2): رئيس قسم واحد مفعّل لكل
    /// فرع، ورئيس شعبة مفعّل واحد لكل شعبة — بالرسائل المعتمدة (§14). القيد الجزئي
    /// الفريد في القاعدة ظهرًا للكتابة المباشرة؛ هذا الحارس يعطي `400` نظيفًا بدل `500`.
    /// </summary>
    private async Task GuardSingleActiveHeadAsync(UserRole role, int? branchId, int? sectionId, int? excludeUserId, CancellationToken ct)
    {
        if (role is not (UserRole.Head or UserRole.SubHead))
            return;
        if (await _users.ExistsActiveHeadAsync(role, branchId, sectionId, excludeUserId, ct))
            throw new ArgumentException(role == UserRole.Head ? "الفرع لا يقبل رئيسي قسم" : "الشعبة مشغولة برئيس مفعّل");
    }

    /// <summary>
    /// التحقق من الخلف (§9.3): موجود ومفعّل وفي نفس الفرع وغير الذات، ودوره
    /// قابل للرئاسة (محامٍ يُرقَّى أو رئيس — لا مشرف/مدير/مندوب).
    /// </summary>
    private async Task<User> ResolveSuccessorAsync(User user, int successorId, CancellationToken ct)
    {
        var successor = await _users.GetByIdAsync(successorId, ct)
            ?? throw new ArgumentException("الخلف غير موجود");
        if (successor.Id == user.Id)
            throw new ArgumentException("الخلف لا يمكن أن يكون الحساب نفسه");
        if (!successor.IsActive)
            throw new ArgumentException("الخلف يجب أن يكون حسابًا مفعّلًا");
        if (successor.BranchId != user.BranchId)
            throw new ArgumentException("الخلف يجب أن يكون في نفس الفرع");
        if (successor.Role is UserRole.Admin or UserRole.Manager or UserRole.EntityManager)
            throw new ArgumentException("الخلف يجب أن يكون محاميًا أو رئيسًا");
        return successor;
    }

    /// <summary>
    /// تطبيق الإحلال (قرار §2.18) — ضمن معاملة التعطيل نفسها: الخلف يحل محل
    /// السلف بكل شيء (الدور والشعبة)، مع نقل محامي الصفر ملفات والتنبيهات
    /// المعلقة والكتب المعلقة، وإبطال الجلسات، وصفي سجل تعاقب.
    /// </summary>
    private async Task ApplySuccessionAsync(User predecessor, User successor, string? actorName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        successor.Role = predecessor.Role;
        successor.SectionId = predecessor.SectionId;
        successor.TokenVersion++;
        successor.UpdatedAt = now;
        _users.Update(successor);

        // محامو الصفر ملفات الذين أنشأهم السلف يتبعون الخلف (§2.10).
        // يُعاد الجلب لا التحديث المباشر لنتائج `AsNoTracking`: نسخة واحدة
        // متتبَّعة لكل مفتاح بلا تعارض — والخلف نفسه مستثنى (ما زال محاميًا في
        // القاعدة لحظة الاستعلام، وإسناده لذاته حلقة مرفوضة).
        var createdIds = (await _users.ListByCreatorAsync(predecessor.Id, ct))
            .Select(u => u.Id)
            .ToList();
        foreach (var lawyerId in createdIds)
        {
            if (lawyerId == successor.Id)
                continue;
            var lawyer = await _users.GetByIdAsync(lawyerId, ct);
            if (lawyer is null || lawyer.Role != UserRole.Lawyer)
                continue;
            if ((await _documents.ListByOwnerAsync(lawyer.Id, ct)).Count > 0)
                continue;
            lawyer.CreatedById = successor.Id;
            lawyer.UpdatedAt = now;
            _users.Update(lawyer);
        }

        // التنبيهات المعلقة: مستلمو السلف غير المقروءة → الخلف (المقروءة تاريخ
        // مكتمل لا يُرحَّل). `TargetLawyerId` مؤشر عمل مفتوح يُنقَل كما هو.
        var recipients = await _recipients.ListAsync(ct);
        foreach (var r in recipients.Where(r => r.UserId == predecessor.Id && !r.IsRead))
        {
            r.UserId = successor.Id;
            _recipients.Update(r);
        }
        var alerts = await _alerts.ListAsync(ct);
        foreach (var a in alerts.Where(a => a.TargetLawyerId == predecessor.Id))
        {
            a.TargetLawyerId = successor.Id;
            _alerts.Update(a);
        }

        // الكتب المعلقة (غير المجابة — بلا رد من المستلم) تُنقَل للخلف (§2.24).
        var messages = await _messages.ListAsync(ct);
        var repliedByPredecessor = messages
            .Where(m => m.Kind == CorrespondenceMessage.KindReply && m.AuthorId == predecessor.Id)
            .Select(m => m.CorrespondenceId)
            .ToHashSet();
        var letters = await _correspondences.ListAsync(ct);
        foreach (var c in letters.Where(c => c.TargetUserId == predecessor.Id && !repliedByPredecessor.Contains(c.Id)))
        {
            c.TargetUserId = successor.Id;
            c.RecipientSectionId = successor.SectionId;
            c.UpdatedAt = now;
            _correspondences.Update(c);
        }

        await _uow.SaveChangesAsync(ct);
        var successionAt = DateTime.UtcNow;
        await _successions.AddAsync(new HeadSuccession
        {
            BranchId = predecessor.BranchId!.Value,
            SectionId = predecessor.SectionId,
            UserId = predecessor.Id,
            Role = predecessor.Role,
            Event = HeadSuccessionEventCatalog.Deactivated,
            At = successionAt,
            ActorName = actorName,
            Reason = $"عُطّل وحل محله {successor.FullName} ({successor.Username})",
        }, ct);
        await _successions.AddAsync(new HeadSuccession
        {
            BranchId = successor.BranchId!.Value,
            SectionId = successor.SectionId,
            UserId = successor.Id,
            Role = successor.Role,
            Event = HeadSuccessionEventCatalog.Succeeded,
            At = successionAt,
            ActorName = actorName,
            Reason = $"خلفًا لـ {predecessor.FullName} ({predecessor.Username})",
        }, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<List<HeadSuccessionDto>> ListSuccessionAsync(int? branchId, CancellationToken ct = default)
    {
        var rows = await _successions.ListAsync(ct);
        var branches = await _branches.ListAsync(ct);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        var sections = await _sections.ListAsync(ct);
        var sectionNames = sections.ToDictionary(s => s.Id, s => s.Name);
        var users = await _users.ListAllUsersAsync(ct);
        var userNames = users.ToDictionary(u => u.Id, u => u.FullName);
        return rows
            .Where(h => branchId == null || h.BranchId == branchId.Value)
            .OrderByDescending(h => h.At)
            .ThenByDescending(h => h.Id)
            .Select(h => new HeadSuccessionDto(
                h.Id,
                h.BranchId,
                branchNames.GetValueOrDefault(h.BranchId),
                h.SectionId,
                h.SectionId.HasValue ? sectionNames.GetValueOrDefault(h.SectionId.Value) : null,
                h.UserId,
                userNames.GetValueOrDefault(h.UserId),
                h.Role.ToString().ToLowerInvariant(),
                h.Event,
                h.At,
                h.ActorName,
                h.Reason))
            .ToList();
    }

    /// <summary>
    /// حد المشرف (`BQ-001د`): المدير يدير كل الأدوار عدا المشرف — إنشاء حساب
    /// مشرف أو المساس بحساب مشرف (تعديل/إيقاف) مرفوض `403`. الفاعل الغائب
    /// (مسارات قديمة/اختبارات بلا فاعل) يتجاوز الفحص مؤقتًا — كل نقاط الإنتاج
    /// تمرر الفاعل.
    /// </summary>
    private async Task GuardManagerAdminBoundaryAsync(
        int? actorUserId, bool requestedIsAdmin, bool targetIsAdmin, CancellationToken ct)
    {
        if (actorUserId is null)
            return;
        var actor = await _users.GetByIdAsync(actorUserId.Value, ct);
        if (actor?.Role == UserRole.Manager && (requestedIsAdmin || targetIsAdmin))
            throw new UnauthorizedAccessException("حسابات المشرف يديرها مشرف فقط");
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
        user.Branch?.Name,
        user.CreatedById);

    private static UserListItemDto ToUserDto(User user) => new(
        user.Id,
        user.Username,
        user.FullName,
        user.Role.ToString().ToLowerInvariant(),
        user.BranchId,
        user.Branch?.Name,
        user.IsActive,
        user.SectionId,
        user.Section?.Name);

    /// <summary>
    /// نسخة التحرير (إنشاء/تحديث): الكيان المتتبَّع بلا `Include` للشعبة —
    /// يُحسم الاسم باستعلام صريح عند الحاجة (لا يُفترض تحميله).
    /// </summary>
    private async Task<UserListItemDto> ToUserDtoAsync(User user, CancellationToken ct)
    {
        var sectionName = user.Section?.Name;
        if (sectionName is null && user.SectionId.HasValue)
            sectionName = (await _sections.GetByIdAsync(user.SectionId.Value, ct))?.Name;
        return new UserListItemDto(
            user.Id,
            user.Username,
            user.FullName,
            user.Role.ToString().ToLowerInvariant(),
            user.BranchId,
            user.Branch?.Name,
            user.IsActive,
            user.SectionId,
            sectionName);
    }
}
