using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Users;

/// <summary>إنشاء حساب محامٍ في فرع — بتفرد اسم الدخول وتدقيق داخل معاملة واحدة.</summary>
public sealed record CreateLawyerCommand(
    int BranchId,
    CreateLawyerRequest Request,
    string? ActorName,
    int? ActorUserId = null) : ICommand<LawyerListItemDto>;

internal sealed class CreateLawyerCommandHandler
    : CommandHandlerBase<CreateLawyerCommand, LawyerListItemDto>
{
    private readonly IUserManagementLawyerWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public CreateLawyerCommandHandler(
        IUserManagementLawyerWriteRules rules,
        IUserRepository users,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateLawyerCommand, LawyerListItemDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _branches = branches;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<LawyerListItemDto> HandleCoreAsync(
        CreateLawyerCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var username = _rules.NormalizeUsername(request.Username);
        _rules.ValidateUsername(username);
        var candidatePassword = _rules.NormalizePassword(request.Password, username);
        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new ArgumentException("الاسم الكامل مطلوب");

        if (await _users.UsernameExistsAsync(username, command.BranchId, null, ct))
            throw new ArgumentException(_rules.DuplicateUsernameMessage(command.BranchId));

        var branch = await _branches.GetByIdAsync(command.BranchId, ct);
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
            CreatedById = command.ActorUserId,
            PasswordHash = _hasher.Hash(candidatePassword),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "create_user", details: $"أنشأ محامياً: {user.FullName} ({user.Username}) في فرع {branch.Name}", ct: token);
        }, ct);

        return new LawyerListItemDto(user.Id, user.Username, user.FullName, user.IsActive, user.BranchId, branch.Name, null, user.Version);
    }
}

/// <summary>تعديل حساب محامٍ (الاسم/كلمة المرور) — ضمن نطاق الفرع، ولرئيس الشعبة ضمن شعبته.</summary>
public sealed record UpdateLawyerCommand(
    int UserId,
    UpdateLawyerRequest Request,
    int? ScopeBranchId,
    string? ActorName,
    int? ScopeSectionId = null,
    int? ActorUserId = null) : ICommand<LawyerListItemDto?>;

internal sealed class UpdateLawyerCommandHandler
    : CommandHandlerBase<UpdateLawyerCommand, LawyerListItemDto?>
{
    private readonly IUserManagementLawyerWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UpdateLawyerCommandHandler(
        IUserManagementLawyerWriteRules rules,
        IUserRepository users,
        IUnitOfWork uow,
        IPasswordHasher hasher,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateLawyerCommand, LawyerListItemDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _uow = uow;
        _hasher = hasher;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<LawyerListItemDto?> HandleCoreAsync(
        UpdateLawyerCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var user = await _users.GetByIdAsync(command.UserId, ct);
        if (user is null || user.Role != UserRole.Lawyer)
            return null;

        // رئيس القسم يعدّل محامي فرعه فقط.
        if (command.ScopeBranchId.HasValue && user.BranchId != command.ScopeBranchId)
            return null;

        // W2/B3: رئيس الشعبة يعدّل محامي شعبته فقط. غياب الفاعل مع نطاق شعبة يُرفض
        // (فشل مغلق) لا يُتجاوز، حتى لا يعود النطاق اختياريًا بسهولة.
        if (command.ScopeSectionId.HasValue)
        {
            if (command.ActorUserId is null)
                return null;
            var inSectionScope = await _rules.IsLawyerInSectionScopeAsync(
                user, command.ActorUserId.Value, command.ScopeBranchId, command.ScopeSectionId, ct);
            if (!inSectionScope)
                return null;
        }

        OptimisticConcurrencyGuard.EnsureCurrent(user, request.Version, "حساب المحامي");

        if (string.IsNullOrWhiteSpace(request.FullName) && string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("لا يوجد تغيير لإجرائه — حدّد اسماً جديداً أو كلمة مرور جديدة");

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            // الاسم الثلاثي هو اسم الدخول: تعديل الاسم يحدّث اسم الدخول تلقائياً مع بقاء التفرد ضمن الفرع.
            var newUsername = _rules.NormalizeUsername(request.FullName);
            _rules.ValidateUsername(newUsername);
            if (newUsername != user.Username
                && await _users.UsernameExistsAsync(newUsername, user.BranchId, user.Id, ct))
                throw new ArgumentException(_rules.DuplicateUsernameMessage(user.BranchId));

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
                var candidatePassword = _rules.NormalizePassword(request.Password, user.Username);
                user.PasswordHash = _hasher.Hash(candidatePassword);
                // إبطال الرموز الصادرة سابقاً عند تغيير كلمة المرور.
                user.TokenVersion++;
            }

            _users.Update(user);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "update_user",
                details: $"عدّل المحامي: {user.FullName} ({user.Username})", ct: token);
            return _rules.ToLawyerDto(user);
        }, ct);
    }
}
