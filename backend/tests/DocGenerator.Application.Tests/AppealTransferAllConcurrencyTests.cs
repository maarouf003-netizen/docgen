using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سلامة سباق النقل الجملي للاستئنافات (F5): القراءة داخل المعاملة + إعادة فحص
/// `Pending` + رمز `Version` — مهمتان متزامنتان على المصدر نفسه عبر سياقين
/// مستقلين على قاعدة <c>PostgreSQL</c> واحدة (‏`ADR-0009`).
/// ما يُثبَت هنا بدقة: سلامة الحالة النهائية (المجموع المنقول = المنظور، بلا كتابة
/// مفقودة صامتة) وصنف الخسارة حصرًا (تعارض تزامن، يترجمه المعالج العام `409`).
/// على `Postgres` (بخلاف ملف `SQLite`) الأقفال صفّية و`MVCC` حقيقي: الخاسر يرى
/// إمّا `AssignedLawyerId` محدَّثًا (صفر منظور) أو يفشل على شرط `Version`.
/// </summary>
public class AppealTransferAllConcurrencyTests : IAsyncLifetime
{
    private PostgresTestDb _pg = null!;

    public async Task InitializeAsync() => _pg = await PostgresTestDb.CreateAsync();

    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private async Task<(int BranchId, int SourceId, int TargetId)> SeedAsync()
    {
        await using var db = _pg.CreateContext();

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

    [PostgresFact]
    public async Task TransferAll_DoubleTransfer_MovedTotalEqualsPending_LoserOnlyConcurrency()
    {
        var seed = await SeedAsync();

        var outcomes = await Task.WhenAll(
            Task.Run(async () =>
            {
                await using var db = _pg.CreateContext();
                try
                {
                    var moved = await AppealWriteHandlerFactory.TransferAll(db).TransferAllAsync(
                        new TransferAllAppealsRequest(seed.SourceId, seed.TargetId), seed.BranchId, null, "head");
                    return (Moved: (int?)moved, Error: (string?)null);
                }
                catch (Exception ex) { return (Moved: null, Error: ex.GetType().Name); }
            }),
            Task.Run(async () =>
            {
                await using var db = _pg.CreateContext();
                try
                {
                    var moved = await AppealWriteHandlerFactory.TransferAll(db).TransferAllAsync(
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

        await using var check = _pg.CreateContext();
        Assert.Equal(2, await check.DocumentAppeals.CountAsync(a => a.AssignedLawyerId == seed.TargetId));
    }

    [PostgresFact]
    public async Task TransferAll_EmptySource_ReturnsZero()
    {
        var seed = await SeedAsync();
        await using var db = _pg.CreateContext();

        // المصدر بلا منظور مسند (الهدف نفسه لا يملك شيئًا) — صفر ناجح بلا استثناء.
        var moved = await AppealWriteHandlerFactory.TransferAll(db).TransferAllAsync(
            new TransferAllAppealsRequest(seed.TargetId, seed.SourceId), seed.BranchId, null, "head");
        Assert.Equal(0, moved);
    }
}
