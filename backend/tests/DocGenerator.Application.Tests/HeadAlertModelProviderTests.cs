using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// نموذج تنبيهات رئيس القسم بعد P0-02: صياغة واحدة لـ`PostgreSQL` في كل المزوّدين
/// (ADR-0001). الفهرس الفريد **الجزئي** على <c>DedupeKey</c> يستخدم
/// <c>"IsArchived" = false</c>، ويُثبَّت على **النموذج/DDL** بلا قاعدة حيّة.
///
/// ملاحظة مقصودة: قيد <c>CK_HeadAlerts_Kind_NotEmpty</c> نصّه واحد على المزوّدين
/// (<c>TRIM("Kind") &lt;&gt; ''</c>) — اختلافه الوحيد في تهريب المعرّفات الذي يتولاه
/// المزوّد؛ لذلك يُتحقق من ظهوره في `DDL` المزوّدين لا من اختلافه.
/// </summary>
public class HeadAlertModelProviderTests
{
    private const string PartialIndexName = "UX_HeadAlerts_DedupeKey_Active";
    private const string UnifiedFilter = "\"DedupeKey\" IS NOT NULL AND \"IsArchived\" = false";

    [Fact]
    public void PartialUniqueDedupeIndex_UsesUnifiedPostgresSyntax()
    {
        using var sqlite = TestDb.Create();
        using var postgres = NewPostgresContext();

        Assert.Equal(UnifiedFilter, PartialIndexFilter(sqlite));
        Assert.Equal(UnifiedFilter, PartialIndexFilter(postgres));
    }

    [Fact]
    public void GeneratedDdl_CarriesTheUnifiedFilter_AndTheSameKindConstraint()
    {
        using var sqlite = TestDb.Create();
        using var postgres = NewPostgresContext();

        var sqliteDdl = sqlite.Database.GenerateCreateScript();
        var postgresDdl = postgres.Database.GenerateCreateScript();

        Assert.Contains(PartialIndexName, sqliteDdl, StringComparison.Ordinal);
        Assert.Contains(PartialIndexName, postgresDdl, StringComparison.Ordinal);
        Assert.Contains("\"IsArchived\" = false", sqliteDdl, StringComparison.Ordinal);
        Assert.Contains("\"IsArchived\" = false", postgresDdl, StringComparison.Ordinal);
        Assert.DoesNotContain("\"IsArchived\" = 0", sqliteDdl, StringComparison.Ordinal);

        // قيد النوع غير الفارغ موجود على المزوّدين (نصّه موحّد).
        Assert.Contains("CK_HeadAlerts_Kind_NotEmpty", sqliteDdl, StringComparison.Ordinal);
        Assert.Contains("CK_HeadAlerts_Kind_NotEmpty", postgresDdl, StringComparison.Ordinal);
    }

    private static DocGeneratorPostgresDbContext NewPostgresContext() =>
        new(new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=unused;Username=unused;Password=unused")
            .Options);

    private static string? PartialIndexFilter(DbContext context) =>
        context.Model
            .FindEntityType(typeof(HeadAlert))!
            .GetIndexes()
            .Single(index => index.GetDatabaseName() == PartialIndexName)
            .GetFilter();
}
