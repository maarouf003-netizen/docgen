using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// تغيير حالة ملف «الجهة العامة طالبة التنفيذ» وفق آلة الحالات، مع حراس الشطب/الإحالة
/// والإنابة، وتوريث المناب، وتدقيق موحّد — في معاملة واحدة.
/// </summary>
public sealed record UpdateDocumentStatusCommand(
    int DocumentId,
    string Status,
    Dictionary<string, string?> Fields,
    string? ActorName,
    long? Version = null) : ICommand<bool>;

public sealed class UpdateDocumentStatusCommandHandler
    : CommandHandlerBase<UpdateDocumentStatusCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IHeadAlertService _alertService;

    public UpdateDocumentStatusCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IRepository<DocumentOccurrence> occurrences,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentDelegationService delegationService,
        IHeadAlertService alertService,
        IEnumerable<IPipelineBehavior<UpdateDocumentStatusCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _occurrences = occurrences;
        _clock = clock;
        _timeZone = timeZone;
        _delegationService = delegationService;
        _alertService = alertService;
    }

    protected override async Task<bool> HandleCoreAsync(
        UpdateDocumentStatusCommand command, CancellationToken ct)
    {
        var fields = command.Fields;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;
        // حراس NeedsRegistration الصريحة (البند 32): تُمنع على المعلق — تغيير الحالة حيث تلزم الهوية.
        DocumentText.EnsureNotPendingRegistration(doc, "تغيير الحالة");
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        var status = (command.Status ?? string.Empty).Trim();
        var valid = ExecutionStatusCatalog.ValidStatuses.Contains(status)
            || status == ExecutionStatusCatalog.StateStruckOff;
        if (!valid)
            throw new ArgumentException("حالة غير صالحة");

        // حارس المناب (ب2 — E1): حالة الملف المناب تلحق حالة الملف المنيب في اعتباره منفذ أو
        // تريث؛ يُحظر تغيير حالته بأي حال، عدا الانتقال إلى «مشطوب» الذي يحسمه تطبيق المنيب
        // (يُستثنى هذا الفرع ليظل الشطب مقدورًا عليه من إدارة المناب المشطوبة).
        if (doc.SourceDelegationId != null && status != ExecutionStatusCatalog.StateStruckOff)
            throw new ArgumentException("حالة الملف المناب تلحق حالة الملف المنيب في اعتباره منفذ أو تريث");

        // حارس الشطب (S1 — E7): لا يجوز شطب ملف عليه إنابة سارية (بأي حال سوى المنفذة).
        // «سارية» تشمل «بانتظار رئيس القسم» عمدًا: اعتماد الإنابة لا يعيد فحص المنيب،
        // فيُقفل الشطب قبل تسطير إنابة من ملف مشطوب. التعريف من DelegationActivityPolicy (D2).
        if (status == ExecutionStatusCatalog.StateStruckOff)
        {
            var delegations = await _delegations.ListBySourceAsync(command.DocumentId, ct);
            if (delegations.Any(DelegationActivityPolicy.IsLifecycleActive))
                throw new ArgumentException("لا يجوز شطب ملف فيه انابة سارية");
        }

        // حارس الإحالة إلى البداية (قرار 9 — نمط حارس الشطب S1): لا يجوز إحالة ملف فيه
        // إنابة سارية إلى البداية، بمن فيه القادم من «منفذ جبريا» (تسوية إنابة مُتممة
        // كبيعٍ مكتمل ليست إنابة سارية). بعد العودة إلى المتداول يصبح قابلاً للتسطير طبيعيًا
        // — الحظر على الدخول فقط ولا يُورَّث.
        if (status == ExecutionStatusCatalog.ReferredToStart)
        {
            var delegations = await _delegations.ListBySourceAsync(command.DocumentId, ct);
            if (delegations.Any(DelegationActivityPolicy.IsLifecycleActive))
                throw new ArgumentException("لا يجوز إحالة ملف فيه إنابة سارية إلى البداية");
        }

        // آلة الحالات: تُمنع الانتقالات غير المسموحة من الحالة الحالية صراحةً.
        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.IsAllowedStatusChange(current, status))
            throw new ArgumentException(
                $"لا يمكن الانتقال من الحالة «{ExecutionStatusCatalog.ToStateLabel(current)}» إلى «{ExecutionStatusCatalog.ToStatusLabel(status)}»");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, command.Version);

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var statusBefore = DocumentChangeTracker.Capture(doc);

        var details = new Dictionary<string, string>();
        switch (status)
        {
            case ExecutionStatusCatalog.ExecutedForcibly:
                var sub = fields.GetValueOrDefault("execSubStatus");
                if (sub is null || !ExecutionStatusCatalog.ValidSubStatuses.Contains(sub))
                    throw new ArgumentException("نوع التنفيذ الفرعي غير صالح");
                doc.ExecSubStatus = sub;
                details["execSubStatus"] = sub;
                DocumentStatusRules.ApplyCollectedAmounts(doc, fields, details);
                DocumentStatusRules.ApplySoldAssets(doc, fields, details);
                DocumentStatusRules.ClearBaraetFields(doc);
                DocumentStatusRules.ClearTarithFields(doc);
                DocumentStatusRules.ClearSayerFields(doc);
                DocumentValidator.RequireField(fields, "forcedExecutionDate", "تاريخ قرار الإحالة القطعية");
                doc.ForcedExecutionDate = fields.GetValueOrDefault("forcedExecutionDate");
                DocumentStatusRules.CopyDetail(details, "forcedExecutionDate", doc.ForcedExecutionDate);
                break;
            case ExecutionStatusCatalog.ExecutedBySettlement:
                DocumentValidator.RequireField(fields, "baraetNumber", "رقم كتاب براءة الذمة");
                DocumentValidator.RequireField(fields, "baraetDate", "تاريخ كتاب براءة الذمة");
                doc.BaraetNumber = fields.GetValueOrDefault("baraetNumber");
                doc.BaraetDate = fields.GetValueOrDefault("baraetDate");
                doc.BaraetRegNumber = fields.GetValueOrDefault("baraetRegNumber");
                doc.BaraetRegDate = fields.GetValueOrDefault("baraetRegDate");
                DocumentStatusRules.CopyDetail(details, "baraetNumber", doc.BaraetNumber);
                DocumentStatusRules.CopyDetail(details, "baraetDate", doc.BaraetDate);
                DocumentStatusRules.CopyDetail(details, "baraetRegNumber", doc.BaraetRegNumber);
                DocumentStatusRules.CopyDetail(details, "baraetRegDate", doc.BaraetRegDate);
                DocumentStatusRules.ApplyCollectedAmounts(doc, fields, details);
                DocumentStatusRules.ClearTarithFields(doc);
                DocumentStatusRules.ClearSayerFields(doc);
                DocumentStatusRules.ClearForcedExecutionField(doc);
                DocumentStatusRules.ClearForcibleTransferFields(doc);
                doc.ExecSubStatus = null;
                doc.SoldAssetIds = null;
                break;
            case ExecutionStatusCatalog.Deferred:
                DocumentValidator.RequireField(fields, "tarithNumber", "رقم كتاب التريث");
                DocumentValidator.RequireField(fields, "tarithDate", "تاريخ كتاب التريث");
                doc.TarithNumber = fields.GetValueOrDefault("tarithNumber");
                doc.TarithDate = fields.GetValueOrDefault("tarithDate");
                doc.TarithRegNumber = fields.GetValueOrDefault("tarithRegNumber");
                doc.TarithRegDate = fields.GetValueOrDefault("tarithRegDate");
                DocumentStatusRules.CopyDetail(details, "tarithNumber", doc.TarithNumber);
                DocumentStatusRules.CopyDetail(details, "tarithDate", doc.TarithDate);
                DocumentStatusRules.CopyDetail(details, "tarithRegNumber", doc.TarithRegNumber);
                DocumentStatusRules.CopyDetail(details, "tarithRegDate", doc.TarithRegDate);
                DocumentStatusRules.ClearBaraetFields(doc);
                DocumentStatusRules.ClearSayerFields(doc);
                DocumentStatusRules.ClearForcedExecutionField(doc);
                DocumentStatusRules.ClearForcibleTransferFields(doc);
                doc.ExecSubStatus = null;
                DocumentStatusRules.ClearCollectedFields(doc);
                doc.SoldAssetIds = null;
                break;
            case ExecutionStatusCatalog.ReferredToStart:
                // القرار 1: الدخول من «منفذ جبريا» متاح للمنفذ جزئيًا فقط دون «منفذ كاملا».
                if (current == ExecutionStatusCatalog.ExecutedForcibly
                    && doc.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
                    throw new ArgumentException("الإحالة إلى البداية من «منفذ جبريا» متاحة فقط للملف المنفذ جزئيًا");
                DocumentValidator.RequireField(fields, "noFundsDemandNumber", "رقم كتاب المطالعة بعدم وجود أموال للتنفيذ عليها");
                DocumentValidator.RequireField(fields, "noFundsDemandDate", "تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها");
                doc.NoFundsDemandNumber = fields.GetValueOrDefault("noFundsDemandNumber")?.Trim();
                doc.NoFundsDemandDate = DocumentValidator.ParseDateTime(fields.GetValueOrDefault("noFundsDemandDate"),
                    "تاريخ كتاب المطالعة بعدم وجود أموال للتنفيذ عليها");
                var startReferralNumber = (fields.GetValueOrDefault("startReferralNumber") ?? string.Empty).Trim();
                doc.StartReferralNumber = string.IsNullOrWhiteSpace(startReferralNumber) ? null : startReferralNumber;
                doc.StartReferralDate = DocumentValidator.ParseDateTime(fields.GetValueOrDefault("startReferralDate"),
                    "تاريخ كتاب الإحالة لقسم البداية");
                DocumentStatusRules.CopyDetail(details, "noFundsDemandNumber", doc.NoFundsDemandNumber);
                DocumentStatusRules.CopyDetail(details, "noFundsDemandDate",
                    doc.NoFundsDemandDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                DocumentStatusRules.CopyDetail(details, "startReferralNumber", doc.StartReferralNumber);
                DocumentStatusRules.CopyDetail(details, "startReferralDate",
                    doc.StartReferralDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                // تطهير منشطّر حسب المصدر (قرار 11): الدخول من جزئيا يُبقي عائلة الجبريا
                // ظاهرة (ExecSubStatus/Collected*/SoldAssetIds/ForcedExecution* — يعود منها
                // عبر نقطة العودة إلى «منفذ جزئيًا» بلا عمود جديد)، ودخولا متداول/تريث على
                // مسح العائلات كاملاً. لا تُمس حقول Renewal*/StruckOffDate في الحالين،
                // وأداة ClearReferredToStartFields خاصة بالعودة (B4) لا بالدخول.
                if (current == ExecutionStatusCatalog.ExecutedForcibly)
                {
                    DocumentStatusRules.ClearBaraetFields(doc);
                    DocumentStatusRules.ClearTarithFields(doc);
                    DocumentStatusRules.ClearSayerFields(doc);
                }
                else
                {
                    DocumentStatusRules.ClearBaraetFields(doc);
                    DocumentStatusRules.ClearTarithFields(doc);
                    DocumentStatusRules.ClearSayerFields(doc);
                    DocumentStatusRules.ClearForcedExecutionField(doc);
                    DocumentStatusRules.ClearForcibleTransferFields(doc);
                    DocumentStatusRules.ClearCollectedFields(doc);
                    doc.ExecSubStatus = null;
                    doc.SoldAssetIds = null;
                }
                break;
            default: // مشطوب (نظام «طالبة تنفيذ»): يُخفى من القوائم ويظهر في صفحة «الملفات المشطوبة».
                var struckOffDateRaw = fields.GetValueOrDefault("struckOffDate");
                if (string.IsNullOrWhiteSpace(struckOffDateRaw))
                    throw new ArgumentException("يجب إدخال تاريخ الشطب");
                doc.StruckOffDate = DocumentValidator.ParseDateTime(struckOffDateRaw, "تاريخ الشطب");
                details["struckOffDate"] = struckOffDateRaw;
                DocumentStatusRules.ClearBaraetFields(doc);
                DocumentStatusRules.ClearTarithFields(doc);
                DocumentStatusRules.ClearSayerFields(doc);
                DocumentStatusRules.ClearForcedExecutionField(doc);
                DocumentStatusRules.ClearForcibleTransferFields(doc);
                doc.ExecSubStatus = null;
                DocumentStatusRules.ClearCollectedFields(doc);
                doc.SoldAssetIds = null;
                break;
        }

        doc.ExecStatus = status;
        var occurrenceType = status == ExecutionStatusCatalog.StateStruckOff
            ? OccurrenceTypeCatalog.StruckOff
            : ExecutionStatusCatalog.Classify(status) switch
            {
                ExecutionStatus.ExecutedForcibly => OccurrenceTypeCatalog.Forcible,
                ExecutionStatus.ExecutedBySettlement => OccurrenceTypeCatalog.Settled,
                ExecutionStatus.Deferred => OccurrenceTypeCatalog.Deferred,
                ExecutionStatus.ReferredToStart => OccurrenceTypeCatalog.ReferredToStart,
                _ => throw new ArgumentException("حالة غير صالحة"),
            };

        var statusUpdated = await _writeGuard.WithConcurrencyGuardAsync(() => _tx.RunAsync(async token =>
        {
            // W1/B6: قفل صف المصدر يمنع سباق «شطب/إحالة» مع إنشاء/تسجيل إنابة سارية.
            // الفحص الخارجي يبقى للتشخيص السريع، وهذا الفحص هو الحاكم داخل القفل.
            await _documents.GetByIdForUpdateAsync(doc.Id, token);
            if (status is ExecutionStatusCatalog.StateStruckOff or ExecutionStatusCatalog.ReferredToStart)
            {
                var active = (await _delegations.ListBySourceAsync(doc.Id, token))
                    .Any(DelegationActivityPolicy.IsLifecycleActive);
                if (active)
                {
                    throw new ArgumentException(
                        status == ExecutionStatusCatalog.StateStruckOff
                            ? "لا يجوز شطب ملف فيه انابة سارية"
                            : "لا يجوز إحالة ملف فيه إنابة سارية إلى البداية");
                }
            }
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // تسجيل وقعة تغيير الحالة بحقولها الكاملة ضمن المعاملة نفسها — سجل زمني مستقل
            // يبقى ظاهرًا في «وقوعات الملف» بعد أي تراجع أو تعديل لاحق للحالة.
            // وقعة الشطب تحمل الرقم الفعّال وقت الشطب (آخر رقم أساس ≤ سنة الشطب عبر المحلل
            // المركزي) ونوعه وسنة شطبه كما في مسار «منفذ عليه».
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                Source = OccurrenceSourceCatalog.System,
                OccurrenceType = occurrenceType,
                EventDate = status == ExecutionStatusCatalog.StateStruckOff ? doc.StruckOffDate : DateTime.UtcNow,
                FileNumber = status == ExecutionStatusCatalog.StateStruckOff
                    ? EffectiveFileIdentity.Number(doc, doc.StruckOffDate?.Year ?? ServerClock.CurrentYear(_clock, _timeZone))
                    : null,
                FileType = status == ExecutionStatusCatalog.StateStruckOff
                    ? string.IsNullOrWhiteSpace(doc.FileType) ? null : doc.FileType.Trim()
                    : null,
                Year = status == ExecutionStatusCatalog.StateStruckOff ? doc.StruckOffDate?.Year : null,
                Details = details.Count > 0 ? DocumentStatusRules.SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = status == ExecutionStatusCatalog.StateStruckOff
                ? $"حالة {ExecutionStatusCatalog.StateStruckOff}"
                : $"حالة {ExecutionStatusCatalog.ToLabel(ExecutionStatusCatalog.Classify(status))}";
            await _writeGuard.LogDocumentChangesAsync(statusBefore, doc, command.ActorName, "status", auditDetail, token);
            // ب4: توريث «تريث»/استرداد المناب يتبَع حالة المنيب الجديدة — داخل معاملة الحالة نفسها.
            await DocumentStatusRules.ApplyDelegationInheritanceOrRecoveryAsync(
                doc, _documents, _delegations, _occurrences, _uow, _clock, _timeZone, token);
            // D3: نهائية مناب (استرداد/شطب يدوي) تؤرشف تنبيهات الإنابة العالقة —
            // قبل إطلاق تنبيهات الحالة الجديدة كي لا تُؤرشف التنبيهات الجديدة.
            await DocumentStatusRules.CleanupPendingCompletionAlertsAsync(
                command.DocumentId, _delegations, _documents, _alertService, token);
            // مرآة: تغيّر حالة المنيب يُنبه مناباته المعلقة.
            // الصيغ T2/T4/T5 فقط؛ جزئيا (N1) ومشطوب (N7) وسواها كبت بلا تنبيه.
            var statusAlertKind = DocumentDelegationService.DelegationStatusAlertKindFor(doc);
            if (statusAlertKind is not null)
                await _delegationService.FireStatusChangeAlertsAsync(doc, statusAlertKind.Value, token);
            return true;
        }, ct));
        return statusUpdated;
    }
}

/// <summary>
/// «التراجع» في نظام «طالبة تنفيذ»: إعادة الملف إلى المتداول بموجب كتاب الجهة العامة
/// بالسير بالملف، مع وقعة «تراجع» وتدقيق — في معاملة واحدة.
/// </summary>
public sealed record RevertDocumentStatusCommand(
    int DocumentId,
    Dictionary<string, string?> Fields,
    string? ActorName,
    long? Version = null) : ICommand<bool>;

public sealed class RevertDocumentStatusCommandHandler
    : CommandHandlerBase<RevertDocumentStatusCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentDelegationService _delegationService;

    public RevertDocumentStatusCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IRepository<DocumentOccurrence> occurrences,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentDelegationService delegationService,
        IEnumerable<IPipelineBehavior<RevertDocumentStatusCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _occurrences = occurrences;
        _clock = clock;
        _timeZone = timeZone;
        _delegationService = delegationService;
    }

    protected override async Task<bool> HandleCoreAsync(
        RevertDocumentStatusCommand command, CancellationToken ct)
    {
        var fields = command.Fields;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;
        DocumentText.EnsureNotPendingRegistration(doc, "التراجع عن الحالة");
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("التراجع عن الحالة يخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        // حارس المناب (ب2 — E1): حالة الملف المناب تلحق حالة الملف المنيب — لا يُراجع عن حالته.
        if (doc.SourceDelegationId != null)
            throw new ArgumentException("حالة الملف المناب تلحق حالة الملف المنيب في اعتباره منفذ أو تريث");

        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.CanRevert(current))
            throw new ArgumentException(
                $"لا يمكن التراجع عن الحالة الحالية «{ExecutionStatusCatalog.ToStateLabel(current)}»");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, command.Version);

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var revertBefore = DocumentChangeTracker.Capture(doc);

        // حقول كتاب الجهة العامة بالسير بالملف: رقم وتاريخ الكتاب وورودهما إلزامية.
        DocumentValidator.RequireField(fields, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
        DocumentValidator.RequireField(fields, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
        DocumentValidator.RequireField(fields, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
        DocumentValidator.RequireField(fields, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
        doc.SayerNumber = fields.GetValueOrDefault("sayerNumber");
        doc.SayerDate = fields.GetValueOrDefault("sayerDate");
        doc.SayerRegNumber = fields.GetValueOrDefault("sayerRegNumber");
        doc.SayerRegDate = fields.GetValueOrDefault("sayerRegDate");

        var details = new Dictionary<string, string>();
        DocumentStatusRules.CopyDetail(details, "sayerNumber", doc.SayerNumber);
        DocumentStatusRules.CopyDetail(details, "sayerDate", doc.SayerDate);
        DocumentStatusRules.CopyDetail(details, "sayerRegNumber", doc.SayerRegNumber);
        DocumentStatusRules.CopyDetail(details, "sayerRegDate", doc.SayerRegDate);

        // العودة إلى المتداول: تُصفَّر حالة التنفيذ وحقولها مع الإبقاء على حقول «السير بالملف»
        // محفوظةً لتبقى ظاهرة في «وقوعات الملف» (لقطة الحقوق في الوقعة أسفل).
        doc.ExecStatus = ExecutionStatusCatalog.None;
        doc.ExecSubStatus = null;
        DocumentStatusRules.ClearCollectedFields(doc);
        DocumentStatusRules.ClearBaraetFields(doc);
        DocumentStatusRules.ClearTarithFields(doc);
        DocumentStatusRules.ClearForcedExecutionField(doc);
        DocumentStatusRules.ClearForcibleTransferFields(doc);
        doc.SoldAssetIds = null;

        var reverted = await _writeGuard.WithConcurrencyGuardAsync(() => _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                Source = OccurrenceSourceCatalog.System,
                OccurrenceType = OccurrenceTypeCatalog.Revert,
                EventDate = DateTime.UtcNow,
                Details = details.Count > 0 ? DocumentStatusRules.SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(revertBefore, doc, command.ActorName, "status",
                "تراجع عن الحالة وعاد الملف إلى المتداول", token);
            // ب4: عودة المناب الموروث-تريث تلقائيًا عند تراجع المنيب من «تريث» (D3).
            var targetsReverted = await DocumentStatusRules.ApplyDelegationInheritanceOrRecoveryAsync(
                doc, _documents, _delegations, _occurrences, _uow, _clock, _timeZone, token);
            // مرآة: عودة المناب الفعلية تُنبه (T3 — متابعة السير بالملف). أما التراجع من
            // تسوية/جبريا فلا يُرجع منابًا (المسترد لا يُرجع — C1)، فلا يُطلق تنبيه
            // «انتهاء حالة التريث» على مناب مسترد (ضلال).
            if (targetsReverted)
                await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.Returned, token);
            return true;
        }, ct));
        return reverted;
    }
}

