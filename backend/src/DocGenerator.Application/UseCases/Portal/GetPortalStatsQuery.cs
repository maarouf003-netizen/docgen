using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>إحصاءات قرائية لنطاق المندوب (المرحلة 4) — مع فلتر فرع اختياري ضمن النطاق.</summary>
public sealed record GetPortalStatsQuery(int UserId, int? EntryId = null) : IQuery<PortalStatsDto>;

internal sealed class GetPortalStatsQueryHandler
    : QueryHandlerBase<GetPortalStatsQuery, PortalStatsDto>
{
    private readonly IPortalRepository _portal;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetPortalStatsQueryHandler(
        IPortalRepository portal,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetPortalStatsQuery, PortalStatsDto>> behaviors)
        : base(behaviors)
    {
        _portal = portal;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PortalStatsDto> HandleCoreAsync(
        GetPortalStatsQuery query, CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(query.UserId, ct);
        var ids = PortalService.ResolveEffectiveIds(scope, query.EntryId);

        // اللقطة الوحيدة: كل العدّادات والمجاميع تُشتق من نفس الصفوف (فلا سباق
        // بين استعلامين ولا ازدواج تصنيف). الحالة أولًا دائمًا، والمسودة مسودة
        // بلا حالة فقط — مطابقة فلتر القائمة (`ScopedQuery`) حرفيًا عبر ثوابت
        // الكتالوج نفسها (أي انحراف بينهما عيب حاجب — تثبّته لازمة التطابق).
        var amountRows = await _portal.ListAmountRowsAsync(ids, ct);

        var bucketCounts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [ExecutionStatusCatalog.StateCirculating] = 0,
            [ExecutionStatusCatalog.Deferred] = 0,
            [ExecutionStatusCatalog.ExecutedFilter] = 0,
            [ExecutionStatusCatalog.ReferredToStart] = 0,
            [ExecutionStatusCatalog.DraftFilter] = 0,
        };
        var bearingRows = new List<(int DocId, string? Currency, decimal Amount)>();
        var bearingBuckets = new Dictionary<string, List<(int DocId, string? Currency, decimal Amount)>>(StringComparer.Ordinal)
        {
            [ExecutionStatusCatalog.StateCirculating] = new(),
            [ExecutionStatusCatalog.Deferred] = new(),
            [ExecutionStatusCatalog.ExecutedFilter] = new(),
            [ExecutionStatusCatalog.ReferredToStart] = new(),
            [ExecutionStatusCatalog.DraftFilter] = new(),
        };
        foreach (var row in amountRows)
        {
            var bucket = ClassifyPortalBucket(row.IsDraft, row.ExecStatus, row.ExecSubStatus);
            bucketCounts[bucket]++;
            // ملف الإنابة يُحتسب عددًا دون مبالغ (قرار المالك): نسخه تحمل مبالغ
            // المنيب فلا تُجمع مرتين — القاعدة مطابقة ب6 في الاستثناء لا في المقياس
            // (هنا مبالغ الدين الستة، وهناك المحصّل — يُوثَّق الفرق ولا يُدَّعى تطابق).
            if (IsAmountBearing(row))
            {
                var pairs = DebtAmountPairs(row);
                bearingRows.AddRange(pairs);
                bearingBuckets[bucket].AddRange(pairs);
            }
        }

        var createdDates = await _portal.ListCreatedDatesAsync(ids, ct);
        var monthly = BuildMonthlySeries(createdDates, ServerClock.Now(_clock, _timeZone), _timeZone);

        var perEntryCounts = await _portal.CountDocsPerEntryAsync(ids, ct);
        var scopeEntries = scope?.Entries ?? Array.Empty<(int Id, string Governorate, string BranchName, bool IsActive)>();
        if (query.EntryId.HasValue)
            scopeEntries = scopeEntries.Where(e => e.Id == query.EntryId.Value).ToList();
        var perEntry = scopeEntries
            .Select(e => new PortalEntryStatDto(
                e.Id,
                e.Governorate,
                e.BranchName,
                perEntryCounts.GetValueOrDefault(e.Id)))
            .OrderByDescending(e => e.Files)
            // كسر التعادل بالترتيب العربي الوحيد نفسه — لا `Ordinal` منفصلًا
            // فيتباين سطر الإحصاء عن قائمة الفرع للنطاق نفسه.
            .ThenBy(e => e.Governorate, PortalScopeOrdering.ArabicDisplay)
            .ThenBy(e => e.BranchName, PortalScopeOrdering.ArabicDisplay)
            .ToList();

        // «أعلى العملات» بلا مستهلك في العرض حاليًا — يُبقى في العقد لاستقراره
        // (حذفه تغيير عقد بلا مقابل تشغيلي: يُشتق من اللقطة نفسها بلا استعلام زائد).
        var topCurrencies = GroupAmounts(bearingRows).Take(5).ToList();
        var amountTotals = GroupAmounts(bearingRows);
        var amountByStatus = bearingBuckets
            .Select(kv => new PortalStatusAmountDto(kv.Key, bucketCounts[kv.Key], GroupAmounts(kv.Value)))
            .ToList();

        var (pendingAppeals, closedAppeals) = await _portal.AppealsBreakdownAsync(ids, ct);

        return new PortalStatsDto(
            TotalFiles: amountRows.Count,
            DraftFiles: bucketCounts[ExecutionStatusCatalog.DraftFilter],
            CirculatingFiles: bucketCounts[ExecutionStatusCatalog.StateCirculating],
            ExecutedFiles: bucketCounts[ExecutionStatusCatalog.ExecutedFilter],
            DeferredFiles: bucketCounts[ExecutionStatusCatalog.Deferred],
            ReferredToStartFiles: bucketCounts[ExecutionStatusCatalog.ReferredToStart],
            PendingAppeals: pendingAppeals,
            ClosedAppeals: closedAppeals,
            Monthly: monthly,
            PerEntry: perEntry,
            TopCurrencies: topCurrencies,
            AmountTotals: amountTotals,
            AmountByStatus: amountByStatus);
    }

    /// <summary>
    /// المصنّف الوحيد لسلّات البوابة (عدّادات + مطابقة فلتر القائمة): الحالة أولًا
    /// دائمًا، والمسودة مسودة بلا حالة فقط. فرع «منفذ» هو `IsExecuted` من الكتالوج
    /// نفسه (تسوية/إنابة/مسترد/جبريا غير جزئي) — «منفذ جبريا + منفذ جزئيا» متداول
    /// دائمًا بقرار المالك (كالمدير وآلة الحالات وقائمة المحامين بعد التوحيد).
    /// «محال الى البداية» سلّة مستقلة. أي حالة غير مصنّفة (إرثية) تُعامل «متداولًا»
    /// هنا وفي فلتر القائمة معًا (قرار المالك) — فلا عدّاد بلا فلتر مطابق.
    /// </summary>
    private static string ClassifyPortalBucket(bool isDraft, string? execStatus, string? execSubStatus)
    {
        if (!string.IsNullOrEmpty(execStatus))
        {
            if (ExecutionStatusCatalog.IsExecuted(execStatus, execSubStatus))
                return ExecutionStatusCatalog.ExecutedFilter;
            if (execStatus == ExecutionStatusCatalog.Deferred)
                return ExecutionStatusCatalog.Deferred;
            if (execStatus == ExecutionStatusCatalog.ReferredToStart)
                return ExecutionStatusCatalog.ReferredToStart;
            return ExecutionStatusCatalog.StateCirculating;
        }
        return isDraft ? ExecutionStatusCatalog.DraftFilter : ExecutionStatusCatalog.StateCirculating;
    }

    /// <summary>
    /// حامل المبلغ: ليس نسخة إنابة (`SourceDelegationId == null`) وليس في حالة
    /// إنابة انتهائية (`منفذ إنابة`/`مسترد` — منابان دائمًا) — فيُحتسب عددًا دون
    /// مبالغ (قرار المالك). الرابط يغطي النسخة المتداولة/المسوّاة، والحالة تغطي
    /// صفوفًا قديمة قد يكون رابطها فارغًا. المسودة حاملة لمبالغها (القاعدة
    /// المعتمدة تستثني الإنابة وحدها — بخلاف إجمالي المدير الذي يستثني المسودات).
    /// </summary>
    private static bool IsAmountBearing(PortalAmountRow row)
        => row.SourceDelegationId == null
        && row.ExecStatus != ExecutionStatusCatalog.DelegationExecuted
        && row.ExecStatus != ExecutionStatusCatalog.Recovered;

    /// <summary>
    /// أزواج مبالغ الدين كما سُجّلت في النموذج، من مجموعة واحدة حسب نوع السند:
    /// المصرفي من Amount*، والعادي من InclusionAmount* — لا جمعًا للمجموعتين
    /// (ملف قديم/عميل مباشر يحمل القيمتين لا يتضاعف مبلغه). مع إسقاط الصفري:
    /// ملف بلا مبلغ لا يُنشئ ملفًا وهميًا في عملته الافتراضية (مرآة `AddAmount`
    /// في إحصاءات المدير) — يبقى في عدّاد سلّته (عددًا) دون أن يدخل أي مجموع عملة.
    /// </summary>
    private static List<(int DocId, string? Currency, decimal Amount)> DebtAmountPairs(PortalAmountRow row)
    {
        var pairs = ContractTypeCatalog.IsBanking(row.ContractTypeSelector)
            ? new (int DocId, string? Currency, decimal Amount)[]
            {
                (row.DocumentId, row.Currency, row.AmountNumeric),
                (row.DocumentId, row.Currency2, row.Amount2Numeric),
                (row.DocumentId, row.Currency3, row.Amount3Numeric),
            }
            : new (int DocId, string? Currency, decimal Amount)[]
            {
                (row.DocumentId, row.InclusionCurrency, row.InclusionAmountNumeric),
                (row.DocumentId, row.InclusionCurrency2, row.InclusionAmount2Numeric),
                (row.DocumentId, row.InclusionCurrency3, row.InclusionAmount3Numeric),
            };
        return pairs.Where(p => p.Amount != 0).ToList();
    }

    /// <summary>
    /// تجميع مبالغ حسب العملة (تطبيع الفراغ إلى «غير محددة») مرتبًا بعدد الملفات
    /// ثم المبلغ. `Files` = عدد الملفات **المتميزة** الحاملة لمبلغ غير صفري بهذه
    /// العملة (الزوج الثاني/الإدراج لا يضاعف عدّ الملف) — ويختلف عن عدّاد السلّة
    /// الكامل (الذي يشمل الصفرية والإنابة عددًا) قصدًا وبتوثيق في `PortalStatusAmountDto`.
    /// </summary>
    private static List<PortalCurrencyStatDto> GroupAmounts(IEnumerable<(int DocId, string? Currency, decimal Amount)> rows)
        => rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Currency) ? "غير محددة" : x.Currency!.Trim(), StringComparer.Ordinal)
            .Select(g => new PortalCurrencyStatDto(g.Key, g.Select(x => x.DocId).Distinct().Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(c => c.Files)
            .ThenByDescending(c => c.TotalAmount)
            .ToList();

    /// <summary>
    /// سلسلة آخر 12 شهرًا متصلة حتى الشهر الحالي (بتوقيت النظام المحقون —
    /// المرساة والسلال معًا بالتوقيت المحلي، فملف على حدّ الشهر يُحتسب في
    /// شهره المحلي لا UTC).
    /// الأشهر بلا ملفات = 0.
    /// </summary>
    private static List<PortalMonthlyCountDto> BuildMonthlySeries(IReadOnlyList<DateTime> createdAtDates, DateTime now, TimeZoneInfo zone)
    {
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var counts = new Dictionary<(int Year, int Month), int>();
        foreach (var date in createdAtDates)
        {
            // تطبيع آمن لأي Kind قادم من القاعدة (Unspecified من SQLite شائع)
            // قبل التحويل — بلا رمي استثناء على أي نوع.
            var utc = date.Kind switch
            {
                DateTimeKind.Utc => date,
                DateTimeKind.Local => date.ToUniversalTime(),
                _ => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            };
            var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
            var key = (local.Year, local.Month);
            counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
        }

        var series = new List<PortalMonthlyCountDto>(12);
        for (var i = 0; i < 12; i++)
        {
            var monthStart = start.AddMonths(i);
            series.Add(new PortalMonthlyCountDto(
                monthStart.Year,
                monthStart.Month,
                counts.TryGetValue((monthStart.Year, monthStart.Month), out var n) ? n : 0));
        }
        return series;
    }
}
