using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// نموذج تنبيهات رئيس القسم بعد P0-02: صياغة واحدة لـ`PostgreSQL` (ADR-0001).
/// الفهرس الفريد **الجزئي** على <c>DedupeKey</c> يستخدم <c>"IsArchived" = false</c>،
/// ويُثبَّت على **النموذج/DDL** بلا قاعدة حيّة.
///
/// <para>كان الاختبار يقارن `SQLite` بـ`PostgreSQL`؛ بعد `P1-08` (‏`ADR-0009`) صار
/// المزوّد واحدًا، فالتحقق من DDL الإنتاجي نفسه لا من تطابق مزوّدين.</para>
/// </summary>
public class HeadAlertModelProviderTests : PostgresDbTest
{
    private const string PartialIndexName = "UX_HeadAlerts_DedupeKey_Active";
    private const string UnifiedFilter = "\"DedupeKey\" IS NOT NULL AND \"IsArchived\" = false";

    [PostgresFact]
    public void PartialUniqueDedupeIndex_UsesThePostgresFilter()
    {
        using var db = NewContext();
        Assert.Equal(UnifiedFilter, PartialIndexFilter(db));
    }

    [PostgresFact]
    public void GeneratedDdl_CarriesTheUnifiedFilter_AndTheKindConstraint()
    {
        using var db = NewContext();
        var ddl = db.Database.GenerateCreateScript();

        Assert.Contains(PartialIndexName, ddl, StringComparison.Ordinal);
        // مقارنة غير حساسة لحالة الأحرف: صياغة `false`/`FALSE` تفصيل مزوّد، والدلالة واحدة.
        Assert.Contains("\"IsArchived\" = false", ddl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"IsArchived\" = 0", ddl, StringComparison.Ordinal);
        Assert.Contains("CK_HeadAlerts_Kind_NotEmpty", ddl, StringComparison.Ordinal);
    }

    private static string? PartialIndexFilter(DbContext context) =>
        context.Model
            .FindEntityType(typeof(HeadAlert))!
            .GetIndexes()
            .Single(index => index.GetDatabaseName() == PartialIndexName)
            .GetFilter();
}
