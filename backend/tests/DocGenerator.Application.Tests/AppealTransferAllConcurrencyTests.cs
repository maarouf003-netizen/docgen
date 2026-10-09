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
/// سلامة سباق النقل الجملي للاستئنافات (F5): القراءة داخل المعاملة + إعادة فحص
/// `Pending` + رمز `Version` — مهمتان متزامنتان على المصدر نفسه عبر اتصالين مستقلين.
/// ما يُثبَت هنا بدقة: سلامة الحالة النهائية (المجموع المنقول = المنظور، بلا كتابة
/// مفقودة صامتة) وصنف الخسارة حصرًا (تعارض تزامن، يترجمه المعالج العام `409`) —
/// لا التداخل الكامل نفسه: تسلسل SQLite الذاتي قد يمنع التداخل، فهذه قرينة
/// دفاع-عمق لا برهان تداخل (مرآة `TransferRedirectConcurrencyTests`).
/// </summary>
public class AppealTransferAllConcurrencyTests : IDisposable
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

    private static IDocumentAppealService AppealService(DocGeneratorDbContext db)
    {
        var audit = new FakeAuditLogger();
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var alerts = new HeadAlertService(
            new HeadAlertRepository(db),
            new DocumentRepository(db),
            new UserRepository(db),
            new Repository<Branch>(db),
            uow, tx, audit);
        return new DocumentAppealService(
            new AppealRepository(db),
            new DocumentRepository(db),
            new UserRepository(db),
            uow, tx, audit, alerts,
            TimeProvider.System, TestClock.TimeZone);
    }

    private async Task<(int BranchId, int SourceId, int TargetId)> SeedAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(Options(connection));
        await db.Database.EnsureCreatedAsync();

        var branch = new Branch { Name = "حماة", Code = "HAM", Governorate = "حماة" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var head = new User { Username = "race_head", FullName = "رئيس القسم", Role = UserRole.Head, BranchId = branch.Id, PasswordHash = "x" };
        var source = new User { Username = "race_src", FullName = "المحامي المصدر", Role = UserRole.Lawyer, BranchId = branch.Id, PasswordHash = "x" };
        var target = new User { Username = "race_dst", FullName = "المحامي الهدف", Role = UserRole.Lawyer, BranchId = branch.Id, PasswordHash = "x" };
        db.Users.AddRange(head, source, target);
        await db.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            var doc = new Document
            {
                BranchId = branch.Id,
                CreatedById = source.Id,
                IsDraft = false,
                BorrowerName = "أحمد",
                FileNumber = $"900{i}/2026",
                FileType = "تنفيذي",
                FileYear = "2026",
                Court = "دائرة تنفيذ حماة",
                AmountNumeric = 0,
                ExecStatus = string.Empty,
            };
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
            db.DocumentAppeals.Add(new DocumentAppeal
            {
                DocumentId = doc.Id,
                Status = AppealStatusCatalog.Pending,
                AssignedLawyerId = source.Id,
                CreatedById = source.Id,
            });
            await db.SaveChangesAsync();
        }

        return (branch.Id, source.Id, target.Id);
    }

    [Fact]
    public async Task TransferAll_DoubleTransfer_MovedTotalEqualsPending_LoserOnlyConcurrency()
    {
        var seed = await SeedAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = new DocGeneratorDbContext(Options(taskConnection));
                try
                {
                    var moved = await AppealService(db).TransferAllAsync(
                        new TransferAllAppealsRequest(seed.SourceId, seed.TargetId), seed.BranchId, null, "head");
                    return (Moved: (int?)moved, Error: (string?)null);
                }
                catch (Exception ex) { return (Moved: null, Error: ex.GetType().Name); }
            }),
            Task.Run(async () =>
            {
                var taskConnection = await OpenConnectionAsync();
                await using var db = new DocGeneratorDbContext(Options(taskConnection));
                try
                {
                    var moved = await AppealService(db).TransferAllAsync(
                        new TransferAllAppealsRequest(seed.SourceId, seed.TargetId), seed.BranchId, null, "head");
                    return (Moved: (int?)moved, Error: (string?)null);
                }
                catch (Exception ex) { return (Moved: null, Error: ex.GetType().Name); }
            }));

        // الخاسر — إن وُجد — تعارض تزامن ودي حصرًا (يترجمه المعالج العام 409)، بلا خام.
        Assert.All(outcomes, o => Assert.True(
            o.Error is null || o.Error == nameof(DbUpdateConcurrencyException),
            $"خسارة غير ودية: {o.Error}"));
        // المجموع المنقول يساوي المنظور (2): الفائز ينقل الكل، والآخر صفر أو يتراجع.
        Assert.Equal(2, outcomes.Where(o => o.Error is null).Sum(o => o.Moved!.Value));

        var checkConnection = await OpenConnectionAsync();
        await using var check = new DocGeneratorDbContext(Options(checkConnection));
        Assert.Equal(2, await check.DocumentAppeals.CountAsync(a => a.AssignedLawyerId == seed.TargetId));
    }

    [Fact]
    public async Task TransferAll_EmptySource_ReturnsZero()
    {
        var seed = await SeedAsync();
        var connection = _connections[^1];
        await using var db = new DocGeneratorDbContext(Options(connection));

        // المصدر بلا منظور مسند (الهدف نفسه لا يملك شيئًا) — صفر ناجح بلا استثناء.
        var moved = await AppealService(db).TransferAllAsync(
            new TransferAllAppealsRequest(seed.TargetId, seed.SourceId), seed.BranchId, null, "head");
        Assert.Equal(0, moved);
    }
}