/// <summary>
/// العودة من «محال الى البداية» بنتيجتين حسب اللازمة (§2-10 — بلا عمود جديد):
/// إن حمل الملف «منفذ جزئيا» (الكاتب الوحيد لها دخول جبريا) عاد «منفذ جبريا» مع بقاء
/// عائلة الجبريا كما دخلت إطلاقًا، وإلا عاد «متداول» بمسح بقية العائلات (عملية لا-عملية
/// إذ أُنجز المسح عند الدخول). حقول التجديد موحّدة في المسارين (قرار 12): رقم الملف
/// الجديد يفعّل ApplyRenewalAsync (خلفية غير-منفذة — يلزم السنة 1900–2100 وتطابقها مع
/// سنة تاريخ التجديد)، والعودة البسيطة سلوك جديد خاص بهذه النقطة (لا «نمط
/// RestoreStruckOffAsync» ذاك يُلزم الرقم دائمًا). «struckOffDate» الاختياري لنتيجة-متداول
/// فقط. وقعة «تراجع» بسرد آلي + حقول الشطب/التجديد؛ وبلا تنبيهات مرآة عمدًا (قرار 9
/// يجعل الإنابة السارية على «محال» مستحيلة — حتى لا يُقرأ غيابها ثغرة).
/// </summary>
public sealed record ReturnFromReferredToStartCommand(
    int DocumentId,
    ReturnReferredToStartRequest Request,
    string? ActorName) : ICommand<bool>;

