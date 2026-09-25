using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولة سلامة سباق الترقيم (F1): تسطيرات متزامنة على الفرع نفسه عبر اتصالات
/// مستقلة وملف مشترك (WAL) — الكل ينجح بأرقام مميزة، وأي تصادم مرشّحين يُحل
/// بإعادة التوليد داخل الخدمة لا بخطأ 500. النتيجة حتمية أيًّا كان التداخل
/// المتحقق (تصادم أم لا)، ومسار التعارض الحتمي مغطى في CorrespondenceServiceTests.
/// </summary>
public class CorrespondenceConcurrencyTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();
    private readonly List<SqliteConnection> _connections = new();

    public void Dispose()
    {
        foreach (var c in _connections)
            c.Dispose();
        try { File.Delete(_path); } catch { /* ملف مؤقت — التجاهل آمن */ }
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection($"Data Source={_path}");
        await connection.OpenAsync();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 10000;";
        await pragma.ExecuteNonQueryAsync();
        _connections.Add(connection);
        return connection;
    }

    private static DbContextOptions<DocGeneratorDbContext> Options(SqliteConnection connection)
        => new DbContextOptionsBuilder<DocGeneratorDbContext>().UseSqlite(connection).Options;

    private static ICorrespondenceService BuildService(DocGeneratorDbContext db, FakeAuditLogger audit)
        => new CorrespondenceService(
            new CorrespondenceRepository(db),
            new DocumentRepository(db),
            new BranchRepository(db),
            new UserRepository(db),
            new AppealRepository(db),
            new DelegationRepository(db),
            new PortalRepository(db),
            new UnitOfWork(db),
            new TransactionRunner(db),
            audit,
            new DbExceptionClassifier(),
            TimeProvider.System, TestClock.TimeZone);

    private async Task<(int LawyerId, int DelegateId, int BranchId)> SeedAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(Options(connection));
        await db.Database.EnsureCreatedAsync();

        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var lawyer = new User
        {
            Username = "race_lawyer",
            FullName = "محامي السباق",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        var raceDelegate = new User
        {
            Username = "race_delegate",
            FullName = "مندوب السباق",
            Role = UserRole.EntityManager,
            PasswordHash = "x",
        };
        db.Users.AddRange(lawyer, raceDelegate);
        await db.SaveChangesAsync();
        return (lawyer.Id, raceDelegate.Id, branch.Id);
    }

    private sealed record RaceOutcome(string? CorrespondenceNumber, int AuditCount, Exception? Error);

    [Fact]
    public async Task Create_ConcurrentSameBranch_AllSucceedWithDistinctNumbers()
    {
        const int writers = 16;
        var (lawyerId, delegateId, branchId) = await SeedAsync();

        async Task<RaceOutcome> RunAsync(int i)
        {
            try
            {
                var connection = await OpenConnectionAsync();
                await using var db = new DocGeneratorDbContext(Options(connection));
                var audit = new FakeAuditLogger();
                var service = BuildService(db, audit);
                var letter = await service.CreateAsync(
                    new CreateCorrespondenceRequest(null, delegateId, "normal", $"<p>سباق {i}</p>"),
                    lawyerId, "محامي السباق", UserRole.Lawyer, branchId);
                return new RaceOutcome(
                    letter.CorrespondenceNumber,
                    audit.Actions.Count(a => a == "create_correspondence"),
                    null);
            }
            catch (Exception ex)
            {
                return new RaceOutcome(null, 0, ex);
            }
        }

        var outcomes = await Task.WhenAll(Enumerable.Range(0, writers).Select(i => Task.Run(() => RunAsync(i))));

        Assert.All(outcomes, o => Assert.Null(o.Error));
        var numbers = outcomes.Select(o => o.CorrespondenceNumber!).ToList();
        Assert.Equal(writers, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.StartsWith("DAM-", n));
        // كل تسطير سجّل تدقيقه مرة واحدة داخل معاملته الناجحة.
        Assert.All(outcomes, o => Assert.Equal(1, o.AuditCount));

        await using var verifyDb = new DocGeneratorDbContext(Options(await OpenConnectionAsync()));
        Assert.Equal(writers, await verifyDb.Correspondences.CountAsync());
    }
}
