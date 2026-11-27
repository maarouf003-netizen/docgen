using System.Globalization;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد حالة المستند المشتركة بين الخدمة ومراحل `P4-01` — نُقلت حرفيًا من
/// `DocumentStatusService` (المساعدات الحقلية، التجديد/الشطب، وتوريث/استرداد المناب)
/// فلا ينحرف سلوك بين مسار قديم ومسار أوامر.
/// كل ما يفتح معاملة يملكها المستدعي حصرًا، وهذه القواعد تنضم لمعاملته.
/// </summary>
internal static class DocumentStatusRules
{
    /// <summary>
    /// تطبيق بيان تجديد الملف المشطوب: رقم الملف الجديد إلزامي (ومعه سنة الإعادة في نظام
    /// «طالبة تنفيذ»)، وتُفسَّر التواريخ النصية الحرة، ويُسجَّل رقم أساس لسنة الإعادة فيعود
    /// الملف بالرقم والنوع الجديدين — داخل معاملة المستدعي حصرًا (لا تفتح معاملة خاصة).
    /// </summary>
    internal static async Task ApplyRenewalAsync(
        Document doc, RenewalRequest? renewal, bool executedLike, int? userId,
        IRepository<DocumentBaseNumber> baseNumbers, IRepository<DocumentOccurrence> occurrences,
        IDocumentNumberRegistry? numberRegistry, TimeProvider clock, TimeZoneInfo timeZone,
        CancellationToken ct)
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
            if (renewal?.RenewalYear is { } hiddenYear && hiddenYear != ServerClock.CurrentYear(clock, timeZone))
                throw new ArgumentException("سنة الإعادة لعائلة «منفذ عليها/عرض وايداع» هي سنة اليوم الحالية فقط");
            year = ServerClock.CurrentYear(clock, timeZone);
        }
        else
        {
            if (renewal?.RenewalYear is not { } enteredYear)
                throw new ArgumentException("سنة الإعادة مطلوبة عند إعادة الملف المشطوب");
            if (enteredYear < 1900 || enteredYear > 2100)
                throw new ArgumentException("سنة الإعادة غير صالحة");
            DocumentValidator.ValidateFileYearNotFuture(enteredYear.ToString(CultureInfo.InvariantCulture), ServerClock.CurrentYear(clock, timeZone));
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
        if (numberRegistry is not null)
        {
            await numberRegistry.ApplyRenewalAsync(
                doc, number, year, doc.RenewalFileType, userId ?? doc.CreatedById, null,
                renewal?.WarningToken, ct);
        }
        else
        {
            // مسار اختباري قديم بلا سجل (البناءات المباشرة في الاختبارات) — لا يُستخدم في الإنتاج.
            await baseNumbers.AddAsync(new DocumentBaseNumber
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
        await occurrences.AddAsync(new DocumentOccurrence
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
    internal static async Task AddStruckOffOccurrenceAsync(
        Document doc, int? userId, IRepository<DocumentOccurrence> occurrences,
        TimeProvider clock, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var struckOffYear = doc.StruckOffDate?.Year ?? ServerClock.CurrentYear(clock, timeZone);
        string? effectiveNumber = EffectiveFileIdentity.Number(doc, struckOffYear) ?? string.Empty;
        string? fileType = (doc.FileType ?? string.Empty).Trim();
        await occurrences.AddAsync(new DocumentOccurrence
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
    internal static async Task<bool> ApplyDelegationInheritanceOrRecoveryAsync(
        Document source, IDocumentRepository documents, IDelegationRepository delegations,
        IRepository<DocumentOccurrence> occurrences, IUnitOfWork uow,
        TimeProvider clock, TimeZoneInfo timeZone, CancellationToken token)
    {
        var state = source.ExecStatus;
        var subStatus = source.ExecSubStatus;
        var actionable = state == ExecutionStatusCatalog.Deferred
            || state == ExecutionStatusCatalog.ExecutedBySettlement
            || (state == ExecutionStatusCatalog.ExecutedForcibly && subStatus == ExecutionStatusCatalog.SubFullyExecuted)
            || state == ExecutionStatusCatalog.None;
        if (!actionable)
            return false;

        var pending = await delegations.ListPendingBySourceWithTargetsAsync(source.Id, token);
        if (pending.Count == 0)
            return false;

        // «رقم أساس المنيب» لتفاصيل وقعة الاسترداد (F2) — الرقم الفعّال الحالي أو الأصلي.
        var sourceFileNumber = EffectiveFileIdentity.Number(source, ServerClock.CurrentYear(clock, timeZone));

        var changed = false;
        foreach (var delegation in pending)
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
            documents.Update(target);
            if (occurrence is not null)
                await occurrences.AddAsync(occurrence, token);
            await uow.SaveChangesAsync(token);
        }

        return changed;
    }

    /// <summary>
    /// D3: أرشفة تنبيهات الإنابة لكل مسار يجعل مناب مسجلة أصولًا نهائيًا —
    /// استرداد (تسوية/جبرية كاملة → «مسترد») أو شطب يدوي للمناب. تعمل داخل
    /// معاملة المتصل، وidempotent (إعادة الأرشفة آمنة). البوابة Registered حصرًا:
    /// المعلّقة/المحالة لها تنبيهات اعتماد لا تُمس.
    /// </summary>
    internal static async Task CleanupPendingCompletionAlertsAsync(
        int documentId, IDelegationRepository delegations, IDocumentRepository documents,
        IHeadAlertService alertService, CancellationToken ct)
    {
        // (1) المنيب: إناباته المسجلة التي صار منابها نهائيًا (أي فرع من IsTargetTerminal —
        // مشطوب بجهتيه/مسترد/منفذ إنابة — لا إعادة تعريف جزئية هنا).
        var recovered = (await delegations.ListBySourceAsync(documentId, ct))
            .Where(d => d.Status == DelegationStatusCatalog.Registered
                && d.TargetDocument is not null
                && DelegationActivityPolicy.IsTargetTerminal(d.TargetDocument))
            .Select(d => d.Id)
            .ToList();
        foreach (var id in recovered)
            await alertService.ArchiveByDelegationAsync(
                id,
                HeadAlertKindCatalog.DelegationKinds,
                "delegation_target_terminal",
                ct);
        // (2) الملف نفسه مناب صار نهائيًا يدويًا: إنابته المسجلة تحررت.
        var own = await delegations.FindByTargetAsync(documentId, ct);
        if (own is not null && own.Status == DelegationStatusCatalog.Registered)
        {
            var targetDoc = await documents.GetByIdAsync(documentId, ct);
            if (targetDoc is not null && DelegationActivityPolicy.IsTargetTerminal(targetDoc))
                await alertService.ArchiveByDelegationAsync(
                    own.Id,
                    HeadAlertKindCatalog.DelegationKinds,
                    "delegation_target_terminal",
                    ct);
        }
    }

    /// <summary>هل يُتخطى الملف المناب في التوريث/الاسترداد؟ — النهائي (منفذ إنابة)، المشطوب، المسترد، الدرافت.</summary>
    internal static bool ExcludedFromDelegationInheritance(Document target) =>
        target.IsDraft
        || target.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
        || target.ExecStatus == ExecutionStatusCatalog.StateStruckOff
        || target.ExecStatus == ExecutionStatusCatalog.Recovered
        || ExecutedStatusCatalog.IsStruckOff(target.ExecutedStatus);

    /// <summary>وقعة تغيير حالة على الملف المناب (تُسجَّل ضمن معاملة المنيب نفسها).</summary>
    internal static DocumentOccurrence DelegationTargetOccurrence(Document target, string occurrenceType, string? details) => new()
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
    internal static void InheritDeferredInto(Document source, Document target)
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
    internal static void RecoverTarget(Document target)
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
    internal static void RevertTargetStatus(Document target)
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
    internal static string? DeferredInheritanceDetails(Document source)
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
    internal static string RecoveryDetails(Document source, bool forcibly, string? sourceFileNumber)
    {
        var details = new Dictionary<string, string>
        {
            ["recoveryReason"] = forcibly ? ExecutionStatusCatalog.ExecutedForcibly : ExecutionStatusCatalog.ExecutedBySettlement,
        };
        if (forcibly)
        {
            CopyDetail(details, "forcibleTransferDate", source.ForcibleTransferDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
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
    internal static Dictionary<string, string> RevertBookDetails(Document source)
    {
        var details = new Dictionary<string, string>();
        CopyDetail(details, "sayerNumber", source.SayerNumber);
        CopyDetail(details, "sayerDate", source.SayerDate);
        CopyDetail(details, "sayerRegNumber", source.SayerRegNumber);
        CopyDetail(details, "sayerRegDate", source.SayerRegDate);
        return details;
    }

    internal static void ClearBaraetFields(Document doc)
    {
        doc.BaraetNumber = null;
        doc.BaraetDate = null;
        doc.BaraetRegNumber = null;
        doc.BaraetRegDate = null;
    }

    internal static void ClearForcedExecutionField(Document doc)
    {
        doc.ForcedExecutionDate = null;
    }

    /// <summary>
    /// تطهير حقلي «تحويل بدل المبيع» (تاريخ التحويل ورقم الإشعار) عند ترك حالة «منفذ جبريا»
    /// (تسوية/تريث/شطب/تراجع) — يخصان «منفذ جبريا» حصرًا، فيُصفَّران بنفس منطق نظيرهما.
    /// </summary>
    internal static void ClearForcibleTransferFields(Document doc)
    {
        doc.ForcibleTransferDate = null;
        doc.ForcibleTransferNoticeNumber = null;
    }

    internal static void ClearTarithFields(Document doc)
    {
        doc.TarithNumber = null;
        doc.TarithDate = null;
        doc.TarithRegNumber = null;
        doc.TarithRegDate = null;
    }

    internal static void ClearSayerFields(Document doc)
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
    internal static void ClearReferredToStartFields(Document doc)
    {
        doc.NoFundsDemandNumber = null;
        doc.NoFundsDemandDate = null;
        doc.StartReferralNumber = null;
        doc.StartReferralDate = null;
    }

    internal static void ClearCollectedFields(Document doc)
    {
        doc.CollectedAmount = null;
        doc.CollectedAmount2 = null;
        doc.CollectedAmount3 = null;
    }

    internal static decimal? ParseCollectedAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        // تطبيع الأرقام العربية/الفارسية، والفاصلة العشرية العربية إلى نقطة.
        // W1/B3: الفاصلة العادية/فاصلة الآلاف تُرفض بدل تفسيرها آلافًا صامتًا
        // (`"1,5"` كانت تُقرأ 15 لا 1.5)، والتحقق المالي مركزي (سالب/دقة/تجاوز).
        raw = ArabicDigitNormalizer.Normalize(raw)
            .Replace('\u066B', '.')
            .Replace("\u066C", string.Empty)
            .Trim();

        if (raw.Contains(',') || raw.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                "المبلغ المحصل غير صالح — استخدم النقطة كفاصل عشري بلا فواصل آلاف (مثال: 1500.50)");

        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException("المبلغ المحصل غير صالح");

        DocumentAmountPolicy.EnsureValid(parsed, "المبلغ المحصل");
        return parsed;
    }

    /// <summary>تطبيق المبالغ المحصلة (حتى ثلاثة بعملاتها) من حقول الطلب على المستند وسجل الوقعة.</summary>
    internal static void ApplyCollectedAmounts(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
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
            details["collectedAmount"] = doc.CollectedAmount.Value.ToString("0.##", CultureInfo.InvariantCulture);
            details["collectedCurrency"] = doc.CollectedCurrency;
        }
        if (doc.CollectedAmount2.HasValue)
        {
            details["collectedAmount2"] = doc.CollectedAmount2.Value.ToString("0.##", CultureInfo.InvariantCulture);
            details["collectedCurrency2"] = doc.CollectedCurrency2;
        }
        if (doc.CollectedAmount3.HasValue)
        {
            details["collectedAmount3"] = doc.CollectedAmount3.Value.ToString("0.##", CultureInfo.InvariantCulture);
            details["collectedCurrency3"] = doc.CollectedCurrency3;
        }
    }

    /// <summary>
    /// تطبيق الأموال المباعة بالمزاد العلني (إلزامية في «منفذ جبريا»): تُتحقق المعرّفات
    /// من أموال الملف نفسه (عدا كفالة الرواتب)، وتُخزَّن JSON، وتُضمَّن أسماؤها في سجل الوقعة للعرض.
    /// </summary>
    internal static void ApplySoldAssets(Document doc, Dictionary<string, string?> fields, Dictionary<string, string> details)
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
    internal static string AssetDisplayName(Asset a) => AssetDisplay.Label(a);

    internal static void CopyDetail(Dictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            details[key] = value;
    }

    internal static string SerializeDetails(Dictionary<string, string> details) =>
        JsonSerializer.Serialize(details);

    internal static string SerializeJson<T>(T value) => JsonSerializer.Serialize(value);
}
