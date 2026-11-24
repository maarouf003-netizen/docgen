using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Correspondences;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولة سلامة سباق الترقيم (F1): تسطيرات متزامنة على الفرع نفسه عبر سياقات
/// مستقلة على قاعدة <c>PostgreSQL</c> واحدة (‏`ADR-0009`) — الكل ينجح بأرقام مميزة،
/// وأي تصادم مرشّحين يُحل بإعادة التوليد داخل الخدمة لا بخطأ 500.
///
/// على `Postgres` يصل اصطدام الفهرس الفريد كـ`23505` فيُجهض المعاملة (بخلاف `SQLite`)
/// فيتراجع `TransactionRunner` ويعيد التوليد في معاملة جديدة؛ النتيجة الحتمية هي
/// نجاح الجميع بأرقام مميزة أيًّا كان التداخل المتحقق.
/// </summary>
public class CorrespondenceConcurrencyTests : IAsyncLifetime
{
    private PostgresTestDb _pg = null!;

    public async Task InitializeAsync() => _pg = await PostgresTestDb.CreateAsync();

    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private static CorrespondenceService BuildService(DocGeneratorDbContext db, FakeAuditLogger audit)
        => new CorrespondenceService(
            new CorrespondenceRepository(db),
            new DocumentRepository(db),
            new BranchRepository(db),
            new UserRepository(db),
            new AppealRepository(db),
            new DelegationRepository(db),
            new PortalRepository(db),
            TestClock.Provider, TestClock.TimeZone,
            new Repository<Section>(db),
            new Repository<ExecutionCircuit>(db));

    private static ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> BuildCreateHandler(
        DocGeneratorDbContext db, CorrespondenceService service, FakeAuditLogger audit)
        => new CreateCorrespondenceCommandHandler(
            (ICorrespondenceWriteRules)service,
            new UserRepository(db),
            new DocumentRepository(db),
            new CorrespondenceRepository(db),
            new UnitOfWork(db),
            new TransactionRunner(db),
            audit,
            TestClock.Provider,
            new DbExceptionClassifier(),
            Array.Empty<IPipelineBehavior<CreateCorrespondenceCommand, CorrespondenceDto>>());

    private async Task<(int LawyerId, int DelegateId, int BranchId)> SeedAsync()
    {
        await using var db = _pg.CreateContext();

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

    [PostgresFact]
    public async Task Create_ConcurrentSameBranch_AllSucceedWithDistinctNumbers()
    {
        const int writers = 16;
        var (lawyerId, delegateId, branchId) = await SeedAsync();

        async Task<RaceOutcome> RunAsync(int i)
        {
            try
            {
                await using var db = _pg.CreateContext();
                var audit = new FakeAuditLogger();
                var service = BuildService(db, audit);
                var create = BuildCreateHandler(db, service, audit);
                var letter = await create.CreateAsync(
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

        await using var verifyDb = _pg.CreateContext();
        Assert.Equal(writers, await verifyDb.Correspondences.CountAsync());
    }
}
