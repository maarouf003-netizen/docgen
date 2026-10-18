using System.Reflection;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class OccurrenceTypeCatalogTests
{
    [Theory]
    [InlineData("referred-to-start", "محال الى البداية")]
    [InlineData("revert", "تراجع / إلغاء")]
    public void ToLabel_MapsKnownTypes(string type, string expected)
    {
        Assert.Equal(expected, OccurrenceTypeCatalog.ToLabel(type));
    }

    [Fact]
    public void ValidTypes_ExactlyMirrorCatalogConstants()
    {
        // لا شيء كان يضمن أن ثابتًا جديدًا في الكتالوج دخل ValidTypes، فيفلت
        // من حارس حد العمود (S3) — هذا الاختبار يفرض المطابقة في الاتجاهين.
        var constants = GetCatalogConstants();

        Assert.NotEmpty(constants);

        var missing = constants
            .Where(t => !OccurrenceTypeCatalog.ValidTypes.Contains(t))
            .ToList();
        Assert.True(missing.Count == 0,
            $"ثوابت في الكتالوج غير مدرجة في ValidTypes (تفلت من حارس حد العمود): {string.Join(", ", missing)}");

        var stray = OccurrenceTypeCatalog.ValidTypes
            .Where(t => !constants.Contains(t))
            .ToList();
        Assert.True(stray.Count == 0,
            $"أنواع في ValidTypes بلا ثابت مقابل في الكتالوج (خطأ مطبعي محتمل): {string.Join(", ", stray)}");
    }

    [Fact]
    public void EveryCatalogConstant_HasMappedLabel()
    {
        // القاعدة الملزمة: «نوع جديد ⇐ الثابت + ValidTypes + ToLabel». اختبار التكافؤ
        // أعلاه يفرض أول اثنين، وهذا يفرض الثالث: أي ثابت بلا ذراع في ToLabel يعود
        // خامًا بالإنجليزية في الواجهة.
        var unlabeled = GetCatalogConstants()
            .Where(t => OccurrenceTypeCatalog.ToLabel(t) == t)
            .ToList();
        Assert.True(unlabeled.Count == 0,
            $"ثوابت بلا تسمية عربية في ToLabel (تظهر خامة في الواجهة): {string.Join(", ", unlabeled)}");
    }

    [Fact]
    public void ValidTypes_FitWithinColumnLimit()
    {
        // S3: عمود OccurrenceType بعرض 30 — أي رمز أطول ينفجر على Postgres وحده
        // (SQLite يتجاهل حد الطول)، فيحرس هذا الاختبار الرموز المستقبلية.
        Assert.All(OccurrenceTypeCatalog.ValidTypes, t => Assert.True(t.Length <= 30, t));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("referred-to-start", true)]
    [InlineData("deferred", true)]
    [InlineData("settled", true)]
    [InlineData("forcible", true)]
    [InlineData("revert", true)]
    [InlineData("renewal", false)]
    [InlineData("struck-off", false)]
    [InlineData("recovered", false)]
    [InlineData("entity-change", false)]
    public void IsStatusChange_PinsMatrix(string? type, bool expected)
    {
        Assert.Equal(expected, OccurrenceTypeCatalog.IsStatusChange(type));
    }

    private static List<string> GetCatalogConstants() =>
        typeof(OccurrenceTypeCatalog)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}