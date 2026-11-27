using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// تسطير إنابة جديدة — يملك حرّاس المصدر والملكية، والتحقق/بناء الحقول، وحجوزات
/// الأصول، واستهداف اعتماد التنبيه، ومعاملته، وتدقيق `create_delegation`.
/// </summary>
public sealed record CreateDocumentDelegationCommand(
    int SourceDocumentId,
    UpsertDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto>;

public sealed class CreateDocumentDelegationCommandHandler
    : CommandHandlerBase<CreateDocumentDelegationCommand, DelegationDto>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDelegationReservationRepository _reservations;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public CreateDocumentDelegationCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDelegationReservationRepository reservations,
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<CreateDocumentDelegationCommand, DelegationDto>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _reservations = reservations;
        _users = users;
        _branches = branches;
        _circuits = circuits;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto> HandleCoreAsync(
        CreateDocumentDelegationCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var source = await _documents.GetByIdAsync(command.SourceDocumentId, ct)
            ?? throw new KeyNotFoundException("الملف المنيب غير موجود");

        DocumentDelegationService.ValidateSourceForDelegation(source);
        // P2-02e: فشل الصلاحية يجب أن يظهر 403 لا 400 — الرسالة تبقى كما هي للعميل.
        if (source.CreatedById != command.UserId)
            throw new UnauthorizedAccessException("لا يمكنك تسطير إنابة على ملف لا تملكه");
        if (source.NeedsRegistration)
            throw new ArgumentException("لا يمكن تسطير إنابة على ملف بانتظار إعادة القيد — أدخل رقمه الجديد أولًا");

        var (court, circuitId, fields) = await DocumentDelegationRules.ValidateAndBuildAsync(
            request, source.BranchId, _branches, _circuits, ct);

        // الاستهداف (§7.1): مالك الدائرة المنابة (رئيس شعبته وإلا قسمه)،
        // وخارجيةً رئيس قسم الفرع المناب، وبلا دائرة رئيس قسم فرع المنيب.
        var (alertBranch, ownerId) = await DocumentDelegationRules.ResolvePendingAlertTargetAsync(
            fields.IsExternal, fields.ExternalBranchId, source.BranchId, circuitId, _users, _circuits, ct);
        var pendingApprovalMessage = HeadAlertMessages.DelegationPendingApproval(new(
            source.CreatedBy?.FullName ?? string.Empty, DocumentDelegationService.SourceLabel(source), court));

        DocumentDelegation delegation = null!;
        await _tx.RunAsync(async token =>
        {
            // W1/B6: قفل صف المصدر قبل فحص الأصول/الإدراج — يمنع سباق الشطب مع إنشاء الإنابة.
            await _documents.GetByIdForUpdateAsync(source.Id, token);
            // B2: الفحص وبناء اللقطة والحفظ داخل المعاملة نفسها — لا فحص حجب خارجها.
            // إعادة قراءة طازجة بلا تتبع: قراءات السياق المتتبَّع تُرجع المثيلات نفسها
            // (identity resolution) ولا ترى ما التزمته معاملة منافسة بعد القراءة الأولى.
            var fresh = await _documents.GetByIdWithDelegationsNoTrackingAsync(source.Id, token)
                ?? throw new ArgumentException("الملف المنيب غير موجود");
            await DocumentDelegationRules.PruneStaleReservationsAsync(fresh, _reservations, token);
            await DocumentDelegationService.ValidateAssetsNotBlockedAsync(
                fresh, request.AssetIds, excludeDelegationId: null, token);

            delegation = new DocumentDelegation
            {
                SourceDocumentId = source.Id,
                DelegatedCourt = court,
                // `PB-001`: الدائرة المنابة المعيارية (تضبط مع الخام).
                DelegatedCourtNorm = string.IsNullOrWhiteSpace(court)
                    ? null
                    : ArabicNameNormalizer.Normalize(court),
                DelegatedCircuitId = circuitId,
                IsExternal = fields.IsExternal,
                ExternalBranchId = fields.ExternalBranchId,
                DelegationDate = fields.DelegationDate,
                DelegationText = DocumentDelegationService.Normalize(request.DelegationText),
                DepositBookNumber = DocumentDelegationService.Normalize(request.DepositBookNumber),
                DepositBookDate = fields.DepositBookDate,
                Status = DelegationStatusCatalog.PendingHead,
                CreatedById = command.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            DocumentDelegationService.ApplyDelegationAssets(delegation, fresh, request.AssetIds);

            await _delegations.AddAsync(delegation, token);
            await _uow.SaveChangesAsync(token);

            // B3: حجز ما استهلكته لقطات الإنابة الجديدة من المسبح بعد الحواجب — اللغة
            // المشتركة مع المسح (التوائم قابلة للتبادل ولا تحمل اللقطات معرفاتها):
            // يُحجز المستهلَك لا المطلوب حرفيًا، وإلا اختلف المرجعان على التوأم المحجوب.
            var blocked = DocumentDelegationService.ComputeBlockedIds(fresh, excludeDelegationId: null);
            var pool = fresh.Assets.OrderBy(a => a.Id).Where(a => !blocked.Contains(a.Id)).ToList();
            var ownConsumed = DocumentDelegationService.ConsumeSnapshots(
                pool, delegation.Assets.OrderBy(s => s.Id), new HashSet<int>(blocked));
            foreach (var assetId in ownConsumed)
                await _reservations.AddAsync(new DelegationAssetReservation
                {
                    DelegationId = delegation.Id,
                    SourceDocumentId = source.Id,
                    AssetId = assetId,
                }, token);

            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    pendingApprovalMessage,
                    HeadAlertAudience.SingleHead,
                    alertBranch,
                    command.UserId,
                    DocumentId: source.Id,
                    DelegationId: delegation.Id,
                    RecipientUserId: ownerId),
                supersedePlan: null,
                occurredAt: DateTime.UtcNow,
                ct: token);
            try
            {
                await _uow.SaveChangesAsync(token);
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                // الخاسر في السباق: نفس رسالة الحارس (400 بالمجال) لا 500.
                throw new ArgumentException(
                    DocumentDelegationService.BlockedAssetsMessage(fresh, request.AssetIds), ex);
            }

            await _audit.LogAsync(command.ActorName, "create_delegation",
                source.Id, source.DocumentType,
                $"سطّر إنابة على الملف (رقم {source.Id}) إلى {court}", token);
        }, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return DocumentDelegationService.ToDto(delegation, source, currentYear);
    }
}