public sealed class ReturnFromReferredToStartCommandHandler
    : CommandHandlerBase<ReturnFromReferredToStartCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentStatusService _statusService;

    public ReturnFromReferredToStartCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IRepository<DocumentOccurrence> occurrences,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentStatusService statusService,
        IEnumerable<IPipelineBehavior<ReturnFromReferredToStartCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _occurrences = occurrences;
        _clock = clock;
        _timeZone = timeZone;
        _statusService = statusService;
    }

    protected override async Task<bool> HandleCoreAsync(
        ReturnFromReferredToStartCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;
        DocumentText.EnsureNotPendingRegistration(doc, "العودة من «محال الى البداية»");
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("العودة من «محال الى البداية» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        // حارس المناب (ب2 — E1): حالة الملف المناب تلحق حالة الملف المنيب — لا يُعاد سيره.
        if (doc.SourceDelegationId != null)
            throw new ArgumentException("حالة الملف المناب تلحق حالة الملف المنيب في اعتباره منفذ أو تريث");

        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (current != ExecutionStatusCatalog.ReferredToStart)
            throw new ArgumentException(
                $"لا يمكن العودة إلى السير بالملف من الحالة الحالية «{ExecutionStatusCatalog.ToStateLabel(current)}»");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, request?.Version);

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var returnBefore = DocumentChangeTracker.Capture(doc);

        // التوجيه باللازمة (§2-10): «منفذ جزئيا» على ملف «محال» ⟺ دخل من جزئيا — تستعيد
        // حالة جبريا والجزئية ومحفوظاتها كما هي (القرار 11)، والإلا عودة إلى متداول.
        var returnsToPartiallyExecuted = doc.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted;
        if (returnsToPartiallyExecuted)
        {
            doc.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
        }
        else
        {
            doc.ExecStatus = ExecutionStatusCatalog.None;
            doc.ExecSubStatus = null;
            DocumentStatusRules.ClearCollectedFields(doc);
            DocumentStatusRules.ClearBaraetFields(doc);
            DocumentStatusRules.ClearTarithFields(doc);
            DocumentStatusRules.ClearForcedExecutionField(doc);
            DocumentStatusRules.ClearForcibleTransferFields(doc);
            doc.SoldAssetIds = null;
        }

        // حقول «محال الى البداية» الأربعة تُمسح في الحالين (أداة العودة فقط — الدخول لا يمسح).
        DocumentStatusRules.ClearReferredToStartFields(doc);

        // «تاريخ الشطب» الاختياري خاص بنتيجة-متداول فقط ويُتجاهل في نتيجة-جزئيا.
        if (!returnsToPartiallyExecuted)
            doc.StruckOffDate = DocumentValidator.ParseDateTime(request?.StruckOffDate, "تاريخ الشطب");

        // حقول التجديد موحّدة في المسارين (قرار 12): رقم الملف الجديد يفعّل التجديد —
        // رقم أساس ونوع لسنة جديدة + وقعة تجديد، بنفس شروط ApplyRenewalAsync القائمة.
        bool renewed = false;
        if (!string.IsNullOrWhiteSpace(request?.RenewalFileNumber))
        {
            renewed = true;
            await _statusService.ApplyRenewalAsync(doc, request, executedLike: false, doc.CreatedById, ct);
        }

        // وقعة «تراجع» بسرد آلي موحّد + حقول الشطب/التجديد إن وُجدت (بلا حقول سير — عودة
        // خاصة لا تحمل كتبًا إلزامية).
        var narration = returnsToPartiallyExecuted
            ? "أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها وعاد منفذًا جزئيًا"
            : "أعيد السير به بعد موافاتنا بأموال للتنفيذ عليها";
        if (renewed)
            narration += $" وجدد الملف برقم {doc.RenewalFileNumber} نوع {doc.RenewalFileType} تاريخ {FreeDateParser.ToResponse(doc.RenewalDate)}";
        var details = new Dictionary<string, string> { ["revertNarration"] = narration };
        if (!returnsToPartiallyExecuted && doc.StruckOffDate is not null)
            DocumentStatusRules.CopyDetail(details, "struckOffDate", FreeDateParser.ToResponse(doc.StruckOffDate));
        if (renewed)
        {
            DocumentStatusRules.CopyDetail(details, "renewalFileNumber", doc.RenewalFileNumber);
            DocumentStatusRules.CopyDetail(details, "renewalFileType", doc.RenewalFileType);
            DocumentStatusRules.CopyDetail(details, "renewalDate", FreeDateParser.ToResponse(doc.RenewalDate));
            DocumentStatusRules.CopyDetail(details, "renewalYear", request?.RenewalYear?.ToString(CultureInfo.InvariantCulture));
        }

        var returned = await _writeGuard.WithConcurrencyGuardAsync(() => _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                Source = OccurrenceSourceCatalog.System,
                OccurrenceType = OccurrenceTypeCatalog.Revert,
                EventDate = DateTime.UtcNow,
                Details = DocumentStatusRules.SerializeDetails(details),
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = returnsToPartiallyExecuted
                ? "أعاد السير بالملف من «محال الى البداية» منفذًا جزئيًا"
                : "أعاد السير بالملف من «محال الى البداية» إلى المتداول";
            await _writeGuard.LogDocumentChangesAsync(returnBefore, doc, command.ActorName, "status", auditDetail, token);
            // ب4: عودة المناب الموروث-تريث تلقائيًا (D3) — عمليًا لا-عملية هنا (قرار 9 يحول
            // دون إنابات سارية على «محال»)، وتُستدعى للاتساق السلوكي مع التراجع.
            await DocumentStatusRules.ApplyDelegationInheritanceOrRecoveryAsync(
                doc, _documents, _delegations, _occurrences, _uow, _clock, _timeZone, token);
            return true;
        }, ct));

        // بلا تنبيهات مرآة عمدًا — مبرَّر بقرار 9 (لا إنابات سارية على «محال» حتى تُنبَّه).
        return returned;
    }
}

/// <summary>
/// «اعتبار الملف منفذًا كاملًا بهذا البيع» (نظام «طالبة تنفيذ»): إغلاق «منفذ جبريا (منفذ
/// جزئيا)» الذي فُعّل تلقائيًا بإتمام إنابة إلى «منفذ كاملا» — يُخصم من ملفٍ منفذ جزئيًا
/// وفيه إنابة منفذة، ويُلزم إدخال «تاريخ تحويل بدل المبيع للجهة العامة» (و«رقم الإشعار»
/// اختياريًا)، ويُسجَّل وقعة «منفذ جبريا» في وقوعات الملف. حينها فقط يدخل بدل الإنابة
/// ضمن «إحصاءات منفذ جبريا» مرة واحدة (عبر مسار DelegationSalesAmount القائم).
/// </summary>
public sealed record ConsiderExecutedByDelegationCommand(
    int DocumentId,
    Dictionary<string, string?> Fields,
    string? ActorName,
    long? Version = null) : ICommand<bool>;

public sealed class ConsiderExecutedByDelegationCommandHandler
    : CommandHandlerBase<ConsiderExecutedByDelegationCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDelegationRepository _delegations;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IHeadAlertService _alertService;

    public ConsiderExecutedByDelegationCommandHandler(
        IDocumentRepository documents,
        IDelegationRepository delegations,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IRepository<DocumentOccurrence> occurrences,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentDelegationService delegationService,
        IHeadAlertService alertService,
        IEnumerable<IPipelineBehavior<ConsiderExecutedByDelegationCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _delegations = delegations;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _occurrences = occurrences;
        _clock = clock;
        _timeZone = timeZone;
        _delegationService = delegationService;
        _alertService = alertService;
    }

    protected override async Task<bool> HandleCoreAsync(
        ConsiderExecutedByDelegationCommand command, CancellationToken ct)
    {
        var fields = command.Fields;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;
        DocumentText.EnsureNotPendingRegistration(doc, "اعتبار الملف منفذًا");
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        // حارس دفاعي (ب2 — F6): اعتبار ملف منفذًا كاملًا بهذا البيع إجراء خاص بالملف المنيب،
        // ولا يُطبَّق على ملف الإنابة. يقي الصفوف القديمة والطلبات المباشرة (النافذة تُخفي
        // الخيار عن المناب أصلًا).
        if (doc.SourceDelegationId != null)
            throw new ArgumentException("اعتبار الملف منفذًا كاملًا بهذا البيع إجراء خاص بالملف المنيب، ولا يُطبَّق على ملف الإنابة");

        // «اعتبار الملف منفذًا كاملًا بهذا البيع»: إغلاق «منفذ جبريا (منفذ جزئيا)» فحسب —
        // الحالة التي يُفعَّل بها المنيب تلقائيًا عند إتمام إنابته (أو ما يوازيها من ملفات
        // «منفذ جبريا» ذات إنابة منفذة). حينها فقط يدخل بدل الإنابة إحصاءات «منفذ جبريا».
        if (doc.ExecStatus != ExecutionStatusCatalog.ExecutedForcibly
            || doc.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
        {
            var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
            throw new ArgumentException(
                $"لا يمكن اعتبار الملف منفذًا كاملًا بهذا البيع من حالته الحالية «{ExecutionStatusCatalog.ToStateLabel(current)}»");
        }

        var executedDelegation = (await _delegations.ListBySourceAsync(command.DocumentId, ct))
            .FirstOrDefault(d => d.Status == DelegationStatusCatalog.Executed);
        if (executedDelegation is null)
            throw new ArgumentException("لا توجد إنابة منفذة للملف ليُعتبر منفذًا بهذا البيع");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, command.Version);

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var considerBefore = DocumentChangeTracker.Capture(doc);

        // «تاريخ تحويل بدل المبيع للجهة العامة» إلزامي (نص حر يُفسَّر ويُخزَّن زمنيًا)،
        // و«رقم الإشعار» اختياري — يدخلهما محامي المنيب من نافذة تغيير الحالة.
        var transferRaw = ArabicDigitNormalizer.Normalize(fields.GetValueOrDefault("forcedTransferDate"));
        if (string.IsNullOrWhiteSpace(transferRaw))
            throw new ArgumentException("يجب إدخال تاريخ تحويل بدل المبيع للجهة العامة على الأقل");
        doc.ForcibleTransferDate = DocumentValidator.ParseDateTime(transferRaw, "تاريخ تحويل بدل المبيع للجهة العامة");
        var notice = fields.GetValueOrDefault("forcedTransferNoticeNumber")?.Trim();
        doc.ForcibleTransferNoticeNumber = string.IsNullOrWhiteSpace(notice) ? null : notice;

        var details = new Dictionary<string, string>
        {
            ["execSubStatus"] = ExecutionStatusCatalog.SubFullyExecuted,
            ["forcedTransferDate"] = transferRaw,
        };
        DocumentStatusRules.CopyDetail(details, "forcedTransferNoticeNumber", doc.ForcibleTransferNoticeNumber);
        DocumentStatusRules.CopyDetail(details, "forcedExecutionDate", doc.ForcedExecutionDate);

        doc.ExecSubStatus = ExecutionStatusCatalog.SubFullyExecuted;
        doc.UpdatedAt = DateTime.UtcNow;

        var considered = await _writeGuard.WithConcurrencyGuardAsync(() => _tx.RunAsync(async token =>
        {
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // وقعة «منفذ جبريا» كاملة بحقولها — سجل زمني مستقل يبقى ظاهرًا في «وقوعات الملف».
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                Source = OccurrenceSourceCatalog.System,
                OccurrenceType = OccurrenceTypeCatalog.Forcible,
                EventDate = DateTime.UtcNow,
                Details = DocumentStatusRules.SerializeDetails(details),
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(considerBefore, doc, command.ActorName, "status",
                "اعتُبر الملف منفذًا كاملًا بهذا البيع (منفذ جبريا — منفذ كاملا)", token);
            // ب4: استرداد المناب لاعتبار المنيب منفذًا كاملًا (وقف إجراءات الإنابة).
            await DocumentStatusRules.ApplyDelegationInheritanceOrRecoveryAsync(
                doc, _documents, _delegations, _occurrences, _uow, _clock, _timeZone, token);
            // D3: المنابات المستردة هنا تحررت — أرشِف تنبيهاتها العالقة قبل إطلاق تنبيه الاسترداد.
            await DocumentStatusRules.CleanupPendingCompletionAlertsAsync(
                command.DocumentId, _delegations, _documents, _alertService, token);
            // مرآة: اكتمال تنفيذ المنيب يُنبه مناباته المعلقة (T5 — استرداد جبري).
            await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.FullyForcibly, token);
            return true;
        }, ct));
        return considered;
    }
}

