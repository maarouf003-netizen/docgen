using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// دخان هجرة `AddSubHeadSections` على ملف SQLite حقيقي (لا `EnsureCreated`):
/// الصعود يبني المخطط، والصفوف القديمة تُرحَّل (`Owned` + `Version = 1`)،
/// والهبوط للهجرة السابقة ثم الصعود مجددًا يثبت مسار التراجع (شرط النشر الحكومي).
/// </summary>
public class SubHeadMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261005103301_WidenOccurrenceType";

    // أسماء الجداول المسموحة في استعلامات الاختبار الخام (`pragma_table_info` لا
    // يقبل معامَلَين مرتبطين لاسم الجدول) — قائمة بيضاء تمنع أي حقن عبر البناء.
    private static readonly IReadOnlySet<string> AllowedTables = new HashSet<string>(StringComparer.Ordinal)
    {
        "Users", "Documents", "DocumentAppeals", "DocumentDelegations",
        "Correspondences", "ReviewLetters", "Sections", "HeadSuccessions", "ExecutionCircuits",
    };

    private readonly string _path;
    private readonly DocGeneratorDbContext _db;

    public SubHeadMigrationTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"subhead_mig_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _db = new DocGeneratorDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* تنظيف أفضل جهد */ }
    }

    // `pragma_table_info` لا يقبل معامَلَين مرتبطين لاسم الجدول، وقيم الإدراج
    // تُبنى ديناميكيًا من مخطط القاعدة نفسه — لذا يُسكَت تحذيرا EF1002/EF1003
    // موضعيًا ومبرَّرًا: كل أسماء الجداول من القائمة البيضاء أعلاه، وكل القيم
    // ثوابت كتالوج أو عدّادات داخلية — لا مدخل مستخدم إطلاقًا.
