using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IPortalService
{
    /// <summary>نطاق المندوب (هوية/قيد) — null إن لم يُربط بنطاق بعد.</summary>
    Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default);

    Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default);

    /// <summary>
    /// تفاصيل ملف قراءةً — null إذا خرج عن نطاق المندوب (يُترجم 404). داخليّات المحامي
    /// (الملاحظات `Notes`/`ImmediateActions` وإجراءات نوع `note`) مُسقطة سلكيًا (ق7).
    /// </summary>
    Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default);

    /// <summary>بطاقة الاستئنافات القرائية لملف داخل النطاق — null إذا خرج عن النطاق.</summary>
    Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>الإجراءات التنفيذية القرائية (نوع action فقط، الأحدث أولًا) — null خارج النطاق.</summary>
    Task<IReadOnlyList<PortalExecutionActionDto>?> ListExecutionActionsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تشعبات الملف القرائية (إنابة) — null خارج النطاق.</summary>
    Task<IReadOnlyList<DelegationDto>?> ListDelegationsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تفاصيل استئنافات الملف (رأي المحامي وملاحظاته مخفيّان عنه في البوابة) — null خارج النطاق.</summary>
    Task<IReadOnlyList<AppealDto>?> ListAppealDetailsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تاريخ أرقام الأساس للملف — null خارج النطاق.</summary>
    Task<IReadOnlyList<BaseNumberHistoryDto>?> ListBaseNumbersAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>
    /// مصنّف Excel لملفات النطاق وفق فلاتر القائمة نفسها، مع سقف
    /// ExportOptions.MaxRows وتدقيق export_entity_portal_excel.
    /// </summary>
    Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default);

    /// <summary>إحصاءات قرائية لنطاق المندوب (المرحلة 4).</summary>
    Task<PortalStatsDto> GetStatsAsync(int userId, CancellationToken ct = default);
}

/// <summary>
/// خدمة بوابة مندوب الجهة العامة (المرحلة 3): رؤية قرائية بحسب الربط — هوية أم
/// تشمل كل قيودها النهائية، أو قيد بعينه (د1/د4)؛ قيود الانتظار لا تظهر إطلاقًا.
/// التصدير يمرّ بسقف الصفوف ويُدوَّن، وأعمدة المحامين الداخلية مخفية دائمًا عن البوابة.
/// </summary>
public sealed class PortalService : IPortalService
{
    private readonly IPortalRepository _portal;
    private readonly IRepository<Document> _documents;
    private readonly IAppealRepository _appeals;
    private readonly IDocumentAppealService _appealService;
    private readonly IDocumentDelegationService _delegationService;
    private readonly IExcelExportService _excel;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public PortalService(
        IPortalRepository portal,
        IRepository<Document> documents,
        IAppealRepository appeals,
        IDocumentAppealService appealService,
        IDocumentDelegationService delegationService,
        IExcelExportService excel,
        IAuditLogger audit,
        IOptions<ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _portal = portal;
        _documents = documents;
        _appeals = appeals;
        _appealService = appealService;
        _delegationService = delegationService;
        _excel = excel;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        if (scope is null || scope.GroupId == 0)
            return scope is null ? null : new PortalScopeDto("group", 0, string.Empty, PublicEntityTypeCatalog.Ministry,
                Array.Empty<PortalScopeEntryDto>());

        return new PortalScopeDto(scope.ScopeType, scope.GroupId, scope.CanonicalName, scope.EntityType,
            scope.Entries.Select(e => new PortalScopeEntryDto(e.Id, e.Governorate, e.BranchName, e.IsActive)).ToList());
    }

