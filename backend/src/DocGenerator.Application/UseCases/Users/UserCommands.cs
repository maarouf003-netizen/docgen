using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>إنشاء مستخدم إداري بدوره ونطاقه (فرع/شعبة) مع سجل التعاقب للرؤساء.</summary>
public sealed record CreateUserCommand(
    CreateUserRequest Request,
    string? ActorName,
    int? ActorUserId = null) : ICommand<UserListItemDto>;

internal sealed class CreateUserCommandHandler
    : CommandHandlerBase<CreateUserCommand, UserListItemDto>
{
    private readonly IUserManagementUserWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public CreateUserCommandHandler(
        IUserManagementUserWriteRules rules,
        IUserRepository users,
        IRepository<HeadSuccession> successions,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateUserCommand, UserListItemDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _successions = successions;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<UserListItemDto> HandleCoreAsync(
        CreateUserCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var username = _rules.NormalizeUsername(request.Username);
        _rules.ValidateUsername(username);
        var candidatePassword = _rules.NormalizePassword(request.Password, username);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        var role = _rules.ParseRole(request.Role);
        await _rules.GuardManagerAdminBoundaryAsync(command.ActorUserId, requestedIsAdmin: role == UserRole.Admin, targetIsAdmin: false, ct);
        var branchId = await _rules.ResolveBranchAsync(request.BranchId, role, ct);
        // شعبة الحساب (§9.1): إلزامية ومحققة لرئيس الشعبة، ومرفوضة لغيره.
        var sectionId = await _rules.ResolveSectionAsync(role, branchId, request.SectionId, ct);
        await _rules.GuardSingleActiveHeadAsync(role, branchId, sectionId, excludeUserId: null, ct);

        if (await _users.UsernameExistsAsync(username, branchId, null, ct))
            throw new ArgumentException(_rules.DuplicateUsernameMessage(branchId));

        var user = new User
        {
            Username = username,
            FullName = request.FullName.Trim(),
            Role = role,
            BranchId = branchId,
            SectionId = sectionId,
            // منشئ المحامي فقط (§2.10) — الرؤساء يُتبعون بسجل التعاقب.
            CreatedById = role == UserRole.Lawyer ? command.ActorUserId : null,
            IsActive = true,
            PasswordHash = _hasher.Hash(candidatePassword),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "create_user",
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
                    ActorName = command.ActorName,
                }, token);
                await _uow.SaveChangesAsync(token);
            }
        }, ct);

        return await _rules.ToUserDtoAsync(user, ct);
    }
}

/// <summary>تحديث مستخدم إداري (الدور/الفرع/الشعبة/الاسم/المرور) مع الخلف الإجباري.</summary>
public sealed record UpdateUserCommand(
    int UserId,
    UpdateUserRequest Request,
    int ActorUserId,
    string? ActorName) : ICommand<UserListItemDto?>;

internal sealed class UpdateUserCommandHandler
    : CommandHandlerBase<UpdateUserCommand, UserListItemDto?>
{
    private readonly IUserManagementUserWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UpdateUserCommandHandler(
        IUserManagementUserWriteRules rules,
        IUserRepository users,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateUserCommand, UserListItemDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<UserListItemDto?> HandleCoreAsync(
        UpdateUserCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var user = await _users.GetByIdAsync(command.UserId, ct);
        if (user is null)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(user, request.Version, "حساب المستخدم");

        var role = request.Role is null ? user.Role : _rules.ParseRole(request.Role);
        await _rules.GuardManagerAdminBoundaryAsync(command.ActorUserId, requestedIsAdmin: role == UserRole.Admin, targetIsAdmin: user.Role == UserRole.Admin, ct);
        var branchId = await _rules.ResolveBranchAsync(request.BranchId ?? user.BranchId, role, ct);
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
            sectionId = await _rules.ResolveSectionAsync(role, branchId, sectionId, ct);
        }
        await _rules.GuardSingleActiveHeadAsync(role, branchId, sectionId, user.Id, ct);

        // التعطيل بخلف إجباري (قرار §2.18): تعطيل رئيس (قسم/شعبة) يتطلب خلفًا
        // يحل محله بكل شيء — في نفس المعاملة. لغير الرؤساء: الخلف مرفوض.
        var deactivating = user.IsActive && !request.IsActive;
        User? successor = null;
        if (deactivating && role is UserRole.Head or UserRole.SubHead)
        {
            if (request.SuccessorId is null)
                throw new ArgumentException("التعطيل يتطلب خلفًا إجباريًا — حدد الخلف");
            successor = await _rules.ResolveSuccessorAsync(user, request.SuccessorId.Value, ct);
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
        if (command.UserId == command.ActorUserId && (!request.IsActive || role != UserRole.Admin))
            throw new ArgumentException("لا يمكنك إيقاف حسابك أو تغيير دورك أنت بنفسك");

        var usernameChanged = false;
        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = ArabicNameNormalizer.Normalize(request.FullName.Trim());
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, branchId, user.Id, ct))
                throw new ArgumentException(_rules.DuplicateUsernameMessage(branchId));

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
                var candidatePassword = _rules.NormalizePassword(request.Password, user.Username);
                user.PasswordHash = _hasher.Hash(candidatePassword);
                user.TokenVersion++;
            }

            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            if (successor is not null)
                await _rules.ApplySuccessionAsync(user, successor, command.ActorName, token);
            await _audit.LogAsync(command.ActorName, "update_user",
                details: $"عدّل المستخدم: {user.FullName} ({user.Username})"
                    + (roleOrBranchChanged || usernameChanged ? " — أُبطلت الجلسات السابقة لتغيّر الدور/الفرع/اسم الدخول" : "")
                    + (successor is not null ? $" — خلفه {successor.FullName} ({successor.Username})" : ""), ct: token);
            return await _rules.ToUserDtoAsync(user, token);
        }, ct);
    }
}