#pragma warning disable EF1002, EF1003 // مبرَّر: قائمة بيضاء + ثوابت داخلية بلا مدخل مستخدم
    private Task<List<string>> TableColumnsAsync(string table)
    {
        RequireAllowedTable(table);
        return _db.Database.SqlQueryRaw<string>($"SELECT name AS Value FROM pragma_table_info('{table}')").ToListAsync();
    }

    private async Task<string?> IndexSqlAsync(string indexName) =>
        (await _db.Database.SqlQueryRaw<string>(
            $"SELECT sql AS Value FROM sqlite_master WHERE type = 'index' AND name = '{indexName}'").ToListAsync())
            .SingleOrDefault();

    private async Task<bool> TableExistsAsync(string table) =>
        await _db.Database.SqlQueryRaw<int>(
            $"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = '{table}'")
            .SingleAsync() == 1;

    [Fact]
    public async Task MigrateUp_CreatesSchema_WithPartialUniquesAndChecks()
    {
        await _db.Database.MigrateAsync();

        Assert.True(await TableExistsAsync("Sections"));
        Assert.True(await TableExistsAsync("HeadSuccessions"));

        Assert.Contains("SectionId", await TableColumnsAsync("Users"));
        Assert.Contains("CreatedById", await TableColumnsAsync("Users"));
        Assert.Contains("SectionId", await TableColumnsAsync("ExecutionCircuits"));
        Assert.Contains("ForwardState", await TableColumnsAsync("DocumentAppeals"));
        Assert.Contains("Version", await TableColumnsAsync("DocumentAppeals"));
        Assert.Contains("Version", await TableColumnsAsync("DocumentDelegations"));
        Assert.Contains("RedirectedToSectionId", await TableColumnsAsync("DocumentDelegations"));
        Assert.Contains("RejectReason", await TableColumnsAsync("DocumentDelegations"));
        Assert.Contains("RecipientSectionId", await TableColumnsAsync("Correspondences"));
        Assert.Contains("RecipientSectionId", await TableColumnsAsync("ReviewLetters"));

        // الوحدانية الجزئية على المفعّلين فقط — نص الفلتر حرفيًا.
        var subHeadIndex = await IndexSqlAsync("IX_Users_SectionId");
        Assert.Contains("SubHead", subHeadIndex);
        Assert.Contains("IsActive", subHeadIndex);
        var headIndex = await IndexSqlAsync("IX_Users_BranchId");
        Assert.Contains("'Head'", headIndex);

        // قيد الفرع يشمل رئيس الشعبة.
        var tableSql = (await _db.Database.SqlQueryRaw<string>(
            "SELECT sql AS Value FROM sqlite_master WHERE type = 'table' AND name = 'Users'").ToListAsync()).Single();
        Assert.Contains("SubHead", tableSql);
    }

    [Fact]
    public async Task MigrateUp_BackfillsLegacyRows_WithOwnedAndVersionOne()
    {
        // صفوف «قديمة»: تُدرج قبل الهجرة (بلا الأعمدة الجديدة) ثم تُرحَّل عند الصعود.
        await _db.Database.MigrateAsync(PreviousMigration);
        // المحامي القديم بفرع (قيد الفروع القديم `CK_Users_BranchRequiredForBranchRoles`
        // يفرض الفرع للمحامي منذ ما قبل هذه الهجرة).
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Branches\" (\"Name\", \"Code\", \"IsActive\", \"CreatedAt\") " +
            "VALUES ('دمشق', 'DAM', 1, '2026-01-01')");
        var branchId = (await _db.Database.SqlQueryRaw<int>(
            "SELECT \"Id\" AS Value FROM \"Branches\" WHERE \"Code\" = 'DAM'").ToListAsync()).Single();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Users\" (\"Username\", \"FullName\", \"Role\", \"PasswordHash\", \"BranchId\", \"IsActive\", \"FailedLoginCount\", \"TokenVersion\", \"CreatedAt\", \"UpdatedAt\") " +
            $"VALUES ('lawyer_old', 'محام قديم', 'Lawyer', 'x', {branchId}, 1, 0, 0, '2026-01-01', '2026-01-01')");
        var userId = (await _db.Database.SqlQueryRaw<int>(
            "SELECT \"Id\" AS Value FROM \"Users\" WHERE \"Username\" = 'lawyer_old'").ToListAsync()).Single();
        await InsertLegacyDocumentAsync(userId);
        var docId = (await _db.Database.SqlQueryRaw<int>(
            "SELECT \"Id\" AS Value FROM \"Documents\" WHERE \"FileNumber\" = '1'").ToListAsync()).Single();
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"DocumentAppeals\" (\"DocumentId\", \"Direction\", \"Status\", \"AppellantsJson\", \"AppelleesJson\", \"CreatedById\", \"CreatedAt\", \"UpdatedAt\") " +
            $"VALUES ({docId}, '{AppealDirectionCatalog.Appellants}', '{AppealStatusCatalog.Pending}', '[]', '[]', {userId}, '2026-01-01', '2026-01-01')");
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"DocumentDelegations\" (\"SourceDocumentId\", \"IsExternal\", \"Status\", \"CreatedById\", \"CreatedAt\", \"UpdatedAt\") " +
            $"VALUES ({docId}, 0, '{DelegationStatusCatalog.PendingHead}', {userId}, '2026-01-01', '2026-01-01')");

        await _db.Database.MigrateAsync();

        var forwardState = (await _db.Database.SqlQueryRaw<string>(
            "SELECT \"ForwardState\" AS Value FROM \"DocumentAppeals\"").ToListAsync()).Single();
        var appealVersion = (await _db.Database.SqlQueryRaw<long>(
            "SELECT \"Version\" AS Value FROM \"DocumentAppeals\"").ToListAsync()).Single();
        var delegationVersion = (await _db.Database.SqlQueryRaw<long>(
            "SELECT \"Version\" AS Value FROM \"DocumentDelegations\"").ToListAsync()).Single();

        Assert.Equal("Owned", forwardState);
        Assert.Equal(1L, appealVersion);
        Assert.Equal(1L, delegationVersion);
    }

    /// <summary>
    /// إدراج ملف «قديم» بلا تخمين أعمدة: تُقرأ الأعمدة الإلزامية بلا افتراضي من
    /// `pragma_table_info` ويُملأ كلٌّ بقيمة نوعية، مع تجاوزات صريحة للأعمدة ذات
    /// القيود (`GeneralEntitySide`) والقيم المقصودة — فيتكيف الاختبار مع أي عمود
    /// مستقبلي بلا كسر.
    /// </summary>
    private async Task InsertLegacyDocumentAsync(int userId)
    {
        RequireAllowedTable("Documents");
        var names = await _db.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM pragma_table_info('Documents') ORDER BY cid").ToListAsync();
        var notnulls = await _db.Database.SqlQueryRaw<int>(
            "SELECT \"notnull\" AS Value FROM pragma_table_info('Documents') ORDER BY cid").ToListAsync();
        var defaults = await _db.Database.SqlQueryRaw<string?>(
            "SELECT dflt_value AS Value FROM pragma_table_info('Documents') ORDER BY cid").ToListAsync();
        var types = await _db.Database.SqlQueryRaw<string>(
            "SELECT type AS Value FROM pragma_table_info('Documents') ORDER BY cid").ToListAsync();
        var pks = await _db.Database.SqlQueryRaw<int>(
            "SELECT pk AS Value FROM pragma_table_info('Documents') ORDER BY cid").ToListAsync();

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileNumber"] = "'1'",
            ["CreatedById"] = userId.ToString(),
            ["CreatedAt"] = "'2026-01-01'",
            ["UpdatedAt"] = "'2026-01-01'",
            ["GeneralEntitySide"] = $"'{GeneralEntitySideCatalog.Applicant}'",
        };

        var columns = new List<string>();
        var values = new List<string>();
        for (var i = 0; i < names.Count; i++)
        {
            if (pks[i] == 1)
                continue; // المفتاح الذاتي التزايد.
            if (overrides.TryGetValue(names[i], out var forced))
            {
                columns.Add($"\"{names[i]}\"");
                values.Add(forced);
            }
            else if (notnulls[i] == 1 && defaults[i] is null)
            {
                // إلزامي بلا افتراضي: قيمة نوعية (NULL تجتاز قيود `CHECK` لكنها
                // مرفوضة هنا بـ NOT NULL).
                columns.Add($"\"{names[i]}\"");
                values.Add(LiteralFor(types[i]));
            }
            // ما عداها (قابل للفراغ أو له افتراضي): يُترك للقاعدة.
        }

        await _db.Database.ExecuteSqlRawAsync(
            $"INSERT INTO \"Documents\" ({string.Join(", ", columns)}) VALUES ({string.Join(", ", values)})");
    }

    private static string LiteralFor(string sqliteType)
    {
        var t = sqliteType.ToUpperInvariant();
        if (t.Contains("INT") || t is "REAL" or "FLOA" or "DOUB" or "DECIMAL" or "NUMERIC" or "BOOL")
            return "0";
        if (t.Contains("DATE") || t.Contains("TIME"))
            return "'2026-01-01'";
        return "'t'";
    }

    private static void RequireAllowedTable(string table)
    {
        if (!AllowedTables.Contains(table))
            throw new ArgumentException($"اسم جدول غير مسموح في فحص الاختبار: {table}", nameof(table));
    }
#pragma warning restore EF1002, EF1003

    [Fact]
    public async Task MigrateDownThenUp_RollbackPath_RestoresSchema()
    {
        await _db.Database.MigrateAsync();
        Assert.True(await TableExistsAsync("Sections"));

        await _db.Database.MigrateAsync(PreviousMigration);

        Assert.False(await TableExistsAsync("Sections"));
        Assert.False(await TableExistsAsync("HeadSuccessions"));
        Assert.DoesNotContain("SectionId", await TableColumnsAsync("Users"));

        await _db.Database.MigrateAsync();

        Assert.True(await TableExistsAsync("Sections"));
        Assert.Contains("SectionId", await TableColumnsAsync("Users"));
    }
}
