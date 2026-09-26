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
    /// <summary>نطاق المندوب (هوية/فرع) — null إن لم يُربط بنطاق بعد.</summary>
    Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default);

    Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default, int? entryId = null);

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
    Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default, int? entryId = null);

    /// <summary>إحصاءات قرائية لنطاق المندوب (المرحلة 4) — مع فلتر فرع اختياري ضمن النطاق.</summary>
    Task<PortalStatsDto> GetStatsAsync(int userId, CancellationToken ct = default, int? entryId = null);
}

/// <summary>
/// خدمة بوابة مندوب الجهة العامة (المرحلة 3): رؤية قرائية بحسب الربط — هوية أم
/// تشمل كل فروعها النهائية، أو فرع بعينه (د1/د4)؛ فروع الانتظار لا تظهر إطلاقًا.
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
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default, int? entryId = null)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage <= 0 ? 20 : perPage, 1, 100);

        var effectiveIds = ResolveEffectiveIds(scope, entryId);
        var (total, items) = await _portal.SearchScopedAsync(effectiveIds, query, status, page, perPage, ct);
        var result = new PagedResult<PortalFileListItemDto> { Page = page, PerPage = perPage, TotalCount = total };
        result.Items = items.Select(d => ToListItem(d, scope)).ToList();
        return result;
    }

    /// <summary>
    /// معرّفات الفروع الفعّالة: كامل النطاق افتراضيًا، أو الفرع المختار وحده بعد التحقق
    /// أنه ضمن نطاق المندوب (تقاطع لا توسيع — خارج النطاق يُرفض).
    /// </summary>
    private static List<int> ResolveEffectiveIds(PortalScopeResolution? scope, int? entryId)
    {
        var ids = scope?.EntryIds?.ToList() ?? new List<int>();
        if (!entryId.HasValue)
            return ids;
        if (!ids.Contains(entryId.Value))
            throw new UnauthorizedAccessException("الفرع المختار خارج نطاقك");
        return new List<int> { entryId.Value };
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

        // ق7 سلكيًا لا عرضيًا: كل حقل لا تستهلكه واجهة البوابة (`PortalFileDetail`
        // وكل المشتركات المعروضة فيه — مُدقَّق حقلًا بحقل ضد الاستهلاك الفعلي) يُحجب
        // قبل مغادرة الخادم — الواجهة ليست خط الدفاع الوحيد.
        // المحجوب: الملاحظات الداخلية (`Notes`/`ImmediateActions`)، العدّادات
        // التشغيلية (`ViewCount`/`PrintCount`)، الهوية الإدارية (`AdministrativeBranchName`/
        // `BranchId`)، هوية المنشئ (`CreatedById`/`CreatedByName`)، حقل `Lawyer` الحر،
        // وشارات قوائم المحامين (`NeedsRotation`/`HasAppeals`/`MatchedAppealId`)، ورابط
        // الإنابة الداخلي (`SourceDelegationId` — عرضه عبر نقطة التشعبات المخصصة)،
        // ومعرّفات الأصول المباعة (`SoldAssetIds`)، وتاريخ قرار الإحالة القطعية
        // (`ForcedExecutionDate`)، والتسمية المشتقة (`GeneralEntitySideLabel` —
        // لا يستهلكها العرض)، وختم الحذف (`DeletedAt` — دائم الفراغ هنا أصلًا)،
        // واسم الفرع (`BranchName` — العرض يستدعي `FileDataCard` بـ `showBranch={false}`).
        // المحفوظ قصدًا رغم الشبهة (مستهلك فعلًا في العرض): `Assignments` (ومنها
        // `AssignedByName` — يعرضه `TransferHistoryModal` «أحالها»)، و`ReferredFromLawyer`
        // و`UnderFilingNumber` و`FileIncoming*` و`FileArrival*` و`FileReceipt*` و`ExecutedDescription`
        // (يعرضها `FileDataCard`)، و`ExecutedRequired/Paid*` (يعرضها `ExecutoryDocumentCard`
        // و`FileDataCard`)، و`Baraet/Tarith/Sayer/NoFunds/StartReferral/Renewal/ForcibleTransfer`
        // و`Collected*` (يعرضها `OccurrencesModal` و`StatusCard`)، و`GeneralEntitySide`.
        // أي حقل جديد في `DocumentResponse` بلا تصريف صريح يُفشل اختبار الحارس
        // (`PortalDetailResponse_EveryPropertyHasExplicitDisposition`) — لا تسرب صامت.
        // سياسة الحجب الواحدة (A): الدالة نفسها يستدعيها مسار التفاصيل ومسار
        // التصدير معًا — فلا يتباعدا بصمت كما حدث مع عمود «الفرع».
        var response = DocumentResponse.FromEntity(doc, ServerClock.CurrentYear(_clock, _timeZone));
        ScrubForPortal(response);

        return response;
    }

    /// <summary>
    /// سياسة حجب البوابة الواحدة: كل حقل لا تستهلكه واجهة البوابة يُصفَّر هنا.
    /// يستدعيها <see cref="GetFileAsync"/> و<see cref="ExportWorkbookAsync"/> معًا —
    /// أي حقل جديد يُحسم هنا مرة واحدة فيغطي التفاصيل والتصدير معًا.
    /// </summary>
    private static void ScrubForPortal(DocumentResponse response)
    {
        response.Notes = null;
        response.ImmediateActions = null;
        response.ViewCount = 0;
        response.PrintCount = 0;
        response.AdministrativeBranchName = null;
        response.BranchId = null;
        response.CreatedById = 0;
        response.CreatedByName = null;
        response.Lawyer = null;
        response.NeedsRotation = false;
        response.HasAppeals = false;
        response.MatchedAppealId = null;
        response.SourceDelegationId = null;
        response.SoldAssetIds = new List<int>();
        response.ForcedExecutionDate = null;
        response.GeneralEntitySideLabel = null;
        response.DeletedAt = null;
        // `BranchName` هو فرع الإدارة الداخلي (نص حر يكتبه المحامي) ولا تُعرض منه
        // واجهة البوابة ولا مصنّف تصديرها — فيُحجب في المسارين. أما ما تعرضه البوابة
        // من فروع فهو فروع *نطاق المندوب* (`MatchedEntries` في
        // `PortalScopeEntryDto`)، وهي بيانات نطاقات لا حقول هذا الملف — فلا يمسّها
        // هذا الحجب أصلًا. (`FileDataCard` يُستدعى هنا بـ `showBranch={false}`.)
        response.BranchName = null;
        response.ExecutionActions = response.ExecutionActions
            .Where(a => a.Type == "action")
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a with { ReminderDuration = null, ReminderColor = null })
            .ToList();
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

    public async Task<byte[]> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default, int? entryId = null)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var entryIds = ResolveEffectiveIds(scope, entryId);

        var total = await _portal.CountScopedAsync(entryIds, query, status, ct);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var docs = await _portal.ExportScopedAsync(entryIds, query, status, ct);

        // عمود «فرع الجهة» مصدره النطاق المضيَّق إلى معرّفات القواعد التي رُشِّحت
        // بها الصفوف فعلًا (`entryIds`)، لا النطاق الكامل: وإلا عرض عمود الفروع
        // فروعًا لم تدخل الصفوف أصلًا فنناقض معلومة التصدير نفسها. (والقائمة تُبقي
        // نطاقها كاملًا لعرضه مختصرًا في البطاقة — قرار يخصّ العرض لا التصدير.)
        var scopedForRows = NarrowScope(scope, entryIds);

        var rows = docs
            .Select(d =>
            {
                var response = DocumentResponse.FromEntity(d, ServerClock.CurrentYear(_clock, _timeZone));
                // طبقتان لا واحدة: التحكم الأساسي في التصدير هو **قائمة الأعمدة
                // المغلقة** (`ExcelExportService.PortalColumns` + `BuildPortalValues`)،
                // فحقل داخلي لا يظهر أصلًا وإن لم تحجبْه التنقية؛ والتنقية هنا طبقة
                // دفاع ثانية تُبطل أي حقل محجوب قبل أن يقرأه عمودٌ مستقبلي بالخطأ،
                // وهي اليوم ما يجعل عمود «الإجراءات والملاحظات» علنيًّا خالصًا:
                // أول `ExecutionActions` بعدها هو أحدث إجراء من النوع `action` حتمًا.
                ScrubForPortal(response);
                return new PortalWorkbookRow(response, BuildMatchedEntries(d, scopedForRows));
            })
            .ToList();

        await _audit.LogAsync(viewerName, "export_entity_portal_excel",
            details: entryId.HasValue
                ? $"صدّر {rows.Count} ملفًا من بوابة الجهة إلى Excel (فرع {entryId.Value})"
                : $"صدّر {rows.Count} ملفًا من بوابة الجهة إلى Excel", ct: ct);

        // مصنّف البوابة عقد منفصل عن مصنّف المدير: «ملحق العقد» و«الإجراءات
        // والملاحظات» غير موجودين فيه بحكم التصميم، و«فرع الجهة» فروع نطاق المندوب
        // لا فرع الإدارة الداخلي.
        return _excel.BuildPortalWorkbook(rows);
    }

    /// <summary>
    /// نسخة من النطاق مقيَّدة بمعرّفات معيّنة (القيود الفعّالة بعد مُرشِّح التصدير
    /// أو القائمة) — تُغذّي `BuildMatchedEntries` فلا يعرض أي مسار فروعًا خارج
    /// ما طلبه الفلتر. النطاق الفارغ أو غير المربوط يُبقي الدالة بلا أثر.
    /// </summary>
    private static PortalScopeResolution? NarrowScope(
        PortalScopeResolution? scope, IReadOnlyCollection<int> entryIds)
    {
        if (scope is null)
            return null;
        return scope with
        {
            Entries = scope.Entries.Where(e => entryIds.Contains(e.Id)).ToList(),
        };
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
    public async Task<PortalStatsDto> GetStatsAsync(int userId, CancellationToken ct = default, int? entryId = null)
    {
        var scope = await _portal.ResolveForUserAsync(userId, ct);
        var ids = ResolveEffectiveIds(scope, entryId);

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
        if (entryId.HasValue)
            scopeEntries = scopeEntries.Where(e => e.Id == entryId.Value).ToList();
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
    /// أزواج مبالغ الدين الستة كما سُجّلت في النموذج (المبلغ×3 + مبلغ الإدراج×3
    /// بعملاتها — قرار المالك) مع إسقاط الصفري: ملف بلا مبلغ لا يُنشئ ملفًا وهميًا
    /// في عملته الافتراضية (مرآة `AddAmount` في إحصاءات المدير) — يبقى في عدّاد
    /// سلّته (عددًا) دون أن يدخل أي مجموع عملة.
    /// </summary>
    private static List<(int DocId, string? Currency, decimal Amount)> DebtAmountPairs(PortalAmountRow row)
    {
        var pairs = new (int DocId, string? Currency, decimal Amount)[]
        {
            (row.DocumentId, row.Currency, row.AmountNumeric),
            (row.DocumentId, row.Currency2, row.Amount2Numeric),
            (row.DocumentId, row.Currency3, row.Amount3Numeric),
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

    private static PortalFileListItemDto ToListItem(Document d, PortalScopeResolution? scope = null)
    {
        // أحدث رقم أساس دائمًا (غير مقيّد بسنة الفحص) — وإلا رقم الملف/سنة قيده الأصلية.
        // انحراف مقصود وموثّق عن بقية النظام (as-of السنة الحالية): قرار صاحب المشروع
        // «دائمًا أحدث رقم أساس مع السنة» — فرقم بسنة مستقبلية (خطأ إدخال) يظهر هنا
        // دون قوائم المحامين؛ مثبّت باختبار سنة مستقبلية قصدًا لا سهوًا.
        var latestBase = EffectiveFileIdentity.LatestFrom(d.BaseNumbers, int.MaxValue);
        var displayNumber = latestBase?.BaseNumber ?? d.FileNumber;
        var displayYear = latestBase is not null ? latestBase.Year.ToString() : d.FileYear;
        return new(
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
            d.UpdatedAt,
            d.BorrowerFather,
            d.BorrowerFamily,
            d.FileType,
            d.Court,
            displayNumber,
            displayYear,
            BuildMatchedEntries(d, scope),
            // شارة البطاقة من المصدر الوحيد (`DocumentStatusResolver`) — لا منطق
            // تصنيف في الواجهة: الخام (`ExecStatus`) للفلترة، والمعروض للشارة.
            DocumentStatusResolver.Resolve(d));
    }

    /// <summary>
    /// فروع النطاق المطابقة للملف (تقاطع RegistryIds الثلاثة مع معرّفات النطاق) —
    /// تُغذّي السطر الثاني «فرع الجهة العامة» في بطاقة الملف (مثال: المصرف التجاري
    /// — اللاذقية/فرع 1)، وعمود «فرع الجهة» في مصنّف تصدير البوابة.
    ///
    /// مصدران يتشاركان هذه الدالة، فقواعد إلزامية على أي مراجعة:
    /// (1) لا تُقرأ `Registry.*` إطلاقًا (لا `ThenInclude` في `ExportScopedAsync`
    /// ولا تحميل كسول) — المرجع `RegistryId` والمدى `scope.Entries` وحدهما؛
    /// (2) لا يخرج عن نطاق المندوب أبدًا، فالبوابة عاجزة بنيويًا عن عرض فرع جهة
    /// أخرى (فرع إداري أو فرع طرف ثالث) مهما بلغ عدد أطراف الملف.
    /// </summary>
    private static IReadOnlyList<PortalScopeEntryDto> BuildMatchedEntries(Document d, PortalScopeResolution? scope)
    {
        if (scope is null)
            return Array.Empty<PortalScopeEntryDto>();
        var byId = scope.Entries.ToDictionary(e => e.Id);
        var matchedIds = new HashSet<int>();
        foreach (var a in d.ApplicantPublicEntities)
            if (a.RegistryId.HasValue && byId.ContainsKey(a.RegistryId.Value))
                matchedIds.Add(a.RegistryId.Value);
        foreach (var e in d.ExecutedPublicEntities)
            if (e.RegistryId.HasValue && byId.ContainsKey(e.RegistryId.Value))
                matchedIds.Add(e.RegistryId.Value);
        foreach (var a in d.ExecutionApplicants)
            if (a.RegistryId.HasValue && byId.ContainsKey(a.RegistryId.Value))
                matchedIds.Add(a.RegistryId.Value);
        return matchedIds
            .Select(id => byId[id])
            // الترتيب العربي الوحيد نفسه (`PortalScopeOrdering`): خلية التصدير
            // وبطاقة الملف وقائمة الفرع ثلاثتها بترتيب واحد لا ثلاثة.
            .OrderBy(e => e.Governorate, PortalScopeOrdering.ArabicDisplay)
            .ThenBy(e => e.BranchName, PortalScopeOrdering.ArabicDisplay)
            .Select(e => new PortalScopeEntryDto(e.Id, e.Governorate, e.BranchName, e.IsActive))
            .ToList();
    }

    private static string ExecutedSummary(Document d) =>
        string.Join("؛ ", d.ExecutedPublicEntities
            .Where(e => e.EntityNature == PartyNatureCatalog.PublicEntity)
            .Select(e => string.Join(' ', new[] { e.EntityName, e.EntityBranch }.Where(p => !string.IsNullOrWhiteSpace(p))))
            .Where(v => v.Length > 0));
}
