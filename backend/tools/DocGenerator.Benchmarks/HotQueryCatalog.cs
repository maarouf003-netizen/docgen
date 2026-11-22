using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Benchmarks;

/// <summary>نتيجة تنفيذ استعلام ساخن: عدد الصفوف وبصمة قصيرة تثبت أن النتيجة غير فارغة وثابتة.</summary>
public sealed record QueryOutcome(int Rows, string Fingerprint);

/// <summary>تعريف استعلام ساخن يُقاس عبر مسار المستودع/الخدمة الحقيقي لا عبر SQL مُعاد كتابته.</summary>
public sealed record HotQuery(
    string Id,
    string Description,
    Func<DocGeneratorPostgresDbContext, CancellationToken, Task<QueryOutcome>> Execute);

/// <summary>
/// كتالوج الاستعلامات الساخنة المطلوبة في `P3-01`: قائمة المستندات، بحث البوابة،
/// تفاصيل ملف البوابة، الإحصاءات (لوحة/شهري/فروع)، وملخص تنبيهات الرؤساء.
/// كل استعلام يُنفَّذ عبر الكائن الإنتاجي نفسه (`DocumentRepository`, `PortalRepository`,
/// `StatisticsRepository`, `HeadAlertRepository`) بلا نسخ SQL موازٍ يتباعد عن الإنتاج.
/// </summary>
public static class HotQueryCatalog
{
    public static IReadOnlyList<HotQuery> Build(SeedManifest manifest) =>
    [
        new HotQuery(
            "documents-list-page1",
            "قائمة المستندات الافتراضية (صفحة 1، 20 صفًا) مع شجرة التحميل القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchAsync(
                    query: null, status: null, applicant: null, court: null, lawyer: null,
                    branch: null, administrativeBranch: null, executedEntity: null,
                    publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-list-page100",
            "قائمة المستندات على إزاحة عميقة (صفحة 100) لكشف كلفة Skip/Take",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchAsync(
                    query: null, status: null, applicant: null, court: null, lawyer: null,
                    branch: null, administrativeBranch: null, executedEntity: null,
                    publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 100, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-list-text",
            "قائمة المستندات بحثًا نصيًا باسم طرف (كتلة DocumentPartySearch)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchAsync(
                    query: manifest.TextSearchTerm, status: null, applicant: null, court: null,
                    lawyer: null, branch: null, administrativeBranch: null, executedEntity: null,
                    publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-list-page1-projected",
            "قائمة المستندات بإسقاط صف القائمة (P3-04b) — بلا شجرة تحميل كاملة",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchListAsync(
                    query: null, status: null, applicant: null, court: null, lawyer: null,
                    branch: null, administrativeBranch: null, executedEntity: null,
                    publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-list-text-projected",
            "قائمة المستندات بحثًا نصيًا بإسقاط صف القائمة (P3-04b)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchListAsync(
                    query: manifest.TextSearchTerm, status: null, applicant: null, court: null,
                    lawyer: null, branch: null, administrativeBranch: null, executedEntity: null,
                    publicEntityBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        // P3-04c — قوائم الأرشيف الخمس: قديم = شجرة التحميل القياسية، جديد = إسقاط صف القائمة.
        new HotQuery(
            "documents-deleted-page1",
            "الملفات المحذوفة (صفحة 1) مع شجرة التحميل القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchDeletedAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-deleted-page1-projected",
            "الملفات المحذوفة بإسقاط صف القائمة (P3-04c)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchDeletedListAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-struck-off-page1",
            "الملفات المشطوبة (صفحة 1) مع شجرة التحميل القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchStruckOffAsync(
                    query: null, applicant: null, court: null, lawyer: null, branch: null,
                    administrativeBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-struck-off-page1-projected",
            "الملفات المشطوبة بإسقاط صف القائمة (P3-04c)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchStruckOffListAsync(
                    query: null, applicant: null, court: null, lawyer: null, branch: null,
                    administrativeBranch: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-executed-page1",
            "الملفات المنفذة (صفحة 1) مع شجرة التحميل القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchExecutedAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-executed-page1-projected",
            "الملفات المنفذة بإسقاط صف القائمة (P3-04c)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchExecutedListAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-referred-to-start-page1",
            "الملفات المحالة الى البداية (صفحة 1) مع شجرة التحميل القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items) = await repository.SearchReferredToStartAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "documents-referred-to-start-page1-projected",
            "الملفات المحالة الى البداية بإسقاط صف القائمة (P3-04c)",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var (total, items, _) = await repository.SearchReferredToStartListAsync(
                    query: null, visibleBranchId: null, visibleUserId: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "strike-off-reminders-page1",
            "تذكيرات الشطب (صفحة 1): اختيار المعرّفات ثم تحميلها بالشجرة القياسية",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var cutoff = DateTime.UtcNow.AddMonths(-5);
                var (total, rows, _) = await repository.SearchStrikeOffReminderIdsAsync(
                    cutoff, visibleBranchId: null, visibleUserId: null, page: 1, perPage: 20, ct: ct);
                var docs = await repository.GetByIdsWithStandardIncludesAsync(
                    rows.Select(r => r.Id).ToList(), ct);
                return new QueryOutcome(docs.Count, $"total={total};first={docs.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "strike-off-reminders-page1-projected",
            "تذكيرات الشطب بإسقاط صف القائمة (P3-04c): نفس الاختيار ثم تحميل خفيف",
            async (db, ct) =>
            {
                var repository = new DocumentRepository(db);
                var cutoff = DateTime.UtcNow.AddMonths(-5);
                var (total, rows, _) = await repository.SearchStrikeOffReminderIdsAsync(
                    cutoff, visibleBranchId: null, visibleUserId: null, page: 1, perPage: 20, ct: ct);
                var items = await repository.GetListRowsByIdsAsync(
                    rows.Select(r => r.Id).ToList(), ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "portal-list-page1",
            "بحث بوابة الجهة العامة (صفحة 1) مع مسند الرؤية الموحد",
            async (db, ct) =>
            {
                var repository = new PortalRepository(db);
                var (total, items) = await repository.SearchScopedAsync(
                    manifest.PortalEntryIds, query: null, status: null, page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "portal-list-text",
            "بحث بوابة الجهة العامة نصيًا باسم طرف (SearchText + أطراف)",
            async (db, ct) =>
            {
                var repository = new PortalRepository(db);
                var (total, items) = await repository.SearchScopedAsync(
                    manifest.PortalEntryIds, query: manifest.TextSearchTerm, status: null,
                    page: 1, perPage: 20, ct: ct);
                return new QueryOutcome(items.Count, $"total={total};first={items.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "portal-file-detail",
            "تفاصيل ملف في البوابة: فحص النطاق ثم تحميل المستند بشجرته القياسية",
            async (db, ct) =>
            {
                var portal = new PortalRepository(db);
                var inScope = await portal.IsDocumentInScopeAsync(
                    manifest.PortalDocumentId, manifest.PortalEntryIds, ct);
                if (!inScope)
                    return new QueryOutcome(0, "out-of-scope");

                var documents = new DocumentRepository(db);
                var document = await documents.GetByIdAsync(manifest.PortalDocumentId, ct);
                return new QueryOutcome(
                    document is null ? 0 : 1,
                    $"id={document?.Id};side={document?.GeneralEntitySide}");
            }),

        new HotQuery(
            "portal-export",
            "تصدير بوابة الجهة: صفوف القائمة المُسقَطة (P3-04d) بلا شجرة تحميل كاملة",
            async (db, ct) =>
            {
                var repository = new PortalRepository(db);
                var rows = await repository.ExportScopedAsync(
                    manifest.PortalEntryIds, query: null, status: null, ct: ct);
                return new QueryOutcome(rows.Count, $"rows={rows.Count};first={rows.FirstOrDefault()?.Id}");
            }),

        new HotQuery(
            "stats-dashboard",
            "إحصاءات اللوحة: عدادات الحالات + المبالغ + عدد المقترضين",
            async (db, ct) =>
            {
                var repository = new StatisticsRepository(db, TimeProvider.System, TimeZoneInfo.Utc);
                var stats = await repository.GetDashboardStatsAsync(branchId: null, ct: ct);
                return new QueryOutcome(1, $"total={stats.TotalDocuments};active={stats.TotalActive}");
            }),

        new HotQuery(
            "stats-monthly",
            "الإحصاءات الشهرية: تجميع الأشهر على كل المستندات",
            async (db, ct) =>
            {
                var repository = new StatisticsRepository(db, TimeProvider.System, TimeZoneInfo.Utc);
                var stats = await repository.GetMonthlyStatsAsync(branchId: null, ct: ct);
                return new QueryOutcome(stats.Count, $"months={stats.Count};last={stats.LastOrDefault()?.Count}");
            }),

        new HotQuery(
            "stats-branches",
            "إحصاءات الفروع: تجميع مستندات كل فرع ومبالغه",
            async (db, ct) =>
            {
                var repository = new StatisticsRepository(db, TimeProvider.System, TimeZoneInfo.Utc);
                var stats = await repository.GetBranchesSummaryAsync(ct);
                return new QueryOutcome(stats.Count, $"branches={stats.Count};total={stats.Sum(s => s.TotalDocuments)}");
            }),

        new HotQuery(
            "head-alert-summaries",
            "ملخص تنبيهات رئيس القسم (ProjectSummariesAsync) لمستلم واحد",
            async (db, ct) =>
            {
                var repository = new HeadAlertRepository(db);
                var page = await repository.ListSummariesForRecipientAsync(
                    manifest.HeadAlertUserId,
                    manifest.HeadAlertBranchId,
                    AlertArchiveFilter.Active,
                    unreadOnly: false,
                    delegationId: null,
                    cursor: null,
                    skip: 0,
                    take: 20,
                    includeCounters: true,
                    ct: ct);
                return new QueryOutcome(page.Items.Count, $"total={page.TotalCount};first={page.Items.FirstOrDefault()?.Id}");
            }),
    ];
}
