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
}