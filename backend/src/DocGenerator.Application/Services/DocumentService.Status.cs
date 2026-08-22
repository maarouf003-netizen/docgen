using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService
{
    // ملاحظة إعادة هيكلة (المرحلة 3 — مؤجلة): عند أول تعديل يمس منطق انتقالات الحالة
    // أو الشطب/التجديد في هذا الملف، تُستخرج هذه التدفقات إلى خدمة مستقلة خلف واجهة
    // (StatusTransitionService) بدل إضافة المزيد هنا. المرجع: FIXES_LOG.md بند المعلقات #4.
    public async Task<bool> UpdateStatusAsync(int documentId, string status, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        status = (status ?? string.Empty).Trim();
        var valid = ExecutionStatusCatalog.ValidStatuses.Contains(status)
            || status == ExecutionStatusCatalog.StateStruckOff;
        if (!valid)
            throw new ArgumentException("حالة غير صالحة");

        // آلة الحالات: تُمنع الانتقالات غير المسموحة من الحالة الحالية صراحةً.
        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.IsAllowedStatusChange(current, status))
            throw new ArgumentException(
                $"لا يمكن الانتقال من الحالة «{ExecutionStatusCatalog.ToStateLabel(current)}» إلى «{ExecutionStatusCatalog.ToStatusLabel(status)}»");

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
                _ => throw new ArgumentException("حالة غير صالحة"),
            };

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // تسجيل وقعة تغيير الحالة بحقولها الكاملة ضمن المعاملة نفسها — سجل زمني مستقل
            // يبقى ظاهرًا في «وقوعات الملف» بعد أي تراجع أو تعديل لاحق للحالة.
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = occurrenceType,
                EventDate = status == ExecutionStatusCatalog.StateStruckOff ? doc.StruckOffDate : DateTime.UtcNow,
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            var auditDetail = status == ExecutionStatusCatalog.StateStruckOff
                ? $"حالة {ExecutionStatusCatalog.StateStruckOff}"
                : $"حالة {ExecutionStatusCatalog.ToLabel(ExecutionStatusCatalog.Classify(status))}";
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor(auditDetail, doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> RevertStatusAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("التراجع عن الحالة يخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

        var current = ExecutionStatusCatalog.CurrentState(doc.IsDraft, doc.ExecStatus, doc.ExecutedStatus);
        if (!ExecutionStatusCatalog.CanRevert(current))
            throw new ArgumentException(
                $"لا يمكن التراجع عن الحالة الحالية «{ExecutionStatusCatalog.ToStateLabel(current)}»");

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

        return await _tx.RunAsync(async token =>
        {
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.Revert,
                EventDate = DateTime.UtcNow,
                Details = details.Count > 0 ? SerializeDetails(details) : null,
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor("تراجع عن الحالة وعاد الملف إلى المتداول", doc), token);
            return true;
        }, ct);
    }

    public async Task<bool> ConsiderExecutedByDelegationAsync(int documentId, Dictionary<string, string?> fields, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return false;
        if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide))
            throw new ArgumentException("حالة نظام «طالبة تنفيذ» تخص ملفات «الجهة العامة طالبة التنفيذ» فقط");

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

        return await _tx.RunAsync(async token =>
        {
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            // وقعة «منفذ جبريا» كاملة بحقولها — سجل زمني مستقل يبقى ظاهرًا في «وقوعات الملف».
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                OccurrenceType = OccurrenceTypeCatalog.Forcible,
                EventDate = DateTime.UtcNow,
                Details = SerializeDetails(details),
                CreatedById = doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "status", doc.Id, doc.DocumentType,
                AuditWithActor("اعتُبر الملف منفذًا كاملًا بهذا البيع (منفذ جبريا — منفذ كاملا)", doc), token);
            return true;
        }, ct);
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

        return await _tx.RunAsync(async token =>
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
            await _audit.LogAsync(actorName, "executed-status", doc.Id, doc.DocumentType,
                AuditWithActor(auditDetail, doc), token);
            return true;
        }, ct);
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

        // فك الشطب: العودة إلى متداول مع الإبقاء على تاريخ الشطب محفوظًا لعرضه بعد الإعادة.
        if (executedLike)
            doc.ExecutedStatus = ExecutedStatusCatalog.None;
        else
            doc.ExecStatus = ExecutionStatusCatalog.None;

        return await _tx.RunAsync(async token =>
        {
            // إعادة الملف المشطوب من صفحة «الملفات المشطوبة» تُعد تجديدًا: رقم الملف الجديد
            // إلزامي (ومعه سنة الإعادة في نظام «طالبة تنفيذ»)، ويُسجَّل رقم أساس لسنة الإعادة
            // فيعود الملف بالرقم والنوع الجديدين.
            await ApplyRenewalAsync(doc, renewal, executedLike, doc.CreatedById, token);
            doc.UpdatedAt = DateTime.UtcNow;
            _documents.Update(doc);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "restore-struck-off", doc.Id, doc.DocumentType,
                AuditWithActor("أعاد ملفًا مشطوبًا إلى المتداول مع تجديد رقم الملف", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين.
    /// </summary>
    private async Task ApplyRenewalAsync(Document doc, RenewalRequest? renewal, bool executedLike, int? userId, CancellationToken ct)
    {
        var number = renewal?.RenewalFileNumber?.Trim();
        if (string.IsNullOrEmpty(number))
            throw new ArgumentException("رقم الملف الجديد مطلوب عند إعادة الملف المشطوب");
        if (number.Length > 100)
            throw new ArgumentException("رقم الملف الجديد يتجاوز الطول المسموح");

        // سنة الإعادة: يحددها المستخدم في نظام «طالبة تنفيذ» (إلزامية)، وافتراضية للعام
        // الحالي في صفة «منفذ عليها» للاتساق مع السلوك القائم.
        int year;
        if (executedLike)
        {
            year = DateTime.Today.Year;
        }
        else
        {
            if (renewal?.RenewalYear is not { } enteredYear)
                throw new ArgumentException("سنة الإعادة مطلوبة عند إعادة الملف المشطوب");
            if (enteredYear < 1900 || enteredYear > 2100)
                throw new ArgumentException("سنة الإعادة غير صالحة");
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
        // النوع الجديد إن وُجد يُطبَّق على نوع الملف الظاهر.
        if (!string.IsNullOrEmpty(type))
            doc.FileType = type;

        // يعود الملف برقم سنة الإعادة: سجل رقم أساس لسنة الإعادة بالرقم الجديد فيظهر عبر
        // DisplayFileNumber (رقم أساس السنة الحالية ?? رقم الملف الأصلي).
        var existing = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
        if (existing is null)
        {
            await _baseNumbers.AddAsync(new DocumentBaseNumber
            {
                DocumentId = doc.Id,
                Year = year,
                BaseNumber = number,
                CreatedById = userId ?? doc.CreatedById,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }, ct);
        }
        else
        {
            existing.BaseNumber = number;
            existing.UpdatedAt = DateTime.UtcNow;
            _baseNumbers.Update(existing);
        }

        // سجل وقعة التجديد في «وقوعات الملف»: الرقم الجديد والنوع وسنة الإعادة
        // وورود اخطار التجديد — ضمن المعاملة نفسها فلا يضيع السجل عند فشل الحفظ.
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
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
    /// إلى الحالة «مشطوب»: تاريخ الشطب المحفوظ في المستند والرقم الأصلي للملف (الرقم
    /// الذي حُمّل عليه) وسنة الشطب — ضمن المعاملة نفسها فلا يضيع السجل عند فشل الحفظ.
    /// </summary>
    private async Task AddStruckOffOccurrenceAsync(Document doc, int? userId, CancellationToken ct)
    {
        string? oldNumber = (doc.FileNumber ?? string.Empty).Trim();
        await _occurrences.AddAsync(new DocumentOccurrence
        {
            DocumentId = doc.Id,
            OccurrenceType = OccurrenceTypeCatalog.StruckOff,
            EventDate = doc.StruckOffDate,
            FileNumber = string.IsNullOrEmpty(oldNumber) ? null : oldNumber,
            Year = doc.StruckOffDate?.Year,
            CreatedById = userId ?? doc.CreatedById,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        }, ct);
    }

}
