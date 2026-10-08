using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولات سلامة سباق نقل الدوائر وتوجيه الإنابات (المرحلة 9):
/// نقل مزدوج للدائرة نفسها + توجيه مزدوج للإنابة نفسها عبر اتصالين مستقلين —
/// فائز واحد وحسم ودي للخاسر (`DocumentConflictException`/`DbUpdateConcurrencyException`
/// للتحويل والحارس الصريح للتوجيه)، بلا كتابة مفقودة صامتة.
/// ملاحظة المنهج (مرآة `DelegationConcurrencyTests`): تسلسل SQLite الذاتي قد
/// يمنع التداخل الكامل، فتثبت الجولات السلامة في كل تداخل متحقق، والمسار
/// الحتمي مغطى باختبارات النسخة القديمة أدناه.
/// </summary>
public class TransferRedirectConcurrencyTests : IDisposable
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

    private static ExecutionCircuitService CircuitService(DocGeneratorDbContext db)
    {
        var uow = new UnitOfWork(db);
        return new ExecutionCircuitService(
            new Repository<ExecutionCircuit>(db),
            new Repository<Branch>(db),
            new Repository<Section>(db),
            new DocumentRepository(db),
            new DelegationRepository(db),
            new AppealRepository(db),
            new Repository<DocumentOccurrence>(db),
            new Repository<DocumentBaseNumber>(db),
            new Repository<HeadSuccession>(db),
            new HeadAlertRepository(db),
            new UserRepository(db),
            uow,
            new TransactionRunner(db),
            new FakeAuditLogger(),
            new DbExceptionClassifier(),
            TimeProvider.System,
            TestClock.TimeZone);
    }

    private async Task<(int BranchId, int SectionA, int SectionB, int CircuitId, int ManagerId, int HeadId)> SeedTransferAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(Options(connection));
        await db.Database.EnsureCreatedAsync();

        var branch = new Branch { Name = "حماة", Code = "HAM", Governorate = "حماة" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        Section MakeSection(string name)
        {
            var s = new Section
            {
                Name = name,
                NameNorm = ArabicNameNormalizer.Normalize(name),
                BranchId = branch.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Sections.Add(s);
            return s;
        }
        var sectionA = MakeSection("شعبة مصياف");
        var sectionB = MakeSection("شعبة السلمية");
        await db.SaveChangesAsync();

        var manager = new User { Username = "race_mgr", FullName = "المدير", Role = UserRole.Manager, PasswordHash = "x" };
        var head = new User { Username = "race_head", FullName = "رئيس القسم", Role = UserRole.Head, BranchId = branch.Id, PasswordHash = "x" };
        var subA = new User { Username = "race_sub_a", FullName = "رئيس مصياف", Role = UserRole.SubHead, BranchId = branch.Id, SectionId = sectionA.Id, PasswordHash = "x" };
        var subB = new User { Username = "race_sub_b", FullName = "رئيس السلمية", Role = UserRole.SubHead, BranchId = branch.Id, SectionId = sectionB.Id, PasswordHash = "x" };
        db.Users.AddRange(manager, head, subA, subB);
        await db.SaveChangesAsync();

        var circuit = new ExecutionCircuit
        {
            BranchId = branch.Id,
            SectionId = null,
            Name = "دائرة السباق",
            NameNorm = ArabicNameNormalizer.Normalize("دائرة السباق"),
            IsActive = true,
            CreatedById = head.Id,
        };
        db.ExecutionCircuits.Add(circuit);
        await db.SaveChangesAsync();

        return (branch.Id, sectionA.Id, sectionB.Id, circuit.Id, manager.Id, head.Id);
    }

    private DocGeneratorDbContext OpenDb(SqliteConnection connection)
        => new(Options(connection));

    [Fact]
    public async Task Transfer_StaleVersion_ThrowsFriendlyConflict_AndBumpsVersion()
    {
        var seed = await SeedTransferAsync();
        var connection = _connections[^1];
        await using var db = OpenDb(connection);
        var service = CircuitService(db);

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            service.TransferCircuitAsync(seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr", default, 9999));

        var before = (await service.TransferCircuitAsync(seed.CircuitId, null, seed.ManagerId, "mgr")).Version;
        var moved = await service.TransferCircuitAsync(seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr");
        Assert.Equal(seed.SectionA, moved.SectionId);
        Assert.Equal(before + 1, moved.Version);

        // النقل لذات المالك عملية خاملة — بلا رفع رمز.
        var noop = await service.TransferCircuitAsync(seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr", default, moved.Version);
        Assert.Equal(moved.Version, noop.Version);
    }

    [Fact]
    public async Task Transfer_DoubleTransfer_OneWins_AndLoserIsFriendly()
    {
        var seed = await SeedTransferAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = OpenDb(taskConnection);
                try
                {
                    await CircuitService(db).TransferCircuitAsync(seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr");
                    return "ok-a";
                }
                catch (Exception ex) { return ex.GetType().Name; }
            }),
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = OpenDb(taskConnection);
                try
                {
                    await CircuitService(db).TransferCircuitAsync(seed.CircuitId, seed.SectionB, seed.ManagerId, "mgr");
                    return "ok-b";
                }
                catch (Exception ex) { return ex.GetType().Name; }
            }));

        // الفائز وحده ينجح أو يتسلسلان؛ الخاسر — إن وُجد — 409 ودية حصرًا
        // (تعارض الرمز الممسوك خدميًا)، بلا 500 خام.
        Assert.Contains(outcomes, o => o == "ok-a" || o == "ok-b");
        Assert.All(outcomes, o => Assert.True(
            o is "ok-a" or "ok-b" or nameof(DocumentConflictException),
            $"خسارة غير ودية: {o}"));

        var checkConnection = await OpenConnectionAsync();
        await using var check = OpenDb(checkConnection);
        var circuit = await check.ExecutionCircuits.AsNoTracking().SingleAsync(c => c.Id == seed.CircuitId);
        Assert.True(circuit.SectionId is null || circuit.SectionId == seed.SectionA || circuit.SectionId == seed.SectionB);
        // الرمز يعكس عدد الكتابات الناجحة فعلًا — بلا قفزات مفقودة.
        var wins = outcomes.Count(o => o is "ok-a" or "ok-b");
        Assert.Equal(wins, circuit.Version);
    }

    private static IDocumentDelegationService DelegationService(DocGeneratorDbContext db)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var branches = new Repository<Branch>(db);
        var uow = new UnitOfWork(db);
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
            new TransactionRunner(db),
            audit,
            headAlerts,
            TimeProvider.System,
            TestClock.TimeZone,
            new Repository<ExecutionCircuit>(db),
            new Repository<Section>(db));
    }

    private async Task<(int DelegationId, int SectionA, int SectionB, int HeadId)> SeedRedirectAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(Options(connection));
        await db.Database.EnsureCreatedAsync();

        var src = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        var dst = new Branch { Name = "حماة", Code = "HAM", Governorate = "حماة" };
        db.Branches.AddRange(src, dst);
        await db.SaveChangesAsync();

        Section MakeSection(string name)
        {
            var s = new Section
            {
                Name = name,
                NameNorm = ArabicNameNormalizer.Normalize(name),
                BranchId = dst.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.Sections.Add(s);
            return s;
        }
        var sectionA = MakeSection("شعبة مصياف");
        var sectionB = MakeSection("شعبة السلمية");
        await db.SaveChangesAsync();

        var lawyer = new User { Username = "race_lawyer", FullName = "محامي السباق", Role = UserRole.Lawyer, BranchId = src.Id, PasswordHash = "x" };
        var head = new User { Username = "race_head", FullName = "رئيس حماة", Role = UserRole.Head, BranchId = dst.Id, PasswordHash = "x" };
        var subA = new User { Username = "race_sub_a", FullName = "رئيس مصياف", Role = UserRole.SubHead, BranchId = dst.Id, SectionId = sectionA.Id, PasswordHash = "x" };
        var subB = new User { Username = "race_sub_b", FullName = "رئيس السلمية", Role = UserRole.SubHead, BranchId = dst.Id, SectionId = sectionB.Id, PasswordHash = "x" };
        db.Users.AddRange(lawyer, head, subA, subB);
        await db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = lawyer.Id,
            BranchId = src.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            FileNumber = "520/2024",
            FileType = "تنفيذي",
            FileYear = "2024",
            Court = "دمشق",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        db.Documents.Add(source);
        await db.SaveChangesAsync();

        var delegation = new DocumentDelegation
        {
            SourceDocumentId = source.Id,
            DelegatedCourt = "دائرة خارجية",
            IsExternal = true,
            ExternalBranchId = dst.Id,
            CreatedById = lawyer.Id,
            Version = 1,
        };
        db.DocumentDelegations.Add(delegation);
        await db.SaveChangesAsync();

        return (delegation.Id, sectionA.Id, sectionB.Id, head.Id);
    }

    [Fact]
    public async Task Redirect_DoubleRedirect_ExactlyOneWins_AndLoserIsFriendly()
    {
        var seed = await SeedRedirectAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = OpenDb(taskConnection);
                try
                {
                    await DelegationService(db).RedirectToSectionAsync(
                        seed.DelegationId, new RedirectDelegationRequest(seed.SectionA), seed.HeadId, "head");
                    return "ok-a";
                }
                catch (Exception ex) { return ex.GetType().Name; }
            }),
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = OpenDb(taskConnection);
                try
                {
                    await DelegationService(db).RedirectToSectionAsync(
                        seed.DelegationId, new RedirectDelegationRequest(seed.SectionB), seed.HeadId, "head");
                    return "ok-b";
                }
                catch (Exception ex) { return ex.GetType().Name; }
            }));

        // التوجيه الثاني مستحيل (حارس «موجَّهة سلفًا» أو تعارض ممسوك خدميًا) — فائز واحد حتمًا.
        var wins = outcomes.Count(o => o is "ok-a" or "ok-b");
        Assert.Equal(1, wins);
        Assert.All(outcomes, o => Assert.True(
            o is "ok-a" or "ok-b" or nameof(ArgumentException) or nameof(DocumentConflictException),
            $"خسارة غير ودية: {o}"));

        await using var check = OpenDb(await OpenConnectionAsync());
        var delegation = await check.DocumentDelegations.AsNoTracking().SingleAsync(d => d.Id == seed.DelegationId);
        Assert.True(delegation.RedirectedToSectionId == seed.SectionA || delegation.RedirectedToSectionId == seed.SectionB);
        Assert.Equal(2, delegation.Version);
    }
}
