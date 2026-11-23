using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.ExecutionCircuits;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولات سلامة سباق نقل الدوائر وتوجيه الإنابات (المرحلة 9):
/// نقل مزدوج للدائرة نفسها + توجيه مزدوج للإنابة نفسها عبر سياقين مستقلين على قاعدة
/// <c>PostgreSQL</c> واحدة (‏`ADR-0009`) — فائز واحد وحسم ودي للخاسر
/// (`DocumentConflictException`/`DbUpdateConcurrencyException` للتحويل والحارس الصريح
/// للتوجيه)، بلا كتابة مفقودة صامتة.
///
/// <para>على `Postgres` (`MVCC`/`READ COMMITTED`) التداخل حقيقي: رمز النسخة
/// (<c>WHERE Version = old</c>) هو الحاسم، لا تسلسل `SQLite` الذاتي. المسار الحتمي
/// (نسخة قديمة صريحة) مغطى في الاختبار الأول.</para>
/// </summary>
public class TransferRedirectConcurrencyTests : IAsyncLifetime
{
    private PostgresTestDb _pg = null!;

    public async Task InitializeAsync() => _pg = await PostgresTestDb.CreateAsync();

    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private static TransferExecutionCircuitCommandHandler CircuitTransferHandler(DocGeneratorDbContext db)
    {
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        return new TransferExecutionCircuitCommandHandler(
            new Repository<ExecutionCircuit>(db),
            new Repository<Section>(db),
            new DocumentRepository(db),
            new DelegationRepository(db),
            new AppealRepository(db),
            new Repository<HeadSuccession>(db),
            new UserRepository(db),
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            uow,
            tx,
            new FakeAuditLogger(),
            new DbExceptionClassifier(),
            Array.Empty<IPipelineBehavior<TransferExecutionCircuitCommand, ExecutionCircuitDto>>());
    }

    private static Task<ExecutionCircuitDto> TransferAsync(
        DocGeneratorDbContext db, int circuitId, int? targetSectionId, int actorUserId, string? actorName,
        CancellationToken ct = default, long? version = null)
        => CircuitTransferHandler(db).HandleAsync(
            new TransferExecutionCircuitCommand(circuitId, targetSectionId, actorUserId, actorName, version), ct);

    private async Task<(int BranchId, int SectionA, int SectionB, int CircuitId, int ManagerId, int HeadId)> SeedTransferAsync()
    {
        await using var db = _pg.CreateContext();

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

    [PostgresFact]
    public async Task Transfer_StaleVersion_ThrowsFriendlyConflict_AndBumpsVersion()
    {
        var seed = await SeedTransferAsync();
        await using var db = _pg.CreateContext();

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            TransferAsync(db, seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr", default, 9999));

        var before = (await TransferAsync(db, seed.CircuitId, null, seed.ManagerId, "mgr")).Version;
        var moved = await TransferAsync(db, seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr");
        Assert.Equal(seed.SectionA, moved.SectionId);
        Assert.Equal(before + 1, moved.Version);

        // النقل لذات المالك عملية خاملة — بلا رفع رمز.
        var noop = await TransferAsync(db, seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr", default, moved.Version);
        Assert.Equal(moved.Version, noop.Version);
    }

    [PostgresFact]
    public async Task Transfer_DoubleTransfer_OneWins_AndLoserIsFriendly()
    {
        var seed = await SeedTransferAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var db = _pg.CreateContext();
                try
                {
                    await TransferAsync(db, seed.CircuitId, seed.SectionA, seed.ManagerId, "mgr");
                    return "ok-a";
                }
                catch (Exception ex) { return ex.GetType().Name; }
            }),
            Task.Run(async () =>
            {
                await using var db = _pg.CreateContext();
                try
                {
                    await TransferAsync(db, seed.CircuitId, seed.SectionB, seed.ManagerId, "mgr");
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

        await using var check = _pg.CreateContext();
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
        var tx = new TransactionRunner(db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(db), documents, users, branches,
            new DelegationRepository(db), uow, tx, audit);
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
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            TestClock.Provider,
            TestClock.TimeZone,
            new Repository<ExecutionCircuit>(db),
            new Repository<Section>(db));
    }

    private async Task<(int DelegationId, int SectionA, int SectionB, int HeadId)> SeedRedirectAsync()
    {
        await using var db = _pg.CreateContext();

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

    [PostgresFact]
    public async Task Redirect_DoubleRedirect_ExactlyOneWins_AndLoserIsFriendly()
    {
        var seed = await SeedRedirectAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var db = _pg.CreateContext();
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
                await using var db = _pg.CreateContext();
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

        await using var check = _pg.CreateContext();
        var delegation = await check.DocumentDelegations.AsNoTracking().SingleAsync(d => d.Id == seed.DelegationId);
        Assert.True(delegation.RedirectedToSectionId == seed.SectionA || delegation.RedirectedToSectionId == seed.SectionB);
        Assert.Equal(2, delegation.Version);
    }
}
