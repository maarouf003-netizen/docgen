using System.Data.Common;
using System.Diagnostics;
using DocGenerator.Application.Common;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit.Abstractions;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قياس F-03: بحث بوابة المندوب الاسمي على حجم إنتاجي (10k ملف) مع كفيل فريد
/// <b>خارج</b> <c>SearchText</c> — يمرّ حصرًا بفرع <c>DocumentPartySearch</c> على
/// <c>Guarantors</c>. يُثبت الصحة والعزل حتميًا، ويسجّل الزمن وخطة التنفيذ في
/// مخرجات الاختبار (لا حدّ زمني حاجب — اتباعًا لروح M6/M24: الزمن الجداري هشّ).
/// </summary>
public sealed class PortalSearchMeasurementTests : IDisposable
{
    private const int Count = 10_000;

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DocGeneratorDbContext> _options;
    private readonly SqlCaptureInterceptor _capture = new();
    private readonly ITestOutputHelper _output;
    private readonly int _delegateUserId;
    private readonly int _entryId;
    private readonly int _targetDocumentId;

    public PortalSearchMeasurementTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(_connection, sqlite =>
                sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
            .AddInterceptors(_capture)
            .Options;

        using var db = new DocGeneratorDbContext(_options);
        db.Database.EnsureCreated();

        var branch = new Branch { Name = "فرع قياس البحث", Code = "PSMEAS" };
        db.Branches.Add(branch);
        db.SaveChanges();

        var creator = new User
        {
            Username = "portal_search_creator",
            FullName = "منشئ القياس",
            Role = UserRole.Admin,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(creator);
        db.SaveChanges();

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
        db.SaveChanges();
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
        db.SaveChanges();
        _delegateUserId = delegateUser.Id;

        var docs = new List<Document>(Count + 2);
        for (var i = 0; i < Count; i++)
            docs.Add(NewDocument(creator.Id, $"مقترض {i}", $"نص {i}", $"كفيل {i}", inScope: true));

        var target = NewDocument(creator.Id, "مقترض الهدف", string.Empty, "كفيل فريد", inScope: true);
        docs.Add(target);

        // ملف بنفس اسم الكفيل الفريد لكن خارج نطاق المندوب: يجب ألا يتسرب.
        docs.Add(NewDocument(creator.Id, "مقترض خارجي", "نص خارجي", "كفيل فريد", inScope: false));

        db.Documents.AddRange(docs);
        db.SaveChanges();
        _targetDocumentId = target.Id;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task PortalNameSearch_Over10kDocs_FindsGuarantorOutsideSearchText_AndStaysScoped()
    {
        await using var db = new DocGeneratorDbContext(_options);
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

        var plan = ExplainLastCommand();
        _output.WriteLine("F-03: plan = " + string.Join(" | ", plan));
        // بنيوي فقط (نص الخطة يتبدل بين إصدارات SQLite): فرع الكفيل مسبار مفهرس
        // على `IX_Guarantors_DocumentId`، لا مسح كامل لجدول `Guarantors`.
        // المسح الخارجي لـ`Documents` متوقع ومقبول: البحث OR عبر عدة جداول أطراف
        // لا يمكن أن يستخدم فهرسًا واحدًا للجذر.
        Assert.Contains(plan, d =>
            d.StartsWith("SEARCH", StringComparison.OrdinalIgnoreCase)
            && d.Contains("IX_Guarantors_DocumentId", StringComparison.OrdinalIgnoreCase));

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
            CreatedAt = new DateTime(2024, 6, 15),
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

    private List<string> ExplainLastCommand()
    {
        var sql = _capture.LastCommandText
            ?? throw new InvalidOperationException("لم يُلتقط استعلام البحث");
        using var command = _connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        foreach (var (name, value) in _capture.LastParameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        using var reader = command.ExecuteReader();
        var details = new List<string>();
        while (reader.Read())
            details.Add(reader.GetString(3));
        return details;
    }

    /// <summary>يلتقط نص آخر استعلام ووسائطه لتمريره إلى `EXPLAIN QUERY PLAN`.</summary>
    private sealed class SqlCaptureInterceptor : DbCommandInterceptor
    {
        public string? LastCommandText { get; private set; }
        public IReadOnlyList<(string Name, object? Value)> LastParameters { get; private set; } =
            Array.Empty<(string, object?)>();

        public void Reset()
        {
            LastCommandText = null;
            LastParameters = Array.Empty<(string, object?)>();
        }

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
            LastCommandText = command.CommandText;
            LastParameters = command.Parameters
                .Cast<DbParameter>()
                .Select(p => (p.ParameterName, p.Value))
                .ToList();
        }
    }
}
