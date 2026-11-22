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

public sealed partial class DocumentStatusService : IDocumentStatusService
{
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
}
