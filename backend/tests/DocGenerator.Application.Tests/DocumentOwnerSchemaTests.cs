using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// التحقق من تقوية "المحامي المختص الإلزامي": عمود CreatedById يجب أن يكون
/// NOT NULL في مخطط القاعدة، والعلاقة في النموذج إلزامية — فلا وجود للملفات اليتيمة.
/// </summary>
public class DocumentOwnerSchemaTests
{
    [Fact]
    public async Task CreatedById_Column_IsNotNullableInSchema()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DocGeneratorDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new DocGeneratorDbContext(options);
        db.Database.EnsureCreated();

        var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "PRAGMA table_info('Documents')";
        await using var reader = await cmd.ExecuteReaderAsync();

        var found = false;
        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) == "CreatedById")
            {
                found = true;
                Assert.Equal(1L, reader.GetInt64(3));
            }
        }

        Assert.True(found, "العمود CreatedById غير موجود في جدول Documents");
    }

    [Fact]
    public void CreatedBy_Relationship_IsRequiredInModel()
    {
        using var db = new DocGeneratorDbContext(
            new DbContextOptionsBuilder<DocGeneratorDbContext>().UseSqlite("DataSource=:memory:").Options);

        var property = db.Model.FindEntityType(typeof(Document))!.FindProperty("CreatedById");
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }
}