/// <summary>
/// تعديل إنابة معلّقة — يملك الحرّاس وإعادة بناء الحقول ومواءمة الحجوزات واستبدال
/// تنبيه الاعتماد، ومعاملته، وتدقيق `update_delegation`.
/// </summary>
public sealed record UpdateDocumentDelegationCommand(
    int DelegationId,
    UpsertDelegationRequest Request,
    int UserId,
    string? ActorName) : ICommand<DelegationDto?>;

public sealed class UpdateDocumentDelegationCommandHandler
    : CommandHandlerBase<UpdateDocumentDelegationCommand, DelegationDto?>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDelegationReservationRepository _reservations;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public UpdateDocumentDelegationCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDelegationReservationRepository reservations,
        IUserRepository users,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<UpdateDocumentDelegationCommand, DelegationDto?>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _reservations = reservations;
        _users = users;
        _branches = branches;
        _circuits = circuits;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DelegationDto?> HandleCoreAsync(
        UpdateDocumentDelegationCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var delegation = await _delegations.GetByIdWithDetailsAsync(command.DelegationId, ct)
            ?? throw new KeyNotFoundException("الإنابة غير موجودة");

        DocumentDelegationRules.EnsurePendingByOwner(delegation, command.UserId);
        var source = delegation.SourceDocument;

        var (court, circuitId, fields) = await DocumentDelegationRules.ValidateAndBuildAsync(
            request, source.BranchId, _branches, _circuits, ct);

        // التصحيح قد يغيّر مالك الاعتماد (دائرة/فرع)، فتُبنى هوية المستلم الجديدة
        // مسبقًا، ويُستبدل أي تنبيه سابق للإنابة عند معالجة Outbox.
        var (alertBranch, ownerId) = await DocumentDelegationRules.ResolvePendingAlertTargetAsync(
            fields.IsExternal, fields.ExternalBranchId, source.BranchId, circuitId, _users, _circuits, ct);
        var pendingApprovalMessage = HeadAlertMessages.DelegationPendingApproval(new(
            source.CreatedBy?.FullName ?? string.Empty, DocumentDelegationService.SourceLabel(source),
            DocumentDelegationService.AlertCourtAfterDaira(court)));

        await _tx.RunAsync(async token =>
        {
            // B2+D1: الفحص أولًا على قراءة طازجة بلا تتبع، ثم أي إسناد على الكيان —
            // داخل المعاملة نفسها. الرفض لا يترك حالة Modified عالقة.
            var fresh = await _documents.GetByIdWithDelegationsNoTrackingAsync(source.Id, token)
                ?? throw new ArgumentException("الملف المنيب غير موجود");
            await DocumentDelegationRules.PruneStaleReservationsAsync(fresh, _reservations, token);
            await DocumentDelegationService.ValidateAssetsNotBlockedAsync(
                fresh, request.AssetIds, excludeDelegationId: delegation.Id, token);

            delegation.DelegatedCourt = court;
            delegation.DelegatedCourtNorm = string.IsNullOrWhiteSpace(court)
                ? null
                : ArabicNameNormalizer.Normalize(court);
            delegation.DelegatedCircuitId = circuitId;
            delegation.IsExternal = fields.IsExternal;
            delegation.ExternalBranchId = fields.ExternalBranchId;
            delegation.DelegationDate = fields.DelegationDate;
            delegation.DelegationText = DocumentDelegationService.Normalize(request.DelegationText);
            delegation.DepositBookNumber = DocumentDelegationService.Normalize(request.DepositBookNumber);
            delegation.DepositBookDate = fields.DepositBookDate;
            delegation.UpdatedAt = DateTime.UtcNow;
            // أي كتابة ترفع الرمز — وإلا سحقت كتابة متزامنة قديمة بصمت (TOCTOU).
            delegation.Version++;
            // تصحيح المحامي يُبطل قراري الرئيس السابقين على المحتوى القديم (التوجيه
            // لشعبة والرفض للدائرة الخطأ) — تُعاد المراجعة من جديد على المحتوى الجديد.
            delegation.RedirectedToSectionId = null;
            delegation.RejectReason = null;
            DocumentDelegationService.ApplyDelegationAssets(delegation, fresh, request.AssetIds);

            // B3: مواءمة الحجوزات مع اللقطات الجديدة (حذف المباشر أولًا فلا تعارض ذاتي) —
            // تُحجز المستهلَكة من المسبح بعد الحواجب (لغة المسح المشتركة)، لا المطلوبة حرفيًا.
            await _reservations.DeleteByDelegationAsync(delegation.Id, token);
            var blocked = DocumentDelegationService.ComputeBlockedIds(fresh, excludeDelegationId: delegation.Id);
            var pool = fresh.Assets.OrderBy(a => a.Id).Where(a => !blocked.Contains(a.Id)).ToList();
            var ownConsumed = DocumentDelegationService.ConsumeSnapshots(
                pool, delegation.Assets.OrderBy(s => s.Id), new HashSet<int>(blocked));
            foreach (var assetId in ownConsumed)
                await _reservations.AddAsync(new DelegationAssetReservation
                {
                    DelegationId = delegation.Id,
                    SourceDocumentId = source.Id,
                    AssetId = assetId,
                }, token);

            _delegations.Update(delegation);

            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    pendingApprovalMessage,
                    HeadAlertAudience.SingleHead,
                    alertBranch,
                    command.UserId,
                    DocumentId: source.Id,
                    DelegationId: delegation.Id,
                    RecipientUserId: ownerId),
                new HeadAlertSupersedePlan(
                    delegation.Id,
                    AppealId: null,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_updated"),
                occurredAt: DateTime.UtcNow,
                ct: token);
            try
            {
                await _uow.SaveChangesAsync(token);
            }
            catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
            {
                throw new ArgumentException(
                    DocumentDelegationService.BlockedAssetsMessage(fresh, request.AssetIds), ex);
            }
            await _audit.LogAsync(command.ActorName, "update_delegation",
                source.Id, source.DocumentType,
                $"عدّل إنابة (رقم {delegation.Id}) إلى {court}", token);
        }, ct);

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return DocumentDelegationService.ToDto(delegation, source, currentYear);
    }
}
