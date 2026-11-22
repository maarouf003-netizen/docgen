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
}