    public async Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage <= 0 ? 20 : perPage, 1, 100);

        var (total, items) = await _portal.SearchScopedAsync(scope?.EntryIds ?? new List<int>(), query, status, page, perPage, ct);
        var result = new PagedResult<PortalFileListItemDto> { Page = page, PerPage = perPage, TotalCount = total };
        result.Items = items.Select(ToListItem).ToList();
        return result;
    }

    public async Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default)
    {
        if (!await IsInScopeAsync(userId, documentId, ct))
            return null;

        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return null;

        // تدقيق دخول جلسة العرض: مرة عند فتح الملف، لا مع كل صفحة/تفصيلة (§4).
        await _audit.LogAsync(viewerName, "view_entity_portal_files", documentId,
            details: "عرض ملف في بوابة الجهة العامة", ct: ct);

        // ق7 سلكيًا لا عرضيًا: الملاحظات الداخلية (`Notes`/`ImmediateActions`) تُصفَّر وإجراءات
        // نوع `note` تُرشَّح من الاستجابة قبل مغادرة الخادم — الواجهة ليست خط الدفاع الوحيد.
        // بيانات التذكير الداخلية (`ReminderDuration`/`ReminderColor`) تُجرَّد أيضًا: البطاقة
        // «بلا شارة تذكير» (ق7) فلا مبرر لعبورها الشبكة أصلًا.
        var response = DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
        response.Notes = null;
        response.ImmediateActions = null;
        response.ExecutionActions = response.ExecutionActions
            .Where(a => a.Type == "action")
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a with { ReminderDuration = null, ReminderColor = null })
            .ToList();

        return response;
    }

    public async Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(int userId, int documentId, CancellationToken ct = default)
    {
        if (!await IsInScopeAsync(userId, documentId, ct))
            return null;

        var appeals = await _appeals.ListByDocumentAsync(documentId, ct);
        return appeals.Select(a => new PortalAppealDto(
            a.Id,
            a.Direction,
            a.Status,
            a.AppealTypeLabel,
            a.AppealBaseNumber,
            a.AppealYear,
            a.CreatedAt,
            a.DecisionDate,
            a.DecisionRuling)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortalExecutionActionDto>?> ListExecutionActionsAsync(int userId, int documentId, CancellationToken ct = default)
    {
        var doc = await GetScopedDocumentAsync(userId, documentId, ct);
        if (doc is null)
            return null;

        // النوع action فقط (الملاحظات الداخلية لا تصل إلى البوابة أصلًا) والأحدث أولًا،
        // وكسر التعادل الزمني بـ Id الأحدث (ف11: CreatedAt ثم Id).
        return doc.ExecutionActions
            .Where(a => a.Type == "action")
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new PortalExecutionActionDto(a.Id, a.Text, a.ActionDate, a.CreatedBy?.FullName, a.CreatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DelegationDto>?> ListDelegationsAsync(int userId, int documentId, CancellationToken ct = default)
    {
        var doc = await GetScopedDocumentAsync(userId, documentId, ct);
        if (doc is null)
            return null;

        return await _delegationService.ListForDocumentAsync(documentId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppealDto>?> ListAppealDetailsAsync(int userId, int documentId, CancellationToken ct = default)
    {
        var doc = await GetScopedDocumentAsync(userId, documentId, ct);
        if (doc is null)
            return null;

        var appeals = await _appealService.ListForDocumentAsync(documentId, ct);

        // رأي المحامي الداخلي (دفاعه) وملاحظاته الحرة لا يظهران للمندوب (ق10 الموسّعة) —
        // حقلان حرّان من إدخال المحامي بلا ضمان بنيوي بأنهما وقائع قضية، فيُصفَّران معًا.
        // بقية الحقول (اسم المحامي المتابع وسطر «سطّره») تبقى ظاهرة (ق9).
        return appeals.Select(a => a with { DefenseOpinion = null, Notes = null }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BaseNumberHistoryDto>?> ListBaseNumbersAsync(int userId, int documentId, CancellationToken ct = default)
    {
        var doc = await GetScopedDocumentAsync(userId, documentId, ct);
        if (doc is null)
            return null;

        return doc.BaseNumbers
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .Select(b => new BaseNumberHistoryDto(b.Year, b.BaseNumber))
            .ToList();
    }

    public async Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var entryIds = scope?.EntryIds ?? new List<int>();

        var total = await _portal.CountScopedAsync(entryIds, query, status, ct);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var docs = await _portal.ExportScopedAsync(entryIds, query, status, ct);
        var responses = docs
            .Select(d => DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone)))
            .ToList();

        // ف12: عمود «الإجراءات والملاحظات» في Excel يُقرأ من أول إجراء بترتيب CreatedAt تنازلي —
        // إن لم يُقصر على النوع action قد تتصدره ملاحظة داخلية. يُرشَّح هنا فلا تتسرب الملاحظات،
        // وبكسر تعادل `Id` نفسه المعتمد في بطاقة التفاصيل فيتطابقا حتميًا حتى عند تساوي اللحظة.
        foreach (var response in responses)
            response.ExecutionActions = response.ExecutionActions
                .Where(a => a.Type == "action")
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .ToList();

        await _audit.LogAsync(viewerName, "export_entity_portal_excel",
            details: $"صدّر {responses.Count} ملفًا من بوابة الجهة إلى Excel", ct: ct);

        // أعمدة المحامين الداخلية (فرع الإدارة/المحامي المختص/العدادات) مخفية دائمًا عن البوابة.
        return _excel.BuildDocumentsWorkbook(
            responses, includeAdministrativeBranch: false, includeAssignedLawyer: false, includeViewCount: false);
    }

    private async Task<bool> IsInScopeAsync(int userId, int documentId, CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        return await _portal.IsDocumentInScopeAsync(documentId, scope?.EntryIds ?? new List<int>(), ct);
    }

    /// <summary>ملف داخل نطاق المندوب (لا يكشف الوجود خارج النطاق) — null عند خروجه أو غيابه.</summary>
    private async Task<Document?> GetScopedDocumentAsync(int userId, int documentId, CancellationToken ct)
    {
        if (!await IsInScopeAsync(userId, documentId, ct))
            return null;

        return await _documents.GetByIdAsync(documentId, ct);
    }

    // ── إحصاءات الجهة (المرحلة 4) ──

    /// <inheritdoc />
    public async Task<PortalStatsDto> GetStatsAsync(int userId, CancellationToken ct = default)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var ids = scope?.EntryIds ?? new List<int>();

        var statusPairs = await _portal.ListStatusPairsAsync(ids, ct);
        int draft = 0, circulating = 0, executed = 0, deferred = 0, referredToStart = 0;
        foreach (var (isDraft, execStatus) in statusPairs)
        {
            if (!string.IsNullOrEmpty(execStatus))
            {
                if (execStatus == ExecutionStatusCatalog.ExecutedForcibly
                    || execStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || execStatus == ExecutionStatusCatalog.DelegationExecuted
                    || execStatus == ExecutionStatusCatalog.Recovered)
                    executed++;
                else if (execStatus == ExecutionStatusCatalog.Deferred)
                    deferred++;
                // «محال الى البداية» سلّة مستقلة تحل محل عدّها المفترض ضمن «منفذ» (السلة
                // التنفيذية تبتلع جبريا بأي فرع) — فتبقى بطاقتها وفلترها متطابقين حرفيًا.
                else if (execStatus == ExecutionStatusCatalog.ReferredToStart)
                    referredToStart++;
                // الإحصاء يطابق فلتر القائمة حرفيًا (المنفذة الأربعة بضمنها «المسترد» عدًدا
                // دون مبالغ + تريث + محال) ليتطابق رقم البطاقة مع نتيجة الفلتر نفسه دون انحراف.
            }
            else if (isDraft) draft++;
            else circulating++;
        }

        var createdDates = await _portal.ListCreatedDatesAsync(ids, ct);
        var monthly = BuildMonthlySeries(createdDates);

        var perEntryCounts = await _portal.CountDocsPerEntryAsync(ids, ct);
        var perEntry = (scope?.Entries ?? Array.Empty<(int Id, string Governorate, string BranchName, bool IsActive)>())
            .Select(e => new PortalEntryStatDto(
                e.Id,
                e.Governorate,
                e.BranchName,
                perEntryCounts.GetValueOrDefault(e.Id)))
            .OrderByDescending(e => e.Files)
            .ThenBy(e => e.Governorate, StringComparer.Ordinal)
            .ThenBy(e => e.BranchName, StringComparer.Ordinal)
            .ToList();

        var currencyAmounts = await _portal.ListCurrencyAmountsAsync(ids, ct);
        var topCurrencies = currencyAmounts
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Currency) ? "غير محددة" : x.Currency!.Trim(), StringComparer.Ordinal)
            .Select(g => new PortalCurrencyStatDto(g.Key, g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(c => c.Files)
            .ThenByDescending(c => c.TotalAmount)
            .Take(5)
            .ToList();

        var (pendingAppeals, closedAppeals) = await _portal.AppealsBreakdownAsync(ids, ct);

        return new PortalStatsDto(
            TotalFiles: statusPairs.Count,
            DraftFiles: draft,
            CirculatingFiles: circulating,
            ExecutedFiles: executed,
            DeferredFiles: deferred,
            ReferredToStartFiles: referredToStart,
            PendingAppeals: pendingAppeals,
            ClosedAppeals: closedAppeals,
            Monthly: monthly,
            PerEntry: perEntry,
            TopCurrencies: topCurrencies);
    }

    /// <summary>سلسلة آخر 12 شهرًا متصلة حتى الشهر الحالي (UTC)، الأشهر بلا ملفات = 0.</summary>
    private static List<PortalMonthlyCountDto> BuildMonthlySeries(IReadOnlyList<DateTime> createdAtDates)
    {
        var nowUtc = DateTime.UtcNow;
        var start = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var counts = new Dictionary<(int Year, int Month), int>();
        foreach (var date in createdAtDates)
        {
            var key = (date.Year, date.Month);
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

    private static PortalFileListItemDto ToListItem(Document d) => new(
        d.Id,
        d.DocumentType ?? string.Empty,
        d.IsDraft,
        d.BorrowerName,
        d.Applicant,
        ExecutedSummary(d),
        d.AmountNumeric,
        d.Currency,
        d.ExecStatus,
        d.CreatedAt,
        d.UpdatedAt);

    private static string ExecutedSummary(Document d) =>
        string.Join("؛ ", d.ExecutedPublicEntities
            .Where(e => e.EntityNature == PartyNatureCatalog.PublicEntity)
            .Select(e => string.Join(' ', new[] { e.EntityName, e.EntityBranch }.Where(p => !string.IsNullOrWhiteSpace(p))))
            .Where(v => v.Length > 0));
}
