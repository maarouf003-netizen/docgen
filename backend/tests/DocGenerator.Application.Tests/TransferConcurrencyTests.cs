using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات حارس التفاؤلية في النقل المتزامن (TransferOwnerAsync):
/// يفشل التحديث الشرطي إذا تغيّر المحامي المختص بين القراءة والتحديث.
/// تُحاكى الجلسات المتزامنة بمُـDbContextـَين يشتركان في نفس اتصال SQLite.
/// </summary>
public class TransferConcurrencyTests
{
    private static DbContextOptions<DocGeneratorDbContext> Options(SqliteConnection connection)
        => new DbContextOptionsBuilder<DocGeneratorDbContext>().UseSqlite(connection).Options;

    [Fact]
    public async Task TransferOwner_WhenOwnerIsFresh_SucceedsAndMovesDocument()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        Seed(Options(connection));

        await using var db = new DocGeneratorDbContext(Options(connection));
        var repo = new DocumentRepository(db);

        var transferred = await repo.TransferOwnerAsync(1, expectedCreatedById: 1, targetId: 2, "سامر", "أحمد");

        Assert.NotNull(transferred);
        Assert.Equal(2, transferred!.CreatedById);
        Assert.Equal("سامر", transferred.Lawyer);
        Assert.Equal("سامر", transferred.CreatedBy!.FullName);
        Assert.Equal("أحمد", transferred.ReferredFromLawyer);
        Assert.NotNull(transferred.ReferredAt);
    }

    [Fact]
    public async Task TransferOwner_WhenOwnerChangedConcurrently_Fails()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        Seed(Options(connection));

        // أول نقل (جلسة أ): المحامي المختص الحالي 1 ما زال متوقعاً → ينجح.
        await using (var dbA = new DocGeneratorDbContext(Options(connection)))
        {
            var transferred = await new DocumentRepository(dbA)
                .TransferOwnerAsync(1, expectedCreatedById: 1, targetId: 2, "سامر", "أحمد");
            Assert.NotNull(transferred);
        }

        // نقل ثانٍ (جلسة ب) بنفس التوقع القديم 1: المحامي المختص أصبح 2 → يفشل (تعارض).
        await using (var dbB = new DocGeneratorDbContext(Options(connection)))
        {
            var transferred = await new DocumentRepository(dbB)
                .TransferOwnerAsync(1, expectedCreatedById: 1, targetId: 3, "خالد", "سامر");
            Assert.Null(transferred);
        }

        // التحقق من أن القيمة الفعلية في القاعدة بقيت من النقل الناجح الأول.
        await using (var verify = new DocGeneratorDbContext(Options(connection)))
        {
            var doc = await verify.Documents.SingleAsync();
            Assert.Equal(2, doc.CreatedById);
            Assert.Equal("سامر", doc.Lawyer);
        }
    }

    private static void Seed(DbContextOptions<DocGeneratorDbContext> options)
    {
        using var db = new DocGeneratorDbContext(options);
        db.Database.EnsureCreated();
        db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        db.SaveChanges();
        db.Users.AddRange(
            new User { Username = "lawyer1", FullName = "أحمد", Role = UserRole.Lawyer, BranchId = 1, PasswordHash = "x" },
            new User { Username = "lawyer2", FullName = "سامر", Role = UserRole.Lawyer, BranchId = 1, PasswordHash = "x" },
            new User { Username = "lawyer3", FullName = "خالد", Role = UserRole.Lawyer, BranchId = 1, PasswordHash = "x" });
        db.SaveChanges();
        db.Documents.Add(new Document { CreatedById = 1, BranchId = 1, Lawyer = "أحمد", IsDraft = true });
        db.SaveChanges();
    }
}
