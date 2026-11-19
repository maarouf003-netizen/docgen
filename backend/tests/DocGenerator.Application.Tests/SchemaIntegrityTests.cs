using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس سلامة المخطط على <c>PostgreSQL</c> الفعلي (‏`ADR-0009`): يبني القالب
/// من النموذج (<c>EnsureCreated</c>) ثم يقارن كل خاصية معيّنة بعمودها في
/// <c>information_schema</c>، ويتحقق من فهرس <c>ReviewedById</c> عبر <c>pg_indexes</c>،
/// ويثبت عمليًا أن صفّين يمكن أن يتشاركا المراجع نفسه (فهرس غير فريد).
/// </summary>
public class SchemaIntegrityTests : IAsyncLifetime
{
    private PostgresTestDb _db = null!;

    public async Task InitializeAsync() => _db = await PostgresTestDb.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static async Task<Dictionary<string, HashSet<string>>> ReadTableColumnsAsync(
        DocGeneratorDbContext db)
    {
        var schema = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT table_name, column_name
            FROM information_schema.columns
            WHERE table_schema = 'public'
            """;

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (!schema.TryGetValue(table, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                schema[table] = columns;
            }

            columns.Add(reader.GetString(1));
        }

        return schema;
    }

    [PostgresFact]
    public async Task EveryMappedEntityProperty_HasColumnInPhysicalSchema()
    {
        await using var db = _db.CreateContext();
        var schema = await ReadTableColumnsAsync(db);
        var missing = new List<string>();

        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null)
                continue;

            if (!schema.ContainsKey(table))
            {
                missing.Add($"جدول مفقود: {table}");
                continue;
            }

            foreach (var prop in entity.GetProperties())
            {
                var column = prop.GetColumnName();
                if (column is null)
                    continue;
                if (!schema[table].Contains(column))
                    missing.Add($"عمود مفقود {table}.{column} (خاصية {entity.ClrType.Name}.{prop.Name})");
            }
        }

        Assert.True(missing.Count == 0,
            "خصائص النموذج بلا أعمدة في المخطط الفعلي:\n" + string.Join("\n", missing));
    }

    /// <summary>
    /// حارس الفهرس: <c>AddEntityRegistryReview</c> أنشأ <c>IX_PublicEntities_ReviewedById</c>
    /// (‏unique: true) ثم أُلغي لأن مراجعة/اعتماد رئيس قسم محافظة واحدة قد يشمل قيدين
    /// متلاحقين للمراجع نفسه. أي عودة للفريدية تفشل هنا على القاعدة الحقيقية.
    /// </summary>
    [PostgresFact]
    public async Task ReviewedByIdIndex_IsNotUniqueInPhysicalSchema()
    {
        await using var db = _db.CreateContext();
        await db.Database.OpenConnectionAsync();

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND indexname = 'IX_PublicEntities_ReviewedById'
            """;

        var definition = (string?)await cmd.ExecuteScalarAsync();
        Assert.False(string.IsNullOrEmpty(definition),
            "فهرس IX_PublicEntities_ReviewedById مفقود من المخطط الفعلي");

        Assert.DoesNotContain("UNIQUE", definition, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ارتدادية وظيفية على مستوى القاعدة: قيدان مُراجعان من المستخدم نفسه يجب أن يُحفظا معًا
    /// دون انفجار قيد فريد — تمامًا كاعتماد رئيس القسم قيدين متلاحقين.
    /// </summary>
    [PostgresFact]
    public async Task TwoRows_CanShareSameReviewedById_InPhysicalSchema()
    {
        await using var db = _db.CreateContext();

        var reviewer = new User
        {
            Username = "reviewer_pg",
            FullName = "مُراجع",
            Role = UserRole.Admin,
            PasswordHash = "x",
        };
        db.Users.Add(reviewer);

        var group = new PublicEntityGroup
        {
            CanonicalName = "جهة اختبار المراجعة",
            EntityType = PublicEntityTypeCatalog.Ministry,
        };
        group.Entries.Add(new PublicEntity
        {
            Governorate = "دمشق",
            BranchName = "الفرع أ",
            Status = EntityStatusCatalog.Final,
            CreatedBy = reviewer,
            ReviewedBy = reviewer,
        });
        group.Entries.Add(new PublicEntity
        {
            Governorate = "دمشق",
            BranchName = "الفرع ب",
            Status = EntityStatusCatalog.Final,
            CreatedBy = reviewer,
            ReviewedBy = reviewer,
        });
        db.PublicEntityGroups.Add(group);

        await db.SaveChangesAsync();

        Assert.Equal(2, await db.PublicEntities.CountAsync(e => e.ReviewedById == reviewer.Id));
    }

    /// <summary>
    /// حارس النموذج: أي عودة لفريدية الفهرس في <c>PublicEntityConfiguration</c> تُفشل هذا الاختبار
    /// حتى قبل توليد هجرة.
    /// </summary>
    [PostgresFact]
    public void ReviewedByIdIndexInModel_IsNotUnique()
    {
        using var db = _db.CreateContext();

        var entity = db.Model.FindEntityType(typeof(PublicEntity))
            ?? throw new InvalidOperationException("كيان PublicEntity غير معيّن في النموذج");
        var index = entity.GetIndexes().Single(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(PublicEntity.ReviewedById));

        Assert.False(index.IsUnique);
    }
}
