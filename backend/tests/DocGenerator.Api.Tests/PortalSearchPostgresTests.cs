using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Tests;

/// <summary>
/// تحقق C-05 على PostgreSQL: باني بحث الأطراف الموحّد (`DocumentPartySearch`)
/// يجب أن يُترجم إلى SQL صالح على Npgsql، وأن يجد الكفيل رغم أن `SearchText` فارغ.
/// مُقيَّد بـ`DOCGEN_TEST_POSTGRES` كسائر اختبارات PostgreSQL.
/// </summary>
public sealed class PortalSearchPostgresTests
{
    [Fact]
    public async Task SearchScoped_FindsGuarantor_OnLivePostgres_WhenSearchTextIsEmpty()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_portal_search_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            int documentId;
            int entryId;
            await using (var seed = new DocGeneratorPostgresDbContext(options))
            {
                await seed.Database.MigrateAsync();
                (documentId, entryId) = await SeedAsync(seed);
            }

            await using var db = new DocGeneratorPostgresDbContext(options);
            var repository = new PortalRepository(db);

            var (total, items) = await repository.SearchScopedAsync(
                new[] { entryId }, "اسم كفيل مخفي", null, 1, 20);

            Assert.Equal(1, total);
            Assert.Contains(items, item => item.Id == documentId);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }

    private static async Task<(int DocumentId, int EntryId)> SeedAsync(
        DocGeneratorPostgresDbContext db)
    {
        var branch = new Branch { Name = "فرع بحث", Code = "PGSRCH" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var user = new User
        {
            Username = "pg_portal_search",
            FullName = "فاحص بحث البوابة",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var group = new PublicEntityGroup
        {
            CanonicalName = "جهة بحث البوابة",
            EntityType = PublicEntityTypeCatalog.Ministry,
            CreatedAt = DateTime.UtcNow,
        };
        var entry = new PublicEntity
        {
            Group = group,
            Governorate = "دمشق",
            BranchName = "الفرع الرئيسي",
            Status = EntityStatusCatalog.Final,
            IsActive = true,
            CreatedById = user.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.PublicEntityGroups.Add(group);
        db.PublicEntities.Add(entry);
        await db.SaveChangesAsync();

        var document = new Document
        {
            BranchId = branch.Id,
            CreatedById = user.Id,
            BorrowerName = "مدين البحث",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            ApplicantRegistryId = entry.Id,
            SearchText = string.Empty,
            IsDraft = false,
        };
        document.ApplicantPublicEntities.Add(new ApplicantPublicEntity
        {
            Name = "جهة بحث البوابة",
            Governorate = "دمشق",
            RegistryId = entry.Id,
        });
        document.Guarantors.Add(new Guarantor
        {
            GuarantorNumber = 1,
            GuarantorName = "اسم كفيل مخفي",
            GuarantorFather = "والد",
            GuarantorFamily = "عائلة",
        });
        db.Documents.Add(document);
        await db.SaveChangesAsync();

        return (document.Id, entry.Id);
    }
}
