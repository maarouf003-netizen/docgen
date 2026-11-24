using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>إنشاء حساب مندوب جهة مربوط بنطاقه (هوية أو قيد واحد حصرًا).</summary>
public sealed record CreateEntityDelegateCommand(
    CreateDelegateRequest Request,
    string? ActorName,
    EntityRegistryActor? Actor = null) : ICommand<DelegateDto>;

internal sealed class CreateEntityDelegateCommandHandler
    : CommandHandlerBase<CreateEntityDelegateCommand, DelegateDto>
{
    private readonly IEntityDelegateWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public CreateEntityDelegateCommandHandler(
        IEntityDelegateWriteRules rules,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateEntityDelegateCommand, DelegateDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _hasher = hasher;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<DelegateDto> HandleCoreAsync(
        CreateEntityDelegateCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var username = _rules.NormalizeUsername(request.Username);
        // M-02: ما يُفحَص هو ما يُخزَّن — نقصّ قبل التحقق والتخزين معًا.
        var candidatePassword = request.Password?.Trim() ?? string.Empty;
        _rules.ValidateCredentials(username, candidatePassword, request.FullName);
        var (groupId, entryId) = await _rules.ResolveScopeAsync(request.PortalGroupId, request.PortalEntryId, ct);
        await _rules.EnsureHeadGovernorateAsync(command.Actor, groupId, entryId, ct);

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
                PasswordHash = _hasher.Hash(candidatePassword),
            };
            await _users.AddAsync(user, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "create_delegate",
                details: $"أضاف مندوب جهة: {username} ({DescribeScope(groupId, entryId)})", ct: token);
            return await _rules.BuildDtoWithScopeAsync(user, ct);
        }, ct);
    }

    private string DescribeScope(int? groupId, int? entryId) => _rules.DescribeScope(groupId, entryId);
}

/// <summary>تعديل حساب مندوب قائم (الاسم/التفعيل/كلمة المرور/نطاقه).</summary>
public sealed record UpdateEntityDelegateCommand(
    int DelegateUserId,
    UpdateDelegateRequest Request,
    string? ActorName,
    EntityRegistryActor? Actor = null) : ICommand<DelegateDto?>;

internal sealed class UpdateEntityDelegateCommandHandler
    : CommandHandlerBase<UpdateEntityDelegateCommand, DelegateDto?>
{
    private readonly IEntityDelegateWriteRules _rules;
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UpdateEntityDelegateCommandHandler(
        IEntityDelegateWriteRules rules,
        IUserRepository users,
        IPasswordHasher hasher,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateEntityDelegateCommand, DelegateDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _users = users;
        _hasher = hasher;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<DelegateDto?> HandleCoreAsync(
        UpdateEntityDelegateCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var user = await _users.GetByIdAsync(command.DelegateUserId, ct);
        if (user is null || user.Role != UserRole.EntityManager)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(user, request.Version, "حساب المندوب");

        // `BQ-002`: الرئيس لا يمس مندوبًا خارج محافظته — الحالي أولًا.
        await _rules.EnsureHeadGovernorateAsync(command.Actor, user.PortalGroupId, user.PortalEntryId, ct);

        int? groupId = user.PortalGroupId;
        int? entryId = user.PortalEntryId;
        bool scopeChanged = request.PortalGroupId.HasValue || request.PortalEntryId.HasValue;
        if (scopeChanged)
        {
            // القيم المرسلة تحل محل النطاق كليًا (null يعني إزالة ذلك الطرف).
            groupId = request.PortalGroupId;
            entryId = request.PortalEntryId;
            (groupId, entryId) = await _rules.ResolveScopeAsync(groupId, entryId, ct);
            // ... ولا ينقله خارجها.
            await _rules.EnsureHeadGovernorateAsync(command.Actor, groupId, entryId, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            // M-02: ما يُفحَص هو ما يُخزَّن — نقصّ قبل التحقق والتخزين معًا.
            var candidatePassword = request.NewPassword.Trim();
            PasswordPolicy.EnsureValid(candidatePassword, user.Username);
            user.PasswordHash = _hasher.Hash(candidatePassword);
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
            await _audit.LogAsync(command.ActorName, "update_delegate",
                details: $"عدّل مندوب الجهة: {user.Username} ({DescribeScope(user.PortalGroupId, user.PortalEntryId)})", ct: token);
        }, ct);

        return await _rules.BuildDtoWithScopeAsync(user, ct);
    }

    private string DescribeScope(int? groupId, int? entryId) => _rules.DescribeScope(groupId, entryId);
}
