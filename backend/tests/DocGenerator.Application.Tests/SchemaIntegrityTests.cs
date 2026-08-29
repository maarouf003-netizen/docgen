using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس سلامة المخطط: يطبّق هجرات SQLite الفعلية (Database.Migrate) على قاعدة فورية ثم
/// يقارن أعمدةَ كل جدولٍ بالحقول المعيّنة في النموذج؛ فأي حقل كيان يُضاف في الكود دون عمود
/// في قاعدة البيانات يفشل هذا الاختبار فورًا.
/// </summary>
public class SchemaIntegrityTests
{
    private static DocGeneratorDbContext CreateMigratedDb()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(connection)
            .Options;
        var db = new DocGeneratorDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static Dictionary<string, HashSet<string>> ReadTableColumns(DocGeneratorDbContext db)
    {
        var schema = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = db.Database.GetDbConnection().CreateCommand())
        {
            cmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' "
                + "AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                schema[reader.GetString(0)] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        foreach (var table in schema.Keys.ToArray())
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table.Replace("\"", "\"\"", StringComparison.Ordinal)}\")";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                schema[table].Add(reader.GetString(1));
        }
        return schema;
    }

    [Fact]
    public void EveryMappedEntityProperty_HasColumnInMigratedSchema()
    {
        var db = CreateMigratedDb();
        try
        {
            var schema = ReadTableColumns(db);
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
                        missing.Add($"العمود {table}.{column} (الحقل {entity.ClrType.Name}.{prop.Name})");
                }
            }

            Assert.True(missing.Count == 0,
                "حقول معيّنة بلا أعمدة في مخطط قاعدة البيانات الفعلي:\n" + string.Join("\n", missing));
        }
        finally
        {
            db.Dispose();
        }
    }

    /// <summary>
    /// ارتدادية العلة: كانت هجرة <c>AddEntityRegistryReview</c> تنشئ عدة <c>IX_PublicEntities_ReviewedById</c>
    /// فريدة (unique: true) بالخطأ، فلا يستطيع رئيس القسم اعتماد/تعديل قيدٍ ثانٍ لأنه يُسند ReviewedById
    /// لمستخدمه نفسه في كل مرة فيرفضه القيد بخطأ الحفظ. يجب أن يكون الفهرس غير فريد — المراجع الواحد
    /// يراجع أكثر من قيد.
    /// </summary>
    [Fact]
    public void ReviewedByIdIndex_IsNotUniqueInMigratedSchema()
    {
        var db = CreateMigratedDb();
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText =
                "SELECT sql FROM sqlite_master WHERE type='index' AND name='IX_PublicEntities_ReviewedById'";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read(), "فهرس IX_PublicEntities_ReviewedById مفقود من المخطط المهاجر");
            var sql = reader.GetString(0);

            Assert.DoesNotContain("UNIQUE", sql, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            db.Dispose();
        }
    }

    /// <summary>
    /// ارتدادية وظيفية على مستوى القاعدة: قيدان مُراجعان من المستخدم نفسه يجب أن يُحفظا معًا
    /// دون انفجار قيد فريد — تمامًا كاعتماد رئيس القسم قيدين متلاحقين.
    /// </summary>
    [Fact]
    public void TwoRows_CanShareSameReviewedById_InMigratedSchema()
    {
        var db = CreateMigratedDb();
        try
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Users (Id, Username, FullName, PasswordHash, Role, IsActive, TokenVersion, FailedLoginCount, CreatedAt, UpdatedAt)
                VALUES (9001, 'reviewer9001', 'مُراجع', 'x', 4, 1, 1, 0, '2026-01-01 00:00:00', '2026-01-01 00:00:00');
                INSERT INTO PublicEntityGroups (Id, CanonicalName, EntityType, IsActive, CreatedAt)
                VALUES (9001, 'جهة اختبار', 'ministry', 1, '2026-01-01 00:00:00');
                INSERT INTO PublicEntities (Id, GroupId, Governorate, BranchName, CitationFormula, Status, CreatedById, CreatedAt, NeedsReview, ReviewedById, IsActive, IsParentEntity)
                VALUES (9001, 9001, 'دمشق', 'الفرع أ', 'add-to-job', 'final', 9001, '2026-01-01 00:00:00', 0, 9001, 1, 0),
                       (9002, 9001, 'دمشق', 'الفرع ب', 'add-to-job', 'final', 9001, '2026-01-01 00:00:00', 0, 9001, 1, 0);
            ";
            var inserted = cmd.ExecuteNonQuery();

            Assert.Equal(4, inserted);
        }
        finally
        {
            db.Dispose();
        }
    }

    /// <summary>
    /// حارس النموذج: أي عودة لفريدية الفهرس في <c>PublicEntityConfiguration</c> تُفشل هذا الاختبار
    /// حتى قبل توليد هجرة.
    /// </summary>
    [Fact]
    public void ReviewedByIdIndexInModel_IsNotUnique()
    {
        var db = CreateMigratedDb();
        try
        {
            var entity = db.Model.FindEntityType(typeof(PublicEntity))
                ?? throw new InvalidOperationException("كيان PublicEntity غير معيّن في النموذج");
            var index = entity.GetIndexes().Single(i =>
                i.Properties.Count == 1 && i.Properties[0].Name == nameof(PublicEntity.ReviewedById));

            Assert.False(index.IsUnique);
        }
        finally
        {
            db.Dispose();
        }
    }
}