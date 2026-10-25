using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة الحالات (مستخرجة من `DocumentService` — المرحلة 4، نقل ميكانيكي بلا سلوك):
/// آلة حالات نظام «طالبة تنفيذ» + حالات وضع «منفذ عليه»/«عرض وايداع» + التجديد/الشطب
/// + توريث/استرداد المناب. الاسم الموحّد `IDocumentStatusService
/// (تقاعُد اسم `StatusTransitionService` المؤجل).
/// الاعتمادات الدقيقة: `_documents` و`_occurrences` و`_baseNumbers`
/// و`_alertService` (استثناء البنية التحتية — القاعدة 4) و`_delegations` و`_tx` و`_uow`
/// والساعة/المنطقة + `IDocumentDelegationService` (تنبيهات الإنابة) + `IDocumentWriteGuard`
/// (الحراس المشتركة — أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة).
/// ثابت ملكية المعاملة: معاملة `_tx.RunAsync` واحدة لكل نقطة دخول عامة —
/// المساعدات (`ApplyRenewalAsync`، `AddStruckOffOccurrenceAsync`، المزامنة) تنضم
/// لمعاملة المستدعي، وأرشفة/إطلاق التنبيهات داخل معاملة الحالة نفسها (ذرّية مع الحدث).
/// </summary>
public sealed class DocumentStatusService : IDocumentStatusService
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<DocumentBaseNumber> _baseNumbers;
    private readonly IHeadAlertService _alertService;
    private readonly IDelegationRepository _delegations;
    private readonly ITransactionRunner _tx;
    private readonly IUnitOfWork _uow;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IDocumentWriteGuard _writeGuard;

    public DocumentStatusService(
        IDocumentRepository documents,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<DocumentBaseNumber> baseNumbers,
        IHeadAlertService alertService,
        IDelegationRepository delegations,
        ITransactionRunner tx,
        IUnitOfWork uow,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IDocumentDelegationService delegationService,
        IDocumentWriteGuard writeGuard)
    {
        _documents = documents;
        _occurrences = occurrences;
        _baseNumbers = baseNumbers;
        _alertService = alertService;
        _delegations = delegations;
        _tx = tx;
        _uow = uow;
        _clock = clock;
        _timeZone = timeZone;
        _delegationService = delegationService;
        _writeGuard = writeGuard;
    }

    /// <summary>
    /// D3: أرشفة تنبيهات الإنابة لكل مسار يجعل مناب مسجلة أصولًا نهائيًا —
    /// استرداد (تسوية/جبرية كاملة → «مسترد») أو شطب يدوي للمناب. تعمل داخل
    /// معاملة المتصل، وidempotent (إعادة الأرشفة آمنة). البوابة Registered حصرًا:
    /// المعلّقة/المحالة لها تنبيهات اعتماد لا تُمس.
    /// </summary>
    private async Task CleanupPendingCompletionAlertsAsync(int documentId, CancellationToken ct)
    {
        // (1) المنيب: إناباته المسجلة التي صار منابها نهائيًا (أي فرع من IsTargetTerminal —
        // مشطوب بجهتيه/مسترد/منفذ إنابة — لا إعادة تعريف جزئية هنا).
        var recovered = (await _delegations.ListBySourceAsync(documentId, ct))
            .Where(d => d.Status == DelegationStatusCatalog.Registered
                && d.TargetDocument is not null
                && DelegationActivityPolicy.IsTargetTerminal(d.TargetDocument))
            .Select(d => d.Id)
            .ToList();
        foreach (var id in recovered)
            await _alertService.ArchiveByDelegationAsync(
                id,
                HeadAlertKindCatalog.DelegationKinds,
                "delegation_target_terminal",
                ct);
        // (2) الملف نفسه مناب صار نهائيًا يدويًا: إنابته المسجلة تحررت.
        var own = await _delegations.FindByTargetAsync(documentId, ct);
        if (own is not null && own.Status == DelegationStatusCatalog.Registered)
        {
            var targetDoc = await _documents.GetByIdAsync(documentId, ct);
            if (targetDoc is not null && DelegationActivityPolicy.IsTargetTerminal(targetDoc))
                await _alertService.ArchiveByDelegationAsync(
                    own.Id,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_target_terminal",
                    ct);
        }
    }

    public async Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        // حراس NeedsRegistration الصريحة (البند 32): تُمنع على المعلق — تغيير الحالة حيث تلزم الهوية.
        DocumentText.EnsureNotPendingRegistration(doc, "تغيير الحالة");
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        status = (status ?? string.Empty).Trim();
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
            var delegations = await _delegations.ListBySourceAsync(documentId, ct);
            if (delegations.Any(DelegationActivityPolicy.IsLifecycleActive))
                throw new ArgumentException("لا يجوز شطب ملف فيه انابة سارية");
        }

        // حارس الإحالة إلى البداية (قرار 9 — نمط حارس الشطب S1): لا يجوز إحالة ملف فيه
        // إنابة سارية إلى البداية، بمن فيه القادم من «منفذ جبريا» (تسوية إنابة مُتممة
        // كبيعٍ مكتمل ليست إنابة سارية). بعد العودة إلى المتداول يصبح قابلاً للتسطير طبيعيًا
        // — الحظر على الدخول فقط ولا يُورَّث.
        if (status == ExecutionStatusCatalog.ReferredToStart)
        {
            var delegations = await _delegations.ListBySourceAsync(documentId, ct);
            if (delegations.Any(DelegationActivityPolicy.IsLifecycleActive))
                throw new ArgumentException("لا يجوز إحالة ملف فيه إنابة سارية إلى البداية");
        }

        // آلة الحالات: تُمنع الانتقالات غير المسموحة من الحالة الحالية صراحةً.
        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.IsAllowedStatusChange(current, status))
            throw new ArgumentException(
                $"لا يمكن الانتقال من الحالة «{ExecutionStatusCatalog.ToStateLabel(current)}» إلى «{ExecutionStatusCatalog.ToStatusLabel(status)}»");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, version);

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
                ApplyCollectedAmounts(doc, fields, details);
                ApplySoldAssets(doc, fields, details);
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                DocumentValidator.RequireField(fields, "forcedExecutionDate", "تاريخ قرار الإحالة القطعية");
                doc.ForcedExecutionDate = fields.GetValueOrDefault("forcedExecutionDate");
                CopyDetail(details, "forcedExecutionDate", doc.ForcedExecutionDate);
                break;
            case ExecutionStatusCatalog.ExecutedBySettlement:
                DocumentValidator.RequireField(fields, "baraetNumber", "رقم كتاب براءة الذمة");
                DocumentValidator.RequireField(fields, "baraetDate", "تاريخ كتاب براءة الذمة");
                doc.BaraetNumber = fields.GetValueOrDefault("baraetNumber");
                doc.BaraetDate = fields.GetValueOrDefault("baraetDate");
                doc.BaraetRegNumber = fields.GetValueOrDefault("baraetRegNumber");
                doc.BaraetRegDate = fields.GetValueOrDefault("baraetRegDate");
                CopyDetail(details, "baraetNumber", doc.BaraetNumber);
                CopyDetail(details, "baraetDate", doc.BaraetDate);
                CopyDetail(details, "baraetRegNumber", doc.BaraetRegNumber);
                CopyDetail(details, "baraetRegDate", doc.BaraetRegDate);
                ApplyCollectedAmounts(doc, fields, details);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                ClearForcibleTransferFields(doc);
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
                CopyDetail(details, "tarithNumber", doc.TarithNumber);
                CopyDetail(details, "tarithDate", doc.TarithDate);
                CopyDetail(details, "tarithRegNumber", doc.TarithRegNumber);
                CopyDetail(details, "tarithRegDate", doc.TarithRegDate);
                ClearBaraetFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                ClearForcibleTransferFields(doc);
                doc.ExecSubStatus = null;
                ClearCollectedFields(doc);
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
                CopyDetail(details, "noFundsDemandNumber", doc.NoFundsDemandNumber);
                CopyDetail(details, "noFundsDemandDate",
                    doc.NoFundsDemandDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                CopyDetail(details, "startReferralNumber", doc.StartReferralNumber);
                CopyDetail(details, "startReferralDate",
                    doc.StartReferralDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
                // تطهير منشطّر حسب المصدر (قرار 11): الدخول من جزئيا يُبقي عائلة الجبريا
                // ظاهرة (ExecSubStatus/Collected*/SoldAssetIds/ForcedExecution* — يعود منها
                // عبر نقطة العودة إلى «منفذ جزئيًا» بلا عمود جديد)، ودخولا متداول/تريث على
                // مسح العائلات كاملاً. لا تُمس حقول Renewal*/StruckOffDate في الحالين،
                // وأداة ClearReferredToStartFields خاصة بالعودة (B4) لا بالدخول.
                if (current == ExecutionStatusCatalog.ExecutedForcibly)
                {
                    ClearBaraetFields(doc);
                    ClearTarithFields(doc);
                    ClearSayerFields(doc);
                }
                else
                {
                    ClearBaraetFields(doc);
                    ClearTarithFields(doc);
                    ClearSayerFields(doc);
                    ClearForcedExecutionField(doc);
                    ClearForcibleTransferFields(doc);
                    ClearCollectedFields(doc);
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
                ClearBaraetFields(doc);
                ClearTarithFields(doc);
                ClearSayerFields(doc);
                ClearForcedExecutionField(doc);
                ClearForcibleTransferFields(doc);
                doc.ExecSubStatus = null;
                ClearCollectedFields(doc);
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
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = status == ExecutionStatusCatalog.StateStruckOff
                ? $"حالة {ExecutionStatusCatalog.StateStruckOff}"
                : $"حالة {ExecutionStatusCatalog.ToLabel(ExecutionStatusCatalog.Classify(status))}";
            await _writeGuard.LogDocumentChangesAsync(statusBefore, doc, actorName, "status", auditDetail, token);
            // ب4: توريث «تريث»/استرداد المناب يتبَع حالة المنيب الجديدة — داخل معاملة الحالة نفسها.
            await ApplyDelegationInheritanceOrRecoveryAsync(doc, token);
            // D3: نهائية مناب (استرداد/شطب يدوي) تؤرشف تنبيهات الإنابة العالقة —
            // قبل إطلاق تنبيهات الحالة الجديدة كي لا تُؤرشف التنبيهات الجديدة.
            await CleanupPendingCompletionAlertsAsync(documentId, token);
            // مرآة: تغيّر حالة المنيب يُنبه مناباته المعلقة.
            // الصيغ T2/T4/T5 فقط؛ جزئيا (N1) ومشطوب (N7) وسواها كبت بلا تنبيه.
            var statusAlertKind = DocumentDelegationService.DelegationStatusAlertKindFor(doc);
            if (statusAlertKind is not null)
                await _delegationService.FireStatusChangeAlertsAsync(doc, statusAlertKind.Value, token);
            return true;
        }, ct));
        return statusUpdated;
    }

    public async Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
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
        _writeGuard.EnsureConcurrency(doc, version);

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
        CopyDetail(details, "sayerNumber", doc.SayerNumber);
        CopyDetail(details, "sayerDate", doc.SayerDate);
        CopyDetail(details, "sayerRegNumber", doc.SayerRegNumber);
        CopyDetail(details, "sayerRegDate", doc.SayerRegDate);

        // العودة إلى المتداول: تُصفَّر حالة التنفيذ وحقولها مع الإبقاء على حقول «السير بالملف»
        // محفوظةً لتبقى ظاهرة في «وقوعات الملف» (لقطة الحقوق في الوقعة أسفل).
        doc.ExecStatus = ExecutionStatusCatalog.None;
        doc.ExecSubStatus = null;
        ClearCollectedFields(doc);
        ClearBaraetFields(doc);
        ClearTarithFields(doc);
        ClearForcedExecutionField(doc);
        ClearForcibleTransferFields(doc);
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
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(revertBefore, doc, actorName, "status",
                "تراجع عن الحالة وعاد الملف إلى المتداول", token);
            // ب4: عودة المناب الموروث-تريث تلقائيًا عند تراجع المنيب من «تريث» (D3).
            var targetsReverted = await ApplyDelegationInheritanceOrRecoveryAsync(doc, token);
            // مرآة: عودة المناب الفعلية تُنبه (T3 — متابعة السير بالملف). أما التراجع من
            // تسوية/جبريا فلا يُرجع منابًا (المسترد لا يُرجع — C1)، فلا يُطلق تنبيه
            // «انتهاء حالة التريث» على مناب مسترد (ضلال).
            if (targetsReverted)
                await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.Returned, token);
            return true;
        }, ct));
        return reverted;
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
    public async Task<bool> ReturnFromReferredToStartAsync(int documentId, ReturnReferredToStartRequest request, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
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
            ClearCollectedFields(doc);
            ClearBaraetFields(doc);
            ClearTarithFields(doc);
            ClearForcedExecutionField(doc);
            ClearForcibleTransferFields(doc);
            doc.SoldAssetIds = null;
        }

        // حقول «محال الى البداية» الأربعة تُمسح في الحالين (أداة العودة فقط — الدخول لا يمسح).
        ClearReferredToStartFields(doc);

        // «تاريخ الشطب» الاختياري خاص بنتيجة-متداول فقط ويُتجاهل في نتيجة-جزئيا.
        if (!returnsToPartiallyExecuted)
            doc.StruckOffDate = DocumentValidator.ParseDateTime(request?.StruckOffDate, "تاريخ الشطب");

        // حقول التجديد موحّدة في المسارين (قرار 12): رقم الملف الجديد يفعّل التجديد —
        // رقم أساس ونوع لسنة جديدة + وقعة تجديد، بنفس شروط ApplyRenewalAsync القائمة.
        bool renewed = false;
        if (!string.IsNullOrWhiteSpace(request?.RenewalFileNumber))
        {
            renewed = true;
            await ApplyRenewalAsync(doc, request, executedLike: false, doc.CreatedById, ct);
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
            CopyDetail(details, "struckOffDate", FreeDateParser.ToResponse(doc.StruckOffDate));
        if (renewed)
        {
            CopyDetail(details, "renewalFileNumber", doc.RenewalFileNumber);
            CopyDetail(details, "renewalFileType", doc.RenewalFileType);
            CopyDetail(details, "renewalDate", FreeDateParser.ToResponse(doc.RenewalDate));
            CopyDetail(details, "renewalYear", request?.RenewalYear?.ToString(CultureInfo.InvariantCulture));
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
                Details = SerializeDetails(details),
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = returnsToPartiallyExecuted
                ? "أعاد السير بالملف من «محال الى البداية» منفذًا جزئيًا"
                : "أعاد السير بالملف من «محال الى البداية» إلى المتداول";
            await _writeGuard.LogDocumentChangesAsync(returnBefore, doc, actorName, "status", auditDetail, token);
            // ب4: عودة المناب الموروث-تريث تلقائيًا (D3) — عمليًا لا-عملية هنا (قرار 9 يحول
            // دون إنابات سارية على «محال»)، وتُستدعى للاتساق السلوكي مع التراجع.
            await ApplyDelegationInheritanceOrRecoveryAsync(doc, token);
            return true;
        }, ct));

        // بلا تنبيهات مرآة عمدًا — مبرَّر بقرار 9 (لا إنابات سارية على «محال» حتى تُنبَّه).
        return returned;
    }

    public async Task<bool> ConsiderExecutedByDelegationAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default, long? version = null)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
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

        var executedDelegation = (await _delegations.ListBySourceAsync(documentId, ct))
            .FirstOrDefault(d => d.Status == DelegationStatusCatalog.Executed);
        if (executedDelegation is null)
            throw new ArgumentException("لا توجد إنابة منفذة للملف ليُعتبر منفذًا بهذا البيع");

        // RF-010: فحص التزامن المبكر (409 ودي) + زيادة العدّاد وشبكة السباق.
        _writeGuard.EnsureConcurrency(doc, version);

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
        CopyDetail(details, "forcedTransferNoticeNumber", doc.ForcibleTransferNoticeNumber);
        CopyDetail(details, "forcedExecutionDate", doc.ForcedExecutionDate);

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
                Details = SerializeDetails(details),
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(considerBefore, doc, actorName, "status",
                "اعتُبر الملف منفذًا كاملًا بهذا البيع (منفذ جبريا — منفذ كاملا)", token);
            // ب4: استرداد المناب لاعتبار المنيب منفذًا كاملًا (وقف إجراءات الإنابة).
            await ApplyDelegationInheritanceOrRecoveryAsync(doc, token);
            // D3: المنابات المستردة هنا تحررت — أرشِف تنبيهاتها العالقة قبل إطلاق تنبيه الاسترداد.
            await CleanupPendingCompletionAlertsAsync(documentId, token);
            // مرآة: اكتمال تنفيذ المنيب يُنبه مناباته المعلقة (T5 — استرداد جبري).
            await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.FullyForcibly, token);
            return true;
        }, ct));
        return considered;
    }

    public async Task<bool> UpdateExecutedStatusAsync(int documentId, string status, string? actorName, CancellationToken ct = default)
        => await UpdateExecutedStatusAsync(documentId, status, null, actorName, ct);

    public async Task<bool> UpdateExecutedStatusAsync(int documentId, string status, ExecutedStatusRequest? request, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (!GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة وضع (متداول/منفذ/مشطوب) تخص ملفات «الجهة العامة منفذ عليها» و«عرض وايداع» فقط");

        status = (status ?? string.Empty).Trim();
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
            CopyDetail(revertDetails, "sayerNumber", doc.SayerNumber);
            CopyDetail(revertDetails, "sayerDate", doc.SayerDate);
            CopyDetail(revertDetails, "sayerRegNumber", doc.SayerRegNumber);
            CopyDetail(revertDetails, "sayerRegDate", doc.SayerRegDate);
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
                await ApplyRenewalAsync(doc, request ?? new RenewalRequest(), true, doc.CreatedById, token);
            // الانتقال إلى مشطوب يُسجَّل وقعة شطب في سجل وقوعات الملف.
            else if (!wasStruckOff && ExecutedStatusCatalog.IsStruckOff(doc.ExecutedStatus))
                await AddStruckOffOccurrenceAsync(doc, doc.CreatedById, token);
            // الإرجاع من «منفذ» إلى «متداول» (عرض وايداع) يُسجَّل وقعة تراجع بحقول كتاب السير.
            else if (depositRevert)
                await _occurrences.AddAsync(new DocumentOccurrence
                {
                    DocumentId = doc.Id,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.Revert,
                    EventDate = DateTime.UtcNow,
                    Details = revertDetails?.Count > 0 ? SerializeDetails(revertDetails) : null,
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
            await _writeGuard.LogDocumentChangesAsync(executedBefore, doc, actorName, "executed-status", auditDetail, token);
            return true;
        }, ct));
    }

    public async Task<bool> RestoreStruckOffAsync(int documentId, string? actorName, CancellationToken ct = default)
        => await RestoreStruckOffAsync(documentId, new RenewalRequest(), actorName, ct);

    public async Task<bool> RestoreStruckOffAsync(int documentId, RenewalRequest renewal, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
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
            await ApplyRenewalAsync(doc, renewal, executedLike, doc.CreatedById, token);
            // RF-009 بتوقيع الدائرة (البند 34): مطابقة FK مباشرة.
            await _writeGuard.EnsureNumberUniqueByCircuitAsync(doc.Id, doc.ExecutionCircuitId, doc.CourtNorm,
                doc.RenewalFileNumber, doc.RenewalFileType,
                executedLike
                    ? ServerClock.CurrentYear(_clock, _timeZone).ToString(CultureInfo.InvariantCulture)
                    : renewal?.RenewalYear?.ToString(CultureInfo.InvariantCulture), token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _writeGuard.LogDocumentChangesAsync(restoreBefore, doc, actorName, "restore-struck-off",
                "أعاد ملفًا مشطوبًا إلى المتداول مع تجديد رقم الملف", token);
            // مرآة: فك الشطب «تغيّر حالة المنيب» يُنبه مناباته المعلقة بالصيغة العامة.
            // S1 يجعل وجود إنابات معلقة على مصدر مشطوب شبه معدوم — الاحتفاظ عام.
            await _delegationService.FireStatusChangeAlertsAsync(doc, DelegationStatusAlertKind.Generic, token);
            return true;
        }, ct), renewal?.RenewalFileNumber));
        return restored;
    }

    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    public async Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct)
    {
        var number = renewal?.RenewalFileNumber?.Trim();
        if (string.IsNullOrEmpty(number))
            throw new ArgumentException("رقم الملف الجديد مطلوب عند إعادة الملف المشطوب");
        if (number.Length > 100)
            throw new ArgumentException("رقم الملف الجديد يتجاوز الطول المسموح");

        // سنة الإعادة: يحددها المستخدم في نظام «طالبة تنفيذ» (إلزامية)، وافتراضية للعام
        // الحالي في صفة «منفذ عليها» للاتساق مع السلوك القائم — مع رفض دفاعي للسنة المخالفة
        // بدل تجاهلها بصمت (لا اشتقاق من تاريخ التجديد — سلوك جديد غير مقرر).
        int year;
        if (executedLike)
        {
            if (renewal?.RenewalYear is { } hiddenYear && hiddenYear != ServerClock.CurrentYear(_clock, _timeZone))
                throw new ArgumentException("سنة الإعادة لعائلة «منفذ عليها/عرض وايداع» هي سنة اليوم الحالية فقط");
            year = ServerClock.CurrentYear(_clock, _timeZone);
        }
        else
        {
            if (renewal?.RenewalYear is not { } enteredYear)
                throw new ArgumentException("سنة الإعادة مطلوبة عند إعادة الملف المشطوب");
            if (enteredYear < 1900 || enteredYear > 2100)
                throw new ArgumentException("سنة الإعادة غير صالحة");
            DocumentValidator.ValidateFileYearNotFuture(enteredYear.ToString(CultureInfo.InvariantCulture), ServerClock.CurrentYear(_clock, _timeZone));
            year = enteredYear;
        }

        var type = renewal?.RenewalFileType?.Trim();
        if (!string.IsNullOrEmpty(type) && type.Length > 100)
            throw new ArgumentException("نوع الملف الجديد يتجاوز الطول المسموح");

        var receiptNumber = renewal?.RenewalFileReceiptNumber?.Trim();
        if (!string.IsNullOrEmpty(receiptNumber) && receiptNumber.Length > 200)
            throw new ArgumentException("رقم ورود اخطار التجديد يتجاوز الطول المسموح");

        doc.RenewalFileNumber = number;
        doc.RenewalFileReceiptNumber = string.IsNullOrEmpty(receiptNumber) ? null : receiptNumber;
        doc.RenewalFileReceiptDate = DocumentValidator.ParseDateTime(renewal?.RenewalFileReceiptDate, "تاريخ ورود اخطار التجديد");
        doc.RenewalDate = DocumentValidator.ParseDateTime(renewal?.RenewalDate, "تاريخ التجديد");
        doc.RenewalFileType = string.IsNullOrEmpty(type) ? doc.FileType : type;
        // تطابق سنة الإعادة المصرّحة مع سنة تاريخ التجديد عندما يُقدَّمان معًا في نظام
        // «طالبة تنفيذ» — قبول تاريخٍ بسنة مخالفةٍ للسنة المصرّحة خطأٌ صامت.
        if (!executedLike && doc.RenewalDate is { } renewalDate && renewalDate.Year != year)
            throw new ArgumentException("سنة الإعادة لا تطابق سنة تاريخ التجديد");
        // النوع الجديد إن وُجد يُطبَّق على نوع الملف الظاهر.
        if (!string.IsNullOrEmpty(type))
            doc.FileType = type;

        // يعود الملف برقم سنة الإعادة: سجل جديد دائمًا يُلحق بسجلات السنوات السابقة فيظهر عبر
        // EffectiveFileIdentity (الأحدث Year ثم CreatedAt) — والمعتبر هو الأحدث CreatedAt لنفس السنة.
        await _baseNumbers.AddAsync(new DocumentBaseNumber
        {
            DocumentId = doc.Id,
            Year = year,
            BaseNumber = number,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);

        // إلحاق الرقم الجديد لنص البحث القائم (إلحاق لا إعادة بناء) ليُلتقط البحث.
        doc.SearchText = DocumentSearchTextBuilder.Append(doc.SearchText, number);

        // سجل وقعة التجديد في «وقوعات الملف»: الرقم الجديد والنوع وسنة الإعادة
        // وورود اخطار التجديد — ضمن المعاملة نفسها فلا يضيع السجل عند فشل الحفظ.
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
            Source = OccurrenceSourceCatalog.System,
            OccurrenceType = OccurrenceTypeCatalog.Renewal,
            EventDate = doc.RenewalDate,
            FileNumber = number,
            FileType = string.IsNullOrEmpty(type) ? null : type,
            Year = year,
            ReceiptNumber = doc.RenewalFileReceiptNumber,
            ReceiptDate = doc.RenewalFileReceiptDate,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

    /// <summary>
    /// تسجيل وقعة الشطب في «وقوعات الملف» عند انتقال ملف «منفذ عليه»/«عرض وايداع»
    /// إلى الحالة «مشطوب»: تاريخ الشطب المحفوظ في المستند والرقم الفعّال وقت الشطب
    /// (آخر رقم أساس ≤ سنة الشطب — المحلل المركزي — وإلا رقم الملف الأصلي) ونوع الملف
    /// وسنة الشطب — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    public async Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct)
    {
        var struckOffYear = doc.StruckOffDate?.Year ?? ServerClock.CurrentYear(_clock, _timeZone);
        string? effectiveNumber = EffectiveFileIdentity.Number(doc, struckOffYear) ?? string.Empty;
        string? fileType = (doc.FileType ?? string.Empty).Trim();
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
            Source = OccurrenceSourceCatalog.System,
            OccurrenceType = OccurrenceTypeCatalog.StruckOff,
            EventDate = doc.StruckOffDate,
            FileNumber = string.IsNullOrEmpty(effectiveNumber) ? null : effectiveNumber.Trim(),
            FileType = string.IsNullOrEmpty(fileType) ? null : fileType,
            Year = doc.StruckOffDate?.Year,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

    // ── ب4: توريث/استرداد المناب عند تغيير حالة المنيب ─────────────────────────────

    /// <summary>
    /// ب4 — «الملف المناب يلحق المنيب» في تغيير الحالة (التوريث/الاسترداد)، يُستدعى داخل
    /// معاملة تغيير حالة المنيب نفسها (UpdateStatusAsync وRevertStatusAsync
    /// وConsiderExecutedByDelegationAsync) بعد أن حمل doc حالته الجديدة:
    ///  - منيب → «تريث»: المناب غير النهائي (عدا الدرافت C5) يرث كتاب التريث (Tarith*)
    ///    مع وقعة «تريث» (بلا تكرار C6)، ويُصفَّر سائر حقول عائلة الحالات الأخرى وبلا مبالغ (ب6).
    ///  - منيب → «منفذ بالتسوية»: كل مناب غير نهائي → «مسترد» (وقعة «استرداد» بتفاصيل F2).
    ///  - منيب → «منفذ جبريا/كاملا» (يدوي أو عبر ConsiderExecutedByDelegationAsync):
    ///    كل مناب غير نهائي → «مسترد» بوقعة «استرداد» (فرع تفاصيل جبرية).
    ///  - منيب → «منفذ جبريا/جزئيا» أو «مشطوب»: بلا أثر (N1/N7).
    ///  - منيب يتراجع إلى «متداول»: المناب الموروث-تريث وحده يعود تلقائيًا (D3) بوقعة
    ///    «تراجع» تُنسخ فيها حقول كتاب السير إلى التفاصيل فقط، وسائر المناب (مسترد/نهائي)
    ///    يُتخطى بلا مخرج (C1).
    /// يُتخطى دائمًا: النهائي (منفذ إنابة)، المشطوب، المسترد، والدرافت.
    /// تُرجع «true» إن غُيّر منابٌ واحدٌ على الأقل (لتنبيه التراجع T3: لا يُطلق إلا عند عودة فعلية).
    /// </summary>
    private async Task<bool> ApplyDelegationInheritanceOrRecoveryAsync(Document source, CancellationToken token)
    {
        var state = source.ExecStatus;
        var subStatus = source.ExecSubStatus;
        var actionable = state == ExecutionStatusCatalog.Deferred
            || state == ExecutionStatusCatalog.ExecutedBySettlement
            || (state == ExecutionStatusCatalog.ExecutedForcibly && subStatus == ExecutionStatusCatalog.SubFullyExecuted)
            || state == ExecutionStatusCatalog.None;
        if (!actionable)
            return false;

        var delegations = await _delegations.ListPendingBySourceWithTargetsAsync(source.Id, token);
        if (delegations.Count == 0)
            return false;

        // «رقم أساس المنيب» لتفاصيل وقعة الاسترداد (F2) — الرقم الفعّال الحالي أو الأصلي.
        var sourceFileNumber = EffectiveFileIdentity.Number(source, ServerClock.CurrentYear(_clock, _timeZone));

        var changed = false;
        foreach (var delegation in delegations)
        {
            var target = delegation.TargetDocument;
            if (target is null || ExcludedFromDelegationInheritance(target))
                continue;

            DocumentOccurrence? occurrence = null;
            switch (state)
            {
                case ExecutionStatusCatalog.Deferred:
                    // idempotency (C6): نسخ كتاب التريث دائمًا مع منع الوقعة المكررة فقط.
                    if (target.ExecStatus != ExecutionStatusCatalog.Deferred)
                        occurrence = DelegationTargetOccurrence(target, OccurrenceTypeCatalog.Deferred, DeferredInheritanceDetails(source));
                    InheritDeferredInto(source, target);
                    break;
                case ExecutionStatusCatalog.ExecutedBySettlement:
                    RecoverTarget(target);
                    occurrence = DelegationTargetOccurrence(target, OccurrenceTypeCatalog.Recovered, RecoveryDetails(source, forcibly: false, sourceFileNumber));
                    break;
                case ExecutionStatusCatalog.ExecutedForcibly when subStatus == ExecutionStatusCatalog.SubFullyExecuted:
                    RecoverTarget(target);
                    occurrence = DelegationTargetOccurrence(target, OccurrenceTypeCatalog.Recovered, RecoveryDetails(source, forcibly: true, sourceFileNumber));
                    break;
                case ExecutionStatusCatalog.None:
                    // التراجع: المناب الموروث-تريث وحده يعود تلقائيًا (D3) بوقعة «تراجع».
                    if (target.ExecStatus != ExecutionStatusCatalog.Deferred)
                        continue;
                    RevertTargetStatus(target);
                    occurrence = DelegationTargetOccurrence(target, OccurrenceTypeCatalog.Revert, SerializeDetails(RevertBookDetails(source)));
                    break;
                default:
                    continue;
            }

            changed = true;
            _documents.Update(target);
            if (occurrence is not null)
                await _occurrences.AddAsync(occurrence, token);
            await _uow.SaveChangesAsync(token);
        }

        return changed;
    }

    /// <summary>هل يُتخطى الملف المناب في التوريث/الاسترداد؟ — النهائي (منفذ إنابة)، المشطوب، المسترد، الدرافت.</summary>
    private static bool ExcludedFromDelegationInheritance(Document target) =>
        target.IsDraft
        || target.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
        || target.ExecStatus == ExecutionStatusCatalog.StateStruckOff
        || target.ExecStatus == ExecutionStatusCatalog.Recovered
        || ExecutedStatusCatalog.IsStruckOff(target.ExecutedStatus);

    /// <summary>وقعة تغيير حالة على الملف المناب (تُسجَّل ضمن معاملة المنيب نفسها).</summary>
    private static DocumentOccurrence DelegationTargetOccurrence(Document target, string occurrenceType, string? details) => new()
    {
        DocumentId = target.Id,
        Source = OccurrenceSourceCatalog.System,
        OccurrenceType = occurrenceType,
        EventDate = DateTime.UtcNow,
        Details = details,
        CreatedById = target.CreatedById,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>نسخ كتاب التريث من المنيب إلى المناب + تعليق الحالة + مسح حقول العائلة الأخرى (بلا مبالغ ب6).</summary>
    private static void InheritDeferredInto(Document source, Document target)
    {
        target.TarithNumber = source.TarithNumber;
        target.TarithDate = source.TarithDate;
        target.TarithRegNumber = source.TarithRegNumber;
        target.TarithRegDate = source.TarithRegDate;
        ClearBaraetFields(target);
        ClearSayerFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        target.SoldAssetIds = null;
        target.ExecStatus = ExecutionStatusCatalog.Deferred;
    }

    /// <summary>إحالة المناب إلى «مسترد» (حالة نهائية): تصفير حقول عائلة الحالات بلا مبالغ.</summary>
    private static void RecoverTarget(Document target)
    {
        ClearBaraetFields(target);
        ClearTarithFields(target);
        ClearSayerFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        target.SoldAssetIds = null;
        target.ExecStatus = ExecutionStatusCatalog.Recovered;
    }

    /// <summary>عود الملف المناب إلى المتداول (D3): مسح الحالة وحقول عائلة الحالات كما يراجع المنيب نفسه.</summary>
    private static void RevertTargetStatus(Document target)
    {
        target.ExecStatus = ExecutionStatusCatalog.None;
        target.ExecSubStatus = null;
        ClearCollectedFields(target);
        ClearBaraetFields(target);
        ClearTarithFields(target);
        ClearForcedExecutionField(target);
        ClearForcibleTransferFields(target);
        target.SoldAssetIds = null;
    }

    /// <summary>تفاصيل وقعة «تريث» الموروثة للمناب: حقول كتاب التريث المنقولة من المنيب.</summary>
    private static string? DeferredInheritanceDetails(Document source)
    {
        var details = new Dictionary<string, string>();
        CopyDetail(details, "tarithNumber", source.TarithNumber);
        CopyDetail(details, "tarithDate", source.TarithDate);
        CopyDetail(details, "tarithRegNumber", source.TarithRegNumber);
        CopyDetail(details, "tarithRegDate", source.TarithRegDate);
        return details.Count > 0 ? SerializeDetails(details) : null;
    }

    /// <summary>تفاصيل وقعة «استرداد» (F2): سبب الاسترداد + كتاب براءة الذمة (تسوية) أو تحويل
    /// البدل/رقم الإشعار (اكتمال جبري) + رقم أساس المنيب — بمفاتيح معاودة في نافذة الوقوعات.</summary>
    private static string RecoveryDetails(Document source, bool forcibly, string? sourceFileNumber)
    {
        var details = new Dictionary<string, string>
        {
            ["recoveryReason"] = forcibly ? ExecutionStatusCatalog.ExecutedForcibly : ExecutionStatusCatalog.ExecutedBySettlement,
        };
        if (forcibly)
        {
            CopyDetail(details, "forcibleTransferDate", source.ForcibleTransferDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            CopyDetail(details, "forcibleTransferNoticeNumber", source.ForcibleTransferNoticeNumber);
        }
        else
        {
            CopyDetail(details, "baraetNumber", source.BaraetNumber);
            CopyDetail(details, "baraetDate", source.BaraetDate);
        }
        CopyDetail(details, "sourceFileNumber", sourceFileNumber);
        return SerializeDetails(details);
    }

    /// <summary>تفاصيل وقعة «تراجع» للمناب: حقول كتاب الجهة العامة بالسير بالملف من المنيب —
    /// تُنسخ إلى التفاصيل فقط (تلميع) ولا تُحفظ على المناب.</summary>
    private static Dictionary<string, string> RevertBookDetails(Document source)
    {
        var details = new Dictionary<string, string>();
        CopyDetail(details, "sayerNumber", source.SayerNumber);
        CopyDetail(details, "sayerDate", source.SayerDate);
        CopyDetail(details, "sayerRegNumber", source.SayerRegNumber);
        CopyDetail(details, "sayerRegDate", source.SayerRegDate);
        return details;
    }

    private static void ClearBaraetFields(Document doc)
    {
        doc.BaraetNumber = null;
        doc.BaraetDate = null;
        doc.BaraetRegNumber = null;
        doc.BaraetRegDate = null;
    }

    private static void ClearForcedExecutionField(Document doc)
    {
        doc.ForcedExecutionDate = null;
    }

    /// <summary>
    /// تطهير حقلي «تحويل بدل المبيع» (تاريخ التحويل ورقم الإشعار) عند ترك حالة «منفذ جبريا»
    /// (تسوية/تريث/شطب/تراجع) — يخصان «منفذ جبريا» حصرًا، فيُصفَّران بنفس منطق نظيرهما.
    /// </summary>
    private static void ClearForcibleTransferFields(Document doc)
    {
        doc.ForcibleTransferDate = null;
        doc.ForcibleTransferNoticeNumber = null;
    }

    private static void ClearTarithFields(Document doc)
    {
        doc.TarithNumber = null;
        doc.TarithDate = null;
        doc.TarithRegNumber = null;
        doc.TarithRegDate = null;
    }

    private static void ClearSayerFields(Document doc)
    {
        doc.SayerNumber = null;
        doc.SayerDate = null;
        doc.SayerRegNumber = null;
        doc.SayerRegDate = null;
    }

    /// <summary>
    /// تطهير حقول «محال الى البداية» الأربعة (رقم/تاريخ كتاب المطالعة ورقم/تاريخ كتاب الإحالة)
    /// عند الخروج من الحالة عبر نقطة العودة فقط — الدخول إليها لا يمسح (قرار 11) والتطهير
    /// منشطّر حسب المصدر في UpdateStatusAsync.
    /// </summary>
    private static void ClearReferredToStartFields(Document doc)
    {
        doc.NoFundsDemandNumber = null;
        doc.NoFundsDemandDate = null;
        doc.StartReferralNumber = null;
        doc.StartReferralDate = null;
    }

    private static void ClearCollectedFields(Document doc)
    {
        doc.CollectedAmount = null;
        doc.CollectedAmount2 = null;
        doc.CollectedAmount3 = null;
    }

    private static decimal? ParseCollectedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        // يوحّد الأرقام العربية/الفارسية ثم فواصل الأرقام العربية (فاصل عشري ٫ وألوف ٬)
        // إلى ما يقبله التحليل؛ فلا يكسر ما يكتب بالأرقام ASCII (يمر كما هو).
        raw = ArabicDigitNormalizer.Normalize(raw)
            .Replace('\u066B', '.')   // ٫ الفاصل العشري العربي
            .Replace('\u066C', ',');  // ٬ فاصل الألوف العربي
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out parsed))
        {
            if (parsed < 0)
                throw new ArgumentException("المبلغ المحصل لا يمكن أن يكون سالباً");
            return parsed;
        }
        throw new ArgumentException("المبلغ المحصل غير صالح");
    }

    /// <summary>تطبيق المبالغ المحصلة (حتى ثلاثة بعملاتها) من حقول الطلب على المستند وسجل الوقعة.</summary>
    private static void ApplyCollectedAmounts(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        doc.CollectedAmount = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount"));
        doc.CollectedAmount2 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount2"));
        doc.CollectedAmount3 = ParseCollectedAmount(fields.GetValueOrDefault("collectedAmount3"));
        var currency = fields.GetValueOrDefault("collectedCurrency");
        var currency2 = fields.GetValueOrDefault("collectedCurrency2");
        var currency3 = fields.GetValueOrDefault("collectedCurrency3");
        doc.CollectedCurrency = string.IsNullOrWhiteSpace(currency) ? "ليرة سورية" : currency.Trim();
        doc.CollectedCurrency2 = string.IsNullOrWhiteSpace(currency2) ? "دولار أمريكي" : currency2.Trim();
        doc.CollectedCurrency3 = string.IsNullOrWhiteSpace(currency3) ? "يورو" : currency3.Trim();
        if (doc.CollectedAmount.HasValue)
        {
            details["collectedAmount"] = doc.CollectedAmount.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency"] = doc.CollectedCurrency;
        }
        if (doc.CollectedAmount2.HasValue)
        {
            details["collectedAmount2"] = doc.CollectedAmount2.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency2"] = doc.CollectedCurrency2;
        }
        if (doc.CollectedAmount3.HasValue)
        {
            details["collectedAmount3"] = doc.CollectedAmount3.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            details["collectedCurrency3"] = doc.CollectedCurrency3;
        }
    }

    /// <summary>
    /// تطبيق الأموال المباعة بالمزاد العلني (إلزامية في «منفذ جبريا»): تُتحقق المعرّفات
    /// من أموال الملف نفسه (عدا كفالة الرواتب)، وتُخزَّن JSON، وتُضمَّن أسماؤها في سجل الوقعة للعرض.
    /// </summary>
    private static void ApplySoldAssets(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
    {
        var raw = (fields.GetValueOrDefault("soldAssetIds") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("اختر الأموال التي جرى بيعها بالمزاد العلني على الأقل");

        var ids = new List<int>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id))
                throw new ArgumentException("معرّف مال مباع غير صالح");
            ids.Add(id);
        }
        var ownedIds = new HashSet<int>(doc.Assets
            .Where(a => AssetKindCatalog.IsAuctionable(a.AssetKind))
            .Select(a => a.Id));
        if (ids.Any(id => !ownedIds.Contains(id)))
            throw new ArgumentException("الأموال المختارة ليست من أموال الملف");

        doc.SoldAssetIds = SerializeJson(ids);
        details["soldAssetIds"] = string.Join(",", ids);
        var soldNames = doc.Assets
            .Where(a => ids.Contains(a.Id))
            .Select(AssetDisplayName)
            .Where(v => !string.IsNullOrWhiteSpace(v));
        details["soldAssetNames"] = string.Join("، ", soldNames);
    }

    /// <summary>تسمية قراءة للأصل (تُستخدم في «منفذ جبريا» وفي قوائم العرض).</summary>
    private static string AssetDisplayName(Asset a) => AssetDisplay.Label(a);

    private static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    private static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    private static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value);
}
