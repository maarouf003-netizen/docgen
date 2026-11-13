using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// انحدار C-04: العدّ داخل القفل يجب أن يقرأ الحالة الفعلية من القاعدة، لا مجموعة
/// متتبَّعة قديمة. سيناريو: الطلب حمّل الملف للفحص (50 متدخلًا)، ثم حُذف متدخل في
/// طلب آخر قبل الإضافة — العدّ الطازج 49 فيجب أن تنجح الإضافة (تصبح 50)، لا أن تُرفض.
/// </summary>
public sealed class DocumentInterventionLimitStaleTrackingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DocGeneratorDbContext> _options;

    public DocumentInterventionLimitStaleTrackingTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = TestDb.CreateOptions(_connection);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Add_AfterConcurrentDelete_ReloadsFreshCount()
    {
        int documentId;
        int userId;
        await using (var seed = new DocGeneratorDbContext(_options))
        {
            await seed.Database.EnsureCreatedAsync();
            var branch = new Branch { Name = "فرع", Code = "STL" };
            seed.Branches.Add(branch);
            await seed.SaveChangesAsync();
            var user = new User
            {
                Username = "stale_user",
                FullName = "فاحص",
                Role = UserRole.Lawyer,
                BranchId = branch.Id,
                PasswordHash = "x",
            };
            seed.Users.Add(user);
            await seed.SaveChangesAsync();
            var doc = new Document
            {
                BranchId = branch.Id,
                CreatedById = user.Id,
                BorrowerName = "مدين",
                GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            };
            seed.Documents.Add(doc);
            await seed.SaveChangesAsync();
            for (var i = 0; i < InterventionLimitsCatalog.MaxPerDocument; i++)
            {
                seed.DocumentInterventions.Add(new DocumentIntervention
                {
                    DocumentId = doc.Id,
                    Nature = PartyNatureCatalog.Natural,
                    Name = $"متدخل {i}",
                    CreatedById = user.Id,
                });
            }
            await seed.SaveChangesAsync();
            documentId = doc.Id;
            userId = user.Id;
        }

        // طلب الإضافة حمّل الملف أولًا (فحص الوصول) — 50 متدخلًا في المتتبَّع.
        await using var stale = new DocGeneratorDbContext(_options);
        var staleRepo = new DocumentRepository(stale);
        var loaded = await staleRepo.GetByIdAsync(documentId);
        Assert.Equal(InterventionLimitsCatalog.MaxPerDocument, loaded!.Interventions.Count);

        // طلب آخر حذف متدخلًا وثبّت الحذف — القاعدة الآن 49 والمتتبَّع القديم ما زال 50.
        await using (var deleter = new DocGeneratorDbContext(_options))
        {
            var row = await deleter.DocumentInterventions
                .FirstAsync(i => i.DocumentId == documentId);
            deleter.DocumentInterventions.Remove(row);
            await deleter.SaveChangesAsync();
        }

        var service = new DocumentInterventionService(
            staleRepo,
            new Repository<PublicEntity>(stale),
            new Repository<PublicEntityGroup>(stale),
            new UnitOfWork(stale),
            new TransactionRunner(stale),
            new FakeAuditLogger());

        var created = await service.AddAsync(documentId, NaturalRequest(), userId, "فاحص");

        Assert.NotNull(created);
        await using var verify = new DocGeneratorDbContext(_options);
        Assert.Equal(
            InterventionLimitsCatalog.MaxPerDocument,
            await verify.DocumentInterventions.CountAsync(i => i.DocumentId == documentId));
    }

    private static DocumentInterventionUpsertRequest NaturalRequest() => new()
    {
        Nature = PartyNatureCatalog.Natural,
        ReceiptNumber = "123",
        ReceiptDate = "1/8/2026",
        Subject = "طلب تدخل",
        Name = "متدخل جديد",
        Father = "والد",
        Family = "عائلة",
        NationalId = "01020304",
        AddressType = "عنوان إقامة",
        Address = "دمشق",
    };
}
