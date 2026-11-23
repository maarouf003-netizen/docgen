using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// نقل ملكية دائرة لمالك جديد داخل الفرع نفسه (قرار §2.5 + §8.3): المفتوح والمغلق
/// يتبع الجديد، والتدقيق ثابت بأسماء الفاعلين الأصليين. الإحالات المفتوحة (§6.6):
/// تُنشأ تنبيهات بديلة للمالك الجديد (لا تعديل القديمة) — و`ForwardState` لا يتغير.
/// <para>
/// **حدود المعاملة:** يستثني وسم `ITransactionalRequest` عمدًا ويملك معاملته عبر
/// `ITransactionRunner`، لأن معالجة تعارض التزامن (`IsConcurrencyViolation`) تلتف
/// حول المعاملة كلها كما الأصل — فلا معاملة متداخلة ولا التماس لخطأ بعد الإجهاض.
/// </para>
/// </summary>
public sealed record TransferExecutionCircuitCommand(
    int CircuitId,
    int? TargetSectionId,
    int ActorUserId,
    string? ActorName,
    long? Version = null) : ICommand<ExecutionCircuitDto>;

public sealed class TransferExecutionCircuitCommandHandler
    : CommandHandlerBase<TransferExecutionCircuitCommand, ExecutionCircuitDto>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IAppealRepository _appeals;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IUserRepository _users;
    private readonly IHeadAlertOutbox _alertOutbox;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IReferenceDataCache? _cache;

    public TransferExecutionCircuitCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IAppealRepository appeals,
        IRepository<HeadSuccession> successions,
        IUserRepository users,
        IHeadAlertOutbox alertOutbox,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<TransferExecutionCircuitCommand, ExecutionCircuitDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _sections = sections;
        _documents = documents;
        _delegations = delegations;
        _appeals = appeals;
        _successions = successions;
        _users = users;
        _alertOutbox = alertOutbox;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
        _cache = cache;
    }

    protected override async Task<ExecutionCircuitDto> HandleCoreAsync(
        TransferExecutionCircuitCommand command, CancellationToken ct)
    {
        ExecutionCircuitDto result = null!;
        try
        {
            await _tx.RunAsync(async token =>
            {
                var circuit = await _circuits.GetByIdAsync(command.CircuitId, token)
                    ?? throw new ArgumentException("الدائرة غير موجودة");
                if (command.Version is not null && circuit.Version != command.Version.Value)
                    throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");

                Section? targetSection = null;
                if (command.TargetSectionId.HasValue)
                {
                    targetSection = await _sections.GetByIdAsync(command.TargetSectionId.Value, token)
                        ?? throw new ArgumentException("الشعبة الهدف غير موجودة");
                    if (targetSection.BranchId != circuit.BranchId)
                        throw new ArgumentException("النقل داخل الفرع نفسه — الشعبة من فرع آخر");
                    if (!targetSection.IsActive)
                        throw new ArgumentException("الشعبة الهدف معطلة — اختر شعبة نشطة");
                }

                if (circuit.SectionId == command.TargetSectionId)
                {
                    result = await ExecutionCircuitRules.BuildDtoAsync(circuit, _documents, _sections, token);
                    return;
                }

                var oldOwner = circuit.SectionId.HasValue
                    ? (await _sections.GetByIdAsync(circuit.SectionId.Value, token))?.Name ?? "؟"
                    : "رئيس القسم";
                var newOwner = targetSection?.Name ?? "رئيس القسم";

                circuit.SectionId = command.TargetSectionId;
                circuit.UpdatedAt = DateTime.UtcNow;
                circuit.Version++;
                _circuits.Update(circuit);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
                // النتيجة تُثبَّت هنا قبل أي خروج مبكر (مالك بلا رئيس) — وإلا عاد
                // `null` فتحوّل `Ok` إلى `204`.
                result = await ExecutionCircuitRules.BuildDtoAsync(circuit, _documents, _sections, token);

                await _audit.LogAsync(command.ActorName, "transfer_circuit", null, null,
                    $"نقل الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner} (الملفات المفتوحة والمغلقة تتبع المالك الجديد)", token);

                // المالك الجديد: رئيس الشعبة الهدف، أو رئيس القسم للقسم.
                var newHead = command.TargetSectionId.HasValue
                    ? await _users.FindActiveHeadAsync(UserRole.SubHead, circuit.BranchId, command.TargetSectionId, token)
                    : await _users.FindActiveHeadAsync(UserRole.Head, circuit.BranchId, null, token);
                if (newHead is null)
                    return;

                await _successions.AddAsync(new HeadSuccession
                {
                    BranchId = circuit.BranchId,
                    SectionId = command.TargetSectionId,
                    UserId = newHead.Id,
                    Role = newHead.Role,
                    Event = HeadSuccessionEventCatalog.CircuitTransferred,
                    At = DateTime.UtcNow,
                    ActorName = command.ActorName,
                    Reason = $"نُقلت الدائرة «{circuit.Name}» من {oldOwner} إلى {newOwner}",
                }, token);

                // بدائل المعلّق للمالك الجديد (§6.6): إنابات واردة معلقة + استئنافات
                // بلا إسناد على ملفات الدائرة — تنبيه واحد جامع (لا تعديل القديمة).
                var fileIds = (await _documents.ListByCircuitAsync(circuit.Id, includeDeleted: false, token))
                    .Select(d => d.Id)
                    .ToList();
                var pendingDelegations = (await _delegations.ListByDelegatedCircuitAsync(circuit.Id, token))
                    .Count(g => g.Status == DelegationStatusCatalog.PendingHead);
                var pendingAppeals = 0;
                if (fileIds.Count > 0)
                {
                    pendingAppeals = (await _appeals.ListByDocumentIdsAsync(fileIds, token))
                        .Count(a => a.Status == AppealStatusCatalog.Pending && a.AssignedLawyerId == null);
                }

                await _alertOutbox.EnqueueSystemAsync(
                    new CreateSystemHeadAlertRequest(
                        HeadAlertKindCatalog.CircuitTransferred,
                        HeadAlertMessages.CircuitTransferred(new(
                            circuit.Name, fileIds.Count, pendingDelegations, pendingAppeals)),
                        HeadAlertAudience.SingleHead,
                        circuit.BranchId,
                        command.ActorUserId,
                        ExecutionCircuitId: circuit.Id,
                        RecipientUserId: newHead.Id),
                    supersedePlan: null,
                    occurredAt: DateTime.UtcNow,
                    ct: token);
                await _uow.SaveChangesAsync(token);
                _cache?.Invalidate(ReferenceDataGroups.Circuits);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsConcurrencyViolation(ex))
        {
            // كتابتان متزامنتان حقيقيتان على الدائرة نفسها — 409 ودية بدل 500 خام.
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا", ex);
        }
        return result;
    }
}
