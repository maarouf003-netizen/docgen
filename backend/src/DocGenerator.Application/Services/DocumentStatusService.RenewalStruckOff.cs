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

        // يعود الملف برقم سنة الإعادة: الكاتب الوحيد للسجل يضيف صف Source=renewal
        // (فحص قفل/سنة/تفرد داخله)، ويبقى المعتبر الأحدث CreatedAt لنفس السنة.
        if (_numberRegistry is not null)
        {
            await _numberRegistry.ApplyRenewalAsync(
                doc, number, year, doc.RenewalFileType, userId ?? doc.CreatedById, null,
                renewal?.WarningToken, ct);
        }
        else
        {
            // مسار اختباري قديم بلا سجل (البناءات المباشرة في الاختبارات) — لا يُستخدم في الإنتاج.
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
}
