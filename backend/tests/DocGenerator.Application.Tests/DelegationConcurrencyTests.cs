using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولات سلامة السباق (B3): محاولات تسطير متزامنة على الأصل نفسه عبر اتصالين
/// مستقلين وخيطين — في كل جولة: فائز واحد + خطأ مجال («إنابة سارية») للخاسر.
/// ملاحظة تجريبية موثقة: في مكدس SQLite داخل العملية تتسلسل قراءات الحارس ذاتيًا
/// (أي انتظار داخل معاملة مفتوحة بعد قراءاتها يجمّد القارئ الآخر)، فلا يمكن إجبار
/// التداخل الكامل حتميًا من الخارج — هذه الجولات تثبت السلامة في كل تداخل متحقق،
/// ومسار التعارض الحتمي مغطى بـ DbExceptionClassifierTests + مراجعة موضع الالتقاط.
/// القيد الفريد ضروري وحاسم على PostgreSQL (MVCC حقيقي بلا تسلسل ذاتي).
/// </summary>
public class DelegationConcurrencyTests : IDisposable
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

    private static IDocumentDelegationService BuildService(DocGeneratorDbContext db)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var branches = new Repository<Branch>(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var audit = new FakeAuditLogger();
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(db), documents, users, branches, uow, new TransactionRunner(db), audit);
        return new DocumentDelegationService(
            new DelegationRepository(db),
            new DelegationReservationRepository(db),
            new DbExceptionClassifier(),
            documents,
            users,
            branches,
            new Repository<DocumentRegistrationDate>(db),
            new Repository<DocumentOccurrence>(db),
            uow,
            tx,
            audit,
            headAlerts,
            TimeProvider.System,
            TestClock.TimeZone);
    }

    private async Task<(int SourceId, List<int> AssetIds, int LawyerId)> SeedAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(Options(connection));
        await db.Database.EnsureCreatedAsync();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
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
        db.Users.Add(lawyer);
        await db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = lawyer.Id,
            BranchId = branch.Id,
            BranchName = branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = "520",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = "أحمد الخطيب المدعي 520",
        };
        db.Documents.Add(source);
        await db.SaveChangesAsync();

        var assetIds = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            var asset = new Asset
            {
                DocumentId = source.Id,
                AssetKind = AssetKindCatalog.RealEstate,
                PropertyNumber = (77 + i).ToString(),
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
            assetIds.Add(asset.Id);
        }

        return (source.Id, assetIds, lawyer.Id);
    }

    private static UpsertDelegationRequest Request(int assetId) => new(
        DelegatedCourt: "دائرة تنفيذ حلب",
        IsExternal: false,
        ExternalBranchId: null,
        DelegationDate: "1/8/2026",
        DelegationText: "سباق على الأصل نفسه",
        DepositBookNumber: "كتاب-1",
        DepositBookDate: "2/8/2026",
        AssetIds: new List<int> { assetId });

    private sealed record RaceOutcome(DelegationDto? Dto, Exception? Error);

    [Fact]
    public async Task ConcurrentCreate_OnSameAsset_ExactlyOneWins_WithDomainError()
    {
        var (sourceId, assetIds, lawyerId) = await SeedAsync();

        var connectionA = await OpenConnectionAsync();
        var connectionB = await OpenConnectionAsync();
        await using var dbA = new DocGeneratorDbContext(Options(connectionA));
        await using var dbB = new DocGeneratorDbContext(Options(connectionB));
        var serviceA = BuildService(dbA);
        var serviceB = BuildService(dbB);

        async Task<RaceOutcome> RunAsync(IDocumentDelegationService service, int assetId)
        {
            try
            {
                var dto = await service.CreateAsync(sourceId, Request(assetId), lawyerId, "race");
                return new RaceOutcome(dto, null);
            }
            catch (Exception ex)
            {
                return new RaceOutcome(null, ex);
            }
        }

        foreach (var assetId in assetIds)
        {
            var outcomes = await Task.WhenAll(
                Task.Run(() => RunAsync(serviceA, assetId)),
                Task.Run(() => RunAsync(serviceB, assetId)));

            Assert.Single(outcomes, o => o.Dto is not null);
            var loser = Assert.Single(outcomes, o => o.Error is not null);
            var domainError = Assert.IsType<ArgumentException>(loser.Error);
            Assert.Contains("إنابة سارية", domainError.Message);
        }

        await using var verifyDb = new DocGeneratorDbContext(Options(await OpenConnectionAsync()));
        Assert.Equal(assetIds.Count, await verifyDb.DocumentDelegations.CountAsync(d => d.SourceDocumentId == sourceId));
        Assert.Equal(assetIds.Count, await verifyDb.DelegationAssetReservations.CountAsync(r => r.SourceDocumentId == sourceId));
    }
}
