using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>
/// تسطير استئناف جديد — يملك حرّاس المصدر/الملكية، وبناء اللقطات والحقول، وتنبيه
/// مالك الدائرة عبر `Outbox`، ومعاملته، وتدقيق `create_appeal`.
/// </summary>
public sealed record CreateDocumentAppealCommand(
    int DocumentId,
    UpsertAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto>;

public sealed class CreateDocumentAppealCommandHandler
    : CommandHandlerBase<CreateDocumentAppealCommand, AppealDto>
{
    private readonly IDocumentRepository _documents;
    private readonly IAppealRepository _appeals;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CreateDocumentAppealCommandHandler(
        IDocumentRepository documents,
        IAppealRepository appeals,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CreateDocumentAppealCommand, AppealDto>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _appeals = appeals;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto> HandleCoreAsync(
        CreateDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var source = await _documents.GetByIdAsync(command.DocumentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        DocumentAppealRules.ValidateSourceForAppeal(source);
        // P2-02f: فشل الصلاحية = 403 لا 400 (نفس تصحيح مسارات الإنابة).
        if (source.CreatedById != command.UserId)
            throw new UnauthorizedAccessException("لا يمكنك تسطير استئناف على ملف لا تملكه");

        var direction = DocumentAppealService.ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = DocumentAppealService.BuildSnapshots(source, direction, request.Appellants);
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

        var appeal = new DocumentAppeal
        {
            DocumentId = source.Id,
            Direction = direction,
            Status = AppealStatusCatalog.Pending,
            AppellantsJson = appellantsJson,
            AppelleesJson = appelleesJson,
            ForwardState = AppealForwardCatalog.Owned,
            Version = 1,
            CreatedById = command.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        DocumentAppealService.ApplyUpsertFields(appeal, request, currentYear);

        await _tx.RunAsync(async token =>
        {
            await _appeals.AddAsync(appeal, token);
            await _uow.SaveChangesAsync(token);
            await EnqueueAppealPendingAsync(appeal, source, command.UserId, DateTime.UtcNow, currentYear, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "create_appeal",
                source.Id, source.DocumentType,
                $"تسطير الاستئناف ({AppealDirectionCatalog.ToLabel(direction)}) على الملف (رقم {source.Id})", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, currentYear);
    }

    /// <summary>
    /// الإشعار الأولي لمالك واحد حسب دائرة الملف (§6.1): رئيس شعبته، وإلا رئيس القسم —
    /// لا بث لفرع (قرار §2 + §17/5). منقول حرفيًا من `DocumentAppealService.Alerts.cs`.
    /// </summary>
    private async Task EnqueueAppealPendingAsync(
        DocumentAppeal appeal, Document source, int actorUserId, DateTime occurredAt,
        int currentYear, CancellationToken ct)
    {
        var branchId = source.BranchId;
        if (branchId is null)
            return;

        var sectionId = source.ExecutionCircuit?.SectionId;
        var owner = sectionId.HasValue
            ? await _users.FindActiveHeadAsync(UserRole.SubHead, branchId, sectionId, ct)
                ?? await _users.FindActiveHeadAsync(UserRole.Head, branchId, null, ct)
            : await _users.FindActiveHeadAsync(UserRole.Head, branchId, null, ct);
        if (owner is null)
            return;

        // نفس تعبير الخدمة: الرقم الفعلي للسنة الحالية أو «—».
        var appealNumber = DocumentAppealService.AppealDocumentNumberText(source, currentYear);
        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                Kind: HeadAlertKindCatalog.AppealPendingToOwner,
                Message: HeadAlertMessages.AppealPendingToOwner(new(
                    DocumentAppealService.DocumentTitle(source),
                    appealNumber,
                    source.FileType ?? "—",
                    DocumentAppealService.AppealCourtBare(source.Court))),
                Audience: HeadAlertAudience.SingleHead,
                BranchId: branchId.Value,
                CreatedById: actorUserId,
                DocumentId: source.Id,
                AppealId: appeal.Id,
                RecipientUserId: owner.Id),
            supersedePlan: null,
            occurredAt,
            ct);
    }
}

/// <summary>
/// تعديل استئناف قبل الإسناد — يملك الحرّاس وإعادة بناء اللقطات/الحقول ومعاملته
/// وتدقيق `update_appeal`.
/// </summary>
public sealed record UpdateDocumentAppealCommand(
    int AppealId,
    UpsertAppealRequest Request,
    int UserId,
    string? ActorName) : ICommand<AppealDto?>;

public sealed class UpdateDocumentAppealCommandHandler
    : CommandHandlerBase<UpdateDocumentAppealCommand, AppealDto?>
{
    private readonly IAppealRepository _appeals;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public UpdateDocumentAppealCommandHandler(
        IAppealRepository appeals,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<UpdateDocumentAppealCommand, AppealDto?>> behaviors)
        : base(behaviors)
    {
        _appeals = appeals;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<AppealDto?> HandleCoreAsync(
        UpdateDocumentAppealCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var appeal = await _appeals.GetByIdWithDetailsAsync(command.AppealId, ct)
            ?? throw new KeyNotFoundException("الاستئناف غير موجود");

        DocumentAppealRules.EnsurePendingByCreator(appeal, command.UserId);
        var source = appeal.Document;

        var direction = DocumentAppealService.ParseDirection(request.Direction);
        var (appellantsJson, appelleesJson) = DocumentAppealService.BuildSnapshots(source, direction, request.Appellants);

        appeal.Direction = direction;
        appeal.AppellantsJson = appellantsJson;
        appeal.AppelleesJson = appelleesJson;
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        DocumentAppealService.ApplyUpsertFields(appeal, request, currentYear);
        appeal.UpdatedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            _appeals.Update(appeal);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(command.ActorName, "update_appeal",
                source.Id, source.DocumentType,
                $"عدّل الاستئناف (رقم {appeal.Id}) على الملف (رقم {source.Id})", token);
        }, ct);

        return DocumentAppealService.ToDto(appeal, currentYear);
    }
}
