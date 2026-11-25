using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// تعديل ملف قائم — يملك المعاملة والتدقيق (`update`) وشبكتي التزامن/الترقيم
/// والمرآة والتنبيهات والوقوعات، مع حارسَي الانتقال المنفّذ والإنابة كما الأصل.
/// <para>
/// **حدود المعاملة:** لا وسم `ITransactionalRequest`: الأصل يفتح `_tx.RunAsync`
/// بنفسه داخل شبكتَي `WithConcurrencyGuardAsync`/`WithNumberGuardAsync`، والتحققات
/// الثقيلة تسبق المعاملة (ضد 400 داخل معاملة) — فلا تداخل ولا تغيير في الترتيب.
/// </para>
/// </summary>
public sealed record UpdateDocumentCommand(
    int DocumentId,
    DocumentUpsertRequest Request,
    string? ActorName,
    int? UserId = null) : ICommand<DocumentResponse?>;

public sealed class UpdateDocumentCommandHandler
    : CommandHandlerBase<UpdateDocumentCommand, DocumentResponse?>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<DocumentRegistrationDate> _registrationDates;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IExecutionActionService _actionService;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IDocumentStatusService _statusService;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly IDocumentNumberRegistry _numberRegistry;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public UpdateDocumentCommandHandler(
        IDocumentRepository documents,
        IRepository<DocumentRegistrationDate> registrationDates,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IExecutionActionService actionService,
        IDocumentDelegationService delegationService,
        IDocumentStatusService statusService,
        IDocumentWriteGuard writeGuard,
        IDocumentNumberRegistry numberRegistry,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<UpdateDocumentCommand, DocumentResponse?>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _registrationDates = registrationDates;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _actionService = actionService;
        _delegationService = delegationService;
        _statusService = statusService;
        _writeGuard = writeGuard;
        _numberRegistry = numberRegistry;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<DocumentResponse?> HandleCoreAsync(
        UpdateDocumentCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return null;

        var request = command.Request;
        DocumentValidator.NormalizeCollections(request);
        DocumentValidator.ValidateSide(request);
        DocumentValidator.ValidateExecutedRequest(request);
        DocumentValidator.ValidateGuarantorCount(request);
        DocumentValidator.ValidateRegistrationDate(request);
        DocumentValidator.ValidateFileYearNotFuture(request.FileYear, CurrentYear());
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
        DocumentValidator.ValidateAnnexes(request);

        // لقطة ما قبل التعديل — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var before = Common.Audit.DocumentChangeTracker.Capture(doc);

        // التجديد عند تعديل ملف مشطوب إلى متداول: يُطبَّق بيان التجديد (رقم الملف الجديد إلزامي)
        // فيعود الملف برقمه ونوعه الجديدين لسنة الإعادة. التعديل دون تغيير الحالة لا يُجدَّد.
        var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus);

        // حارس انتقالات وضع «منفذ عليه» (§4.6): يطابق قواعد نافذة تغيير الحالة، فلا تنتقل
        // الحالة عبر نموذج التعديل إلا بما تسمح به النافذة (رحّلات الشطب والتجديد الشرعية).
        ValidateExecutedTransitionOnEdit(doc, request);

        // حارس تعديل الملف المناب (قرار 2: «الملف المناب مرآةً للمنيب»): يرفض الفروق القيمية
        // في الحقول المقفولة، ويرصد الإضافات المحلية المسموحة (وريث جديد/ممثل جديد) لتنبيه
        // منيب الملف بها بعد نجاح الحفظ.
        var targetEditReport = _delegationService.ValidateTargetEdit(doc, request);

        // قفل «الحجز والأصل بعد التسطير» على الملف المنيب (قرار المستخدم): تاريخ القاء
        // الحجز على أصلٍ تحجبه إنابة سارية يُعدَّل فقط ولا يُحذف، وحذف صف الأصل نفسه
        // مرفوض — قبل أي تغيير (ApplyRequest يستبدل الأصول بالكامل).
        await _delegationService.ValidateSeizureLockAsync(doc, request, ct);

        // سجل الدوائر: تبديل الدائرة من التعديل مسموح (فحص 409) — يُحفظ القديم للوقوعتين.
        // إن لم يُرسل تغيير دائرة (لا معرف ولا نص)، يُبقى الحالي بلا إعادة حل (توافق الطلبات الجزئية).
        var oldCircuitId = doc.ExecutionCircuitId;
        var oldCircuitName = doc.Court;
        var oldCourtNorm = doc.CourtNorm;
        var wasPending = doc.NeedsRegistration;

        DocumentUpsertMapper.ApplyRequest(doc, request);
        // سجل الدوائر: تبديل الدائرة من التعديل مسموح (فحص 409) — يُحفظ القديم للوقوعتين.
        // القواعد: (1) بلا معرف ولا نص → إبقاء الحالي (توافق الطلبات الجزئية)؛
        // (2) نفس المعرف → إبقاء (قاعدة الجد: ملف الدائرة المعطلة يُعدَّل ويبقى عليها)؛
        // (3) سواهما → حل صريح بالمعرف أو بالنص (خارج السجل = 400، لا تجاهل صامت).
        var idProvided = request.ExecutionCircuitId is not null;
        var textProvided = !string.IsNullOrWhiteSpace(request.Court);
        if ((!idProvided && !textProvided) || (idProvided && request.ExecutionCircuitId == oldCircuitId))
        {
            doc.ExecutionCircuitId = oldCircuitId;
            doc.Court = oldCircuitName;
            doc.CourtNorm = oldCourtNorm;
        }
        else
        {
            var editCircuit = await _writeGuard.ResolveCircuitForWriteAsync(request, doc.BranchId, isExternalTarget: false, ct);
            DocumentText.ApplyCircuitDerivation(doc, editCircuit, request);
        }
        // تعديل ملف «بانتظار إعادة القيد» برقم غير فارغ يُسقط الـ flag (مع إعادة تحقق الوحدانية).
        if (wasPending && !string.IsNullOrWhiteSpace(doc.FileNumber) && !string.IsNullOrWhiteSpace(doc.FileYear))
            doc.NeedsRegistration = false;
        DocumentText.FillDerivedFields(doc);
        DocumentUpsertMapper.ApplyRegistrationDate(doc, request.FileRegistrationDate, _registrationDates);
        doc.UpdatedAt = DateTime.UtcNow;

        // RF-009 بتوقيع الدائرة: فحص التكرار بعد تطبيق الطلب.
        await _writeGuard.EnsureNumberUniqueByCircuitAsync(command.DocumentId, doc.ExecutionCircuitId, doc.CourtNorm,
            doc.FileNumber, doc.FileType, doc.FileYear, ct);

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, request.Version);

        List<MirrorNotification> mirrorNotifications = [];
        var circuitSwitched = oldCircuitId != doc.ExecutionCircuitId;
        var result = await _writeGuard.WithConcurrencyGuardAsync(() => _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            if (wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.None)
                await _statusService.ApplyRenewalAsync(doc, request, true, command.UserId, token);
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await _statusService.AddStruckOffOccurrenceAsync(doc, command.UserId, token);
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // الكاتب الوحيد: مزامنة صف القيد/فحص التفرد بعد حفظ حقول المستند.
            await _numberRegistry.SyncRegistrationAsync(doc, command.UserId, command.ActorName, request.WarningToken, token);
            // تبديل الدائرة من التعديل يسجل الوقوعتين تلقائيًا (قديم/جديد) — وإلا قفزت بلا أثر.
            if (circuitSwitched)
                await _writeGuard.AddCircuitSwitchOccurrencesAsync(doc, oldCircuitName, command.UserId, token);
            await _delegationService.SyncSnapshotsAsync(doc, token);
            // المرآة: نسخ تغييرات المنيب إلى مناباته المعلقة (داخل المعاملة نفسها) وإعادة اشتقاق
            // الحقول المحسوبة فيها — تعيد إشعارات تُطلق بعد نجاح المعاملة.
            mirrorNotifications = await _delegationService.MirrorSourceEditsAsync(doc, token);
            await _writeGuard.LogDocumentChangesAsync(before, doc, command.ActorName, "update",
                $"عدّل المستند (رقم {doc.Id})", token);
            // التنبيهات تُسجَّل في Outbox داخل معاملة الحفظ نفسها:
            // (1) تنبيهات المرآة إلى المحامين المنابين بتحديث نسخة الملف،
            // (2) تنبيه المنيب بالإضافات المحلية المضافة على الملف المناب (ورثة/ممثل شرعي) —
            //     وتُثبَّت الإضافة في سجل الإنابة (عدّاد + ملخص) قبل الإطلاق.
            await _delegationService.FireMirrorNotificationsAsync(mirrorNotifications, token);
            if (targetEditReport.HasAdditions)
                await _delegationService.RecordLocalAdditionsAndFireReportAsync(doc, targetEditReport, token);
            await _actionService.SeedInitialActionsAsync(doc, request.InitialActions, command.UserId, command.ActorName, token);
            return DocumentResponse.FromEntity(doc, CurrentYear());
        }, ct), doc.FileNumber));

        return result;
    }

    private int CurrentYear() => ServerClock.CurrentYear(_clock, _timeZone);

    /// <summary>
    /// حارس انتقالات وضع «منفذ عليه/عرض وايداع» في نموذج التعديل (§4.6): لا تمر التغييرات
    /// المباشرة عبر التحرير إلا ما تسمح به نافذة تغيير الحالة — الشطبَ والتجديدَ الشرعيين.
    /// (مشطوب → منفذ مباشر ممنوع؛ «منفذ» في «منفذ عليها» نهائية؛ «عرض وايداع» من منفذ
    /// لا تُشطب ولا تُعاد مباشرة بل عبر نافذة الحالة بكتاب السير.) — معزول عن نظام «طالبة
    /// تنفيذ» الذي تحكمه آلة حالات مغلقة منفصلة (لا يسند UpdateAsync الحالةَ إطلاقًا).
    /// </summary>
    private static void ValidateExecutedTransitionOnEdit(Document doc, DocumentUpsertRequest request)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            return;

        var currentStatus = doc.ExecutedStatus;
        var current = currentStatus is not null && ExecutedStatusCatalog.IsStored(currentStatus)
            ? currentStatus
            : ExecutedStatusCatalog.None;
        var normalized = request.ExecutedStatus?.Trim();
        var requested = string.IsNullOrEmpty(normalized) ? ExecutedStatusCatalog.None : normalized;

        if (current == ExecutedStatusCatalog.StruckOff && requested == ExecutedStatusCatalog.Executed)
            throw new ArgumentException("لا يمكن نقل ملف مشطوب إلى «منفذ» مباشرة — يجب إعادته أولًا إلى المتداول (تجديد)");
        if (current != ExecutedStatusCatalog.Executed || requested == ExecutedStatusCatalog.Executed)
            return;

        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            throw new ArgumentException("وضع «منفذ» في صفة «الجهة العامة منفذ عليها» نهائي لا يُغيَّر بالتحرير");
        if (requested == ExecutedStatusCatalog.StruckOff)
            throw new ArgumentException("لا يمكن شطب «عرض وايداع» من وضع «منفذ»");
        throw new ArgumentException("إرجاع «عرض وايداع» من وضع «منفذ» إلى المتداول يتم عبر نافذة تغيير الحالة بكتاب السير بالملف لا عبر التحرير");
    }
}
