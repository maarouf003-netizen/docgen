using System.Data.Common;
using System.Diagnostics;
using DocGenerator.Application.Common;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit.Abstractions;

namespace DocGenerator.Application.Tests;

/// <summary>
/// F-03 على <c>PostgreSQL</c> الحقيقي (‏`ADR-0009`): قياس صحة وعزل بحث البوابة على
/// 10k مستند، حيث اسم الكفيل الفريد موجود **خارج** <c>SearchText</c> — يمرّ حصرًا
/// بفرع <c>DocumentPartySearch</c> على <c>Guarantors</c>. يُثبت الصحة والعزل حتميًا،
/// ويسجّل الزمن وخطة التنفيذ في مخرجات الاختبار (لا حدّ زمني حاجب — اتباعًا لروح
/// M6/M24: الزمن الجداري هشّ).
/// </summary>
public sealed class PortalSearchMeasurementTests : IAsyncLifetime
{
    private const int Count = 10_000;

    private readonly SqlCaptureInterceptor _capture = new();
    private readonly ITestOutputHelper _output;
    private PostgresTestDb _db = null!;
    private DbContextOptions<DocGeneratorPostgresDbContext> _options = null!;
    private int _delegateUserId;
    private int _entryId;
    private int _targetDocumentId;

    public PortalSearchMeasurementTests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _db = await PostgresTestDb.CreateAsync();
        _options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>(_db.Options)
            .AddInterceptors(_capture)
            .Options;

        await using var db = new DocGeneratorPostgresDbContext(_options);

        var branch = new Branch { Name = "فرع قياس البحث", Code = "PSMEAS" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var creator = new User
        {
            Username = "portal_search_creator",
            FullName = "منشئ القياس",
            Role = UserRole.Admin,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(creator);
        await db.SaveChangesAsync();

        var group = new PublicEntityGroup
        {
            CanonicalName = "جهة قياس بحث البوابة",
            EntityType = PublicEntityTypeCatalog.Ministry,
        };
        group.Entries.Add(new PublicEntity
        {
            Governorate = "دمشق",
            BranchName = "الفرع الرئيسي",
            Status = EntityStatusCatalog.Final,
            IsActive = true,
            NeedsReview = false,
            CreatedById = creator.Id,
            CreatedAt = DateTime.UtcNow,
        });
        db.PublicEntityGroups.Add(group);
        await db.SaveChangesAsync();
        _entryId = group.Entries.Single().Id;

        var delegateUser = new User
        {
            Username = "portal_search_delegate",
            FullName = "مندوب القياس",
            Role = UserRole.EntityManager,
            PortalGroupId = group.Id,
            PasswordHash = "x",
        };
        db.Users.Add(delegateUser);
        await db.SaveChangesAsync();
        _delegateUserId = delegateUser.Id;

        var docs = new List<Document>(Count + 2);
        for (var i = 0; i < Count; i++)
            docs.Add(NewDocument(creator.Id, $"مقترض {i}", $"نص {i}", $"كفيل {i}", inScope: true));

        var target = NewDocument(creator.Id, "مقترض الهدف", string.Empty, "كفيل فريد", inScope: true);
        docs.Add(target);

        // ملف بنفس اسم الكفيل الفريد لكن خارج نطاق المندوب: يجب ألا يتسرب.
        docs.Add(NewDocument(creator.Id, "مقترض خارجي", "نص خارجي", "كفيل فريد", inScope: false));

        db.Documents.AddRange(docs);
        await db.SaveChangesAsync();
        _targetDocumentId = target.Id;
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [PostgresFact]
    public async Task PortalNameSearch_Over10kDocs_FindsGuarantorOutsideSearchText_AndStaysScoped()
    {
        await using var db = new DocGeneratorPostgresDbContext(_options);
        var repository = new PortalRepository(db);

        _capture.Reset();
        var sw = Stopwatch.StartNew();
        var (total, items) = await repository.SearchScopedAsync(
            new[] { _entryId }, "كفيل فريد", null, 1, 20);
        sw.Stop();

        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(_targetDocumentId, items[0].Id);
        _output.WriteLine(
            $"F-03: SearchScopedAsync over {Count + 2} docs took {sw.ElapsedMilliseconds} ms (total={total})");

        var plan = ExplainCommandContaining("Guarantors");
        _output.WriteLine("F-03: plan = " + string.Join(" | ", plan));
        // بنيوي فقط (نص الخطة يتبدل بين الإصدارات والمزوّدين): فرع الكفيل يجب أن
        // يظهر في الخطة ولا تُختصر النتيجة قبل الوصول إليه. حارس «الفهرس بدل المسح
        // الكامل» على Postgres ينتمي إلى P3-01/P3-02 حيث تُبنى منصة القياس
        // (‏`pg_stat_statements`/`EXPLAIN ANALYZE`) لا إلى اختبار وحدة.
        Assert.NotEmpty(plan);
        Assert.Contains(plan, line => line.Contains("Guarantors", StringComparison.OrdinalIgnoreCase));

        // المسار الكامل عبر الخدمة (نطاق المندوب + الاستعلام) — نفس النتيجة والعزل.
        var portal = PortalServiceFactory.Create(db, new FakeAuditLogger());
        _capture.Reset();
        sw.Restart();
        var page = await portal.ListFilesAsync(
            _delegateUserId, "كفيل فريد", null, 1, 20);
        sw.Stop();

        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(_targetDocumentId, page.Items[0].Id);
        _output.WriteLine(
            $"F-03: ListFilesAsync (scope + search) took {sw.ElapsedMilliseconds} ms (total={page.TotalCount})");
    }

    private Document NewDocument(
        int createdById, string borrower, string searchText, string guarantorName, bool inScope)
    {
        var doc = new Document
        {
            CreatedById = createdById,
            IsDraft = false,
            BorrowerName = borrower,
            BorrowerFamily = "عائلة",
            SearchText = searchText,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            CreatedAt = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        };
        doc.Guarantors.Add(new Guarantor
        {
            GuarantorNumber = 1,
            GuarantorName = guarantorName,
            GuarantorFamily = "عائلة",
        });
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity
        {
            Name = borrower,
            Governorate = "دمشق",
            RegistryId = inScope ? _entryId : null,
        });
        if (inScope)
            doc.ApplicantRegistryId = _entryId;
        return doc;
    }

    private List<string> ExplainCommandContaining(string fragment)
    {
        var matches = _capture.Commands
            .Where(c => c.Text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 0)
            throw new InvalidOperationException($"لم يُلتقط استعلام يحتوي «{fragment}»");
        var captured = matches[^1];

        using var connection = new NpgsqlConnection(_db.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN " + captured.Text;
        foreach (var parameterInfo in captured.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterInfo.Name;
            parameter.Value = parameterInfo.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        using var reader = command.ExecuteReader();
        var details = new List<string>();
        while (reader.Read())
            details.Add(reader.GetString(0));
        return details;
    }

    /// <summary>يلتقط نصوص الاستعلامات ووسائطها لتمرير أحدها إلى <c>EXPLAIN</c>.</summary>
    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public List<(string Text, IReadOnlyList<(string Name, object? Value)> Parameters)> Commands { get; } = new();

        public void Reset() => Commands.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Capture(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return ValueTask.FromResult(result);
        }

        private void Capture(DbCommand command)
        {
            Commands.Add((command.CommandText, command.Parameters
                .Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value))
                .ToList()));
        }
    }
}
