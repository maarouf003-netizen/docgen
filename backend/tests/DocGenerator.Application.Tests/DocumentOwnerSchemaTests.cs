using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس دلالة "المالك مسؤول عن الملف": عمود <c>CreatedById</c> غير قابل للفراغ في
/// مخطط <c>PostgreSQL</c> الفعلي، والنموذج يعلن العلاقة مطلوبة — فلا يُنشأ ملف بلا مالك.
/// </summary>
public class DocumentOwnerSchemaTests : IAsyncLifetime
{
    private PostgresTestDb _db = null!;

    public async Task InitializeAsync() => _db = await PostgresTestDb.CreateAsync();

    public async Task DisposeAsync() => await _db.DisposeAsync();

    [PostgresFact]
    public async Task CreatedById_Column_IsNotNullableInSchema()
    {
        await using var db = _db.CreateContext();

        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = """
            SELECT is_nullable
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'Documents'
              AND column_name = 'CreatedById'
            """;

        await db.Database.OpenConnectionAsync();
        var value = (string?)await cmd.ExecuteScalarAsync();

        Assert.False(string.IsNullOrEmpty(value), "العمود CreatedById مفقود من جدول Documents");
        Assert.Equal("NO", value);
    }

    [PostgresFact]
    public void CreatedBy_Relationship_IsRequiredInModel()
    {
        using var db = new DocGeneratorPostgresDbContext(_db.Options);

        var property = db.Model.FindEntityType(typeof(Document))!.FindProperty("CreatedById");
        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }
}