/// <summary>
/// تعيين حالة وضع «منفذ عليه» (ExecutedStatusCatalog): منفذ/مشطوب. عند الشطب يُثبَّت
/// StruckOffDate (لحظة الشطب UTC) فيُخفى الملف من القوائم والتصدير ويظهر في صفحة المشطوبة.
/// إعادة الحالة إلى متداول (سلسلة فارغة) تُبقي StruckOffDate محفوظًا لعرضه بعد الإعادة.
/// </summary>
public sealed record UpdateExecutedStatusCommand(
    int DocumentId,
    string Status,
    ExecutedStatusRequest? Request,
    string? ActorName) : ICommand<bool>;

public sealed class UpdateExecutedStatusCommandHandler
    : CommandHandlerBase<UpdateExecutedStatusCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IDocumentStatusService _statusService;

    public UpdateExecutedStatusCommandHandler(
        IDocumentRepository documents,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IRepository<DocumentOccurrence> occurrences,
        IDocumentStatusService statusService,
        IEnumerable<IPipelineBehavior<UpdateExecutedStatusCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _occurrences = occurrences;
        _statusService = statusService;
    }

    protected override async Task<bool> HandleCoreAsync(
        UpdateExecutedStatusCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) تخص ملفات «الجهة العامة منفذ عليها» و«عرض وايداع» فقط");

        var status = (command.Status ?? string.Empty).Trim();
        if (!ExecutedStatusCatalog.ValidStatuses.Contains(status))
            throw new ArgumentException("حالة غير صالحة");

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var executedBefore = DocumentChangeTracker.Capture(doc);

        var current = doc.ExecutedStatus;
        // حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية: لا تُغيَّر إلى متداول ولا إلى مشطوب
        // (ويبقى الدخول مجددًا إلى «منفذ» ذاتها مسموحًا لتحديث حقول الحالة).
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed
            && current == ExecutedStatusCatalog.Executed
            && status != ExecutedStatusCatalog.Executed)
            throw new ArgumentException("حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية لا يمكن تغييرها");
        // «عرض وايداع» يُشطب من متداوله فقط؛ أما المنفذ فلا يُشطب بل يُعاد إلى متداول بكتاب السير بالملف.
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
            && current == ExecutedStatusCatalog.Executed
            && status == ExecutedStatusCatalog.StruckOff)
            throw new ArgumentException("«عرض وايداع» المنفذ لا يُشطب؛ يمكن إرجاعه إلى متداول بكتاب الجهة العامة بالسير بالملف");
        // «مشطوب → منفذ» مباشرة ممنوع في النافذة كما في التحرير: يجب المرور بالتجديد
        // إلى «متداول» أولًا (المبدأ 5) — وإلا حُفظت حالة «منفذ» بلا تجديد وبلا رقم أساس وبلا وقعة.
        if (ExecutedStatusCatalog.IsStruckOff(current) && status == ExecutedStatusCatalog.Executed)
            throw new ArgumentException("لا يمكن نقل ملف مشطوب إلى «منفذ» مباشرة — يجب إعادته أولًا إلى المتداول (تجديد)");

        // الإرجاع من «منفذ» إلى «متداول» في «عرض وايداع»: كتاب الجهة العامة بالسير بالملف إلزامي
        // (رقم وتاريخ الكتاب وورودهما)، ويُحفظ مع بقاء المبالغ المودعة، ويُسجَّل وقعة تراجع.
        // يُتحقق هنا قبل أي تعديل على حالة الملف كي لا تترك حالةُ فشلٍ أثرًا على السجل.
        var depositRevert = doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
            && current == ExecutedStatusCatalog.Executed
            && status == ExecutedStatusCatalog.None;
        Dictionary<string, string>? revertDetails = null;
        if (depositRevert)
        {
            var sayerFields = new Dictionary<string, string?>
            {
                ["sayerNumber"] = request?.SayerNumber,
                ["sayerDate"] = request?.SayerDate,
                ["sayerRegNumber"] = request?.SayerRegNumber,
                ["sayerRegDate"] = request?.SayerRegDate,
            };
            DocumentValidator.RequireField(sayerFields, "sayerNumber", "رقم كتاب الجهة العامة بالسير بالملف");
            DocumentValidator.RequireField(sayerFields, "sayerDate", "تاريخ كتاب الجهة العامة بالسير بالملف");
            DocumentValidator.RequireField(sayerFields, "sayerRegNumber", "رقم ورود كتاب بالسير بالملف");
            DocumentValidator.RequireField(sayerFields, "sayerRegDate", "تاريخ ورود كتاب بالسير بالملف");
            doc.SayerNumber = sayerFields["sayerNumber"];
            doc.SayerDate = sayerFields["sayerDate"];
            doc.SayerRegNumber = sayerFields["sayerRegNumber"];
            doc.SayerRegDate = sayerFields["sayerRegDate"];
            revertDetails = new Dictionary<string, string>();
            DocumentStatusRules.CopyDetail(revertDetails, "sayerNumber", doc.SayerNumber);
            DocumentStatusRules.CopyDetail(revertDetails, "sayerDate", doc.SayerDate);
            DocumentStatusRules.CopyDetail(revertDetails, "sayerRegNumber", doc.SayerRegNumber);
            DocumentStatusRules.CopyDetail(revertDetails, "sayerRegDate", doc.SayerRegDate);
        }

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, request?.Version);

        var wasStruckOff = ExecutedStatusCatalog.IsStruckOff(current);
        doc.ExecutedStatus = ExecutedStatusCatalog.IsStored(status) ? status : ExecutedStatusCatalog.None;
        // عند الدخول إلى «مشطوب» يُحدَّث تاريخ الشطب: بتاريخه المُرسَل إن وُجد وإلا للآن.
        // فلو عاد الملف إلى المتداول (مع إبقاء تاريخ الشطب السابق لعرضه بعد الإعادة) ثم شُطب
        // من جديد، فيجب أن يحمل الشطبُ الجديد تاريخَه الخاص لا تاريخ شطبه الأول.
        if (!wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.StruckOff)
        {
            var submitted = DocumentValidator.ParseDateTime(request?.StruckOffDate, "تاريخ الشطب");
            doc.StruckOffDate = submitted ?? DateTime.UtcNow;
        }
        // عند الدخول إلى «منفذ» تُحفظ حقول الحالة المقدَّمة فقط ولا تُمسّ المحفوظة سابقًا:
        // المبلغ وهو خاص بالصفين (تنفيذ/ايداع)، والوصف خاص بصفة «منفذ عليها»، وتاريخ الإيداع
        // خاص بصفة «عرض وايداع». الإعادة إلى منفذ بحقول فارغة تُبقي ما سبق تسجيله.
        if (doc.ExecutedStatus == ExecutedStatusCatalog.Executed)
        {
            // المبلغ المدفوع (حتى ثلاثة بعملاتها) خاص بالصفين (تنفيذ/ايداع): تُحفظ الخانة
            // المقدَّمة فقط بعملتها، ولا تُمسّ المحفوظة سابقًا في سواها. وعملة الخانة عائدة
            // لمنهج «كل مبلغ له عملة»: المقدَّمة، وإلا المحفوظة سابقًا، وإلا الافتراضية.
            if (request?.ExecutedPaidAmount is { } paidAmount)
            {
                doc.ExecutedPaidAmount = paidAmount;
                doc.ExecutedPaidCurrency = request.ExecutedPaidCurrency ?? doc.ExecutedPaidCurrency ?? "ليرة سورية";
            }
            if (request?.ExecutedPaidAmount2 is { } paidAmount2)
            {
                doc.ExecutedPaidAmount2 = paidAmount2;
                doc.ExecutedPaidCurrency2 = request.ExecutedPaidCurrency2 ?? doc.ExecutedPaidCurrency2 ?? "ليرة سورية";
            }
            if (request?.ExecutedPaidAmount3 is { } paidAmount3)
            {
                doc.ExecutedPaidAmount3 = paidAmount3;
                doc.ExecutedPaidCurrency3 = request.ExecutedPaidCurrency3 ?? doc.ExecutedPaidCurrency3 ?? "ليرة سورية";
            }
            if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Executed)
            {
                var description = (request?.ExecutedDescription ?? string.Empty).Trim();
                if (description.Length > 0)
                    doc.ExecutedDescription = description;
                var executionDate = DocumentValidator.ParseDateTime(request?.ExecutedExecutionDate, "تاريخ التنفيذ");
                if (executionDate is not null)
                    doc.ExecutedExecutionDate = executionDate;
            }
            else
            {
                doc.ExecutedDescription = null;
                doc.ExecutedExecutionDate = null;
            }
            if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            {
                // عند دخول «عرض وايداع» إلى «منفذ» تُضبط العلامة الدائمة «سبق تنفيذه» فلا يخرج
                // مبلغه المودع من الإحصاءات (عددًا ومبلغًا) حتى بعد عودته إلى المتداول.
                doc.WasDepositExecuted = true;
                var depositDate = DocumentValidator.ParseDateTime(request?.ExecutedDepositDate, "تاريخ ايداعه حساب الجهة العامة");
                if (depositDate is not null)
                    doc.ExecutedDepositDate = depositDate;
            }
        }

        return await _writeGuard.WithConcurrencyGuardAsync(() => _tx.RunAsync(async token =>
        {
            // العودة من مشطوب إلى متداول تستلزم تجديد الملف برقم ملف جديد لسنة الإعادة.
            if (wasStruckOff && doc.ExecutedStatus == ExecutedStatusCatalog.None)
                await _statusService.ApplyRenewalAsync(doc, request ?? new RenewalRequest(), true, doc.CreatedById, token);
            // الانتقال إلى مشطوب يُسجَّل وقعة شطب في سجل وقوعات الملف.
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await _statusService.AddStruckOffOccurrenceAsync(doc, doc.CreatedById, token);
            // الإرجاع من «منفذ» إلى «متداول» (عرض وايداع) يُسجَّل وقعة تراجع بحقول كتاب السير.
            else if (depositRevert)
                await _occurrences.AddAsync(new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.Revert,
                    EventDate = DateTime.UtcNow,
                    Details = revertDetails?.Count > 0 ? DocumentStatusRules.SerializeDetails(revertDetails) : null,
                    CreatedById = doc.CreatedById,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            var label = ExecutedStatusCatalog.ToLabel(doc.ExecutedStatus);
            var sideLabel = GeneralEntitySideCatalog.ToLabel(doc.GeneralEntitySide);
            var auditDetail = depositRevert
                ? $"أعاد «{sideLabel}» إلى المتداول بكتاب الجهة العامة بالسير بالملف"
                : $"حالة وضع «{sideLabel}»: {label}";
            await _writeGuard.LogDocumentChangesAsync(executedBefore, doc, command.ActorName, "executed-status", auditDetail, token);
            return true;
        }, ct));
    }
}

/// <summary>
/// إعادة ملف مشطوب إلى المتداول مع تجديد الملف برقم ملف جديد لسنة الإعادة (إلزامي):
/// يُطبَّق بيان التجديد ويُسجَّل رقم أساس لسنة الإعادة فيعود الملف بالرقم والنوع الجديدين.
/// </summary>
public sealed record RestoreStruckOffDocumentCommand(
    int DocumentId,
    RenewalRequest Renewal,
    string? ActorName) : ICommand<bool>;

public sealed class RestoreStruckOffDocumentCommandHandler
    : CommandHandlerBase<RestoreStruckOffDocumentCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IDocumentWriteGuard _writeGuard;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly IDocumentStatusService _statusService;
    private readonly IDocumentDelegationService _delegationService;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public RestoreStruckOffDocumentCommandHandler(
        IDocumentRepository documents,
        IDocumentWriteGuard writeGuard,
        ITransactionRunner tx,
        IUnitOfWork uow,
        IDocumentStatusService statusService,
        IDocumentDelegationService delegationService,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<RestoreStruckOffDocumentCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _writeGuard = writeGuard;
        _tx = tx;
        _uow = uow;
        _statusService = statusService;
        _delegationService = delegationService;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<bool> HandleCoreAsync(
        RestoreStruckOffDocumentCommand command, CancellationToken ct)
    {
        var renewal = command.Renewal;
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            return false;

        var executedLike = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide);
        var struckOff = executedLike
            ? ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus)
            : doc.ExecStatus == ExecutionStatusCatalog.StateStruckOff;
        if (executedLike && !struckOff)
            return false;
        if (!executedLike && !struckOff)
            throw new ArgumentException("فك الشطب يخص ملفًا مشطوبًا");

        // حارس المناب (ب3 — E2): ملفات الإنابة إذا شُطبت تعود إلى الدائرة المنيبة ويتوجب
        // تسطير إنابة جديدة — لا فك شطب للمناب (الاسترداد يصنع وقعة «استرداد» بدل الإعادة).
        if (doc.SourceDelegationId != null)
            throw new ArgumentException("ملفات الانابة في حال شطبت تعاد الى الدائرة المنيبة ويتوجب تسطير انابة جديدة");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, renewal?.Version);

        // لقطة ما قبل التغيير — لتوليد صفوف «حقل/قبل/بعد» في سجل التعديلات.
        var restoreBefore = DocumentChangeTracker.Capture(doc);

        // فك الشطب: العودة إلى متداول مع الإبقاء على تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
        if (executedLike)
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
        else
            doc.ExecStatus = ExecutionStatusCatalog.None;

        var restored = await _writeGuard.WithConcurrencyGuardAsync(() => _writeGuard.WithNumberGuardAsync(() => _tx.RunAsync(async token =>
        {
            // إعادة الملف المشطوب من صفحة «الملفات المشطوبة» تُعد تجديدًا: رقم الملف الجديد
            // إلزامي (ومعه سنة الإعادة في نظام «طالبة تنفيذ»)، ويُسجَّل رقم أساس لسنة الإعادة
            // فيعود الملف بالرقم والنوع الجديدين.
            await _statusService.ApplyRenewalAsync(doc, renewal, executedLike, doc.CreatedById, token);
            // RF-009 بتوقيع الدائرة (البند 34): مطابقة FK مباشرة.
            await _writeGuard.EnsureNumberUniqueByCircuitAsync(doc.Id, doc.ExecutionCircuitId, doc.CourtNorm,
                doc.RenewalFileNumber, doc.RenewalFileType,
                executedLike
                    ? ServerClock.CurrentYear(_clock, _timeZone).ToString(CultureInfo.InvariantCulture)
                    : renewal?.RenewalYear?.ToString(CultureInfo.InvariantCulture), token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(restoreBefore, doc, command.ActorName, "restore-struck-off",
                "أعاد ملفًا مشطوبًا إلى المتداول مع تجديد رقم الملف", token);
            // مرآة: فك الشطب «تغيّر حالة المنيب» يُنبه مناباته المعلقة بالصيغة العامة.
            // S1 يجعل وجود إنابات معلقة على مصدر مشطوب شبه معدوم — الاحتفاظ عام.
            await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.Generic, token);
            return true;
        }, ct), renewal?.RenewalFileNumber));
        return restored;
    }
}
