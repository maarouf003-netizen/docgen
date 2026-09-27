using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سياسة `unknown` الموحدة لدوال العرض: المعروف يُعرَض عربيًا، والمجهول يُعرض خامه
/// (لا تسمية صالحة مخترعة)، والغائب يتبع قاعدة الغياب المصممة لكل كتالوج.
/// آلة الحالات (`Classify`/`AllowedStatusChanges`) خارج النطاق عمدًا — مثبتة بعقودها.
/// </summary>
public class CatalogUnknownLabelTests
{
    [Theory]
    [InlineData("in-favor", "للصالح")]
    [InlineData("against", "للضد")]
    [InlineData("نتيجة غريبة", "نتيجة غريبة")]
    [InlineData("", "")]
    public void AppealOutcome_ToLabel_ReturnsRawForUnknown(string outcome, string expected)
    {
        Assert.Equal(expected, AppealOutcomeCatalog.ToLabel(outcome));
    }

    [Theory]
    [InlineData("appellants", "مستأنِفين")]
    [InlineData("against-us", "مستأنف علينا")]
    [InlineData("اتجاه غريب", "اتجاه غريب")]
    public void AppealDirection_ToLabel_ReturnsRawForUnknown(string direction, string expected)
    {
        Assert.Equal(expected, AppealDirectionCatalog.ToLabel(direction));
    }

    [Theory]
    [InlineData("pending", "منظور")]
    [InlineData("decided", "محسوم")]
    [InlineData("struck-off", "مشطوب")]
    [InlineData("حالة غريبة", "حالة غريبة")]
    public void AppealStatus_ToLabel_ReturnsRawForUnknown(string status, string expected)
    {
        Assert.Equal(expected, AppealStatusCatalog.ToLabel(status));
    }

    [Theory]
    [InlineData("", "متداول")]
    [InlineData("منفذ", "منفذ")]
    [InlineData("مشطوب", "مشطوب")]
    [InlineData("حالة غريبة", "حالة غريبة")]
    public void ExecutedStatus_ToLabel_EmptyIsTradingButUnknownIsRaw(string status, string expected)
    {
        Assert.Equal(expected, ExecutedStatusCatalog.ToLabel(status));
    }

    [Theory]
    [InlineData("applicant", "الجهة العامة طالبة التنفيذ")]
    [InlineData("executed", "الجهة العامة منفذ عليها")]
    [InlineData("deposit", "عرض وايداع")]
    [InlineData("صفة غريبة", "صفة غريبة")]
    public void GeneralEntitySide_ToLabel_ReturnsRawForUnknown(string side, string expected)
    {
        Assert.Equal(expected, GeneralEntitySideCatalog.ToLabel(side));
    }

    [Theory]
    [InlineData("struck-off", "شطب")]
    [InlineData("renewal", "تجديد")]
    [InlineData("نوع غريب", "نوع غريب")]
    public void OccurrenceType_ToLabel_ReturnsRawForUnknown(string type, string expected)
    {
        Assert.Equal(expected, OccurrenceTypeCatalog.ToLabel(type));
    }

    [Theory]
    [InlineData("natural", "شخص طبيعي")]
    [InlineData("legal", "شخص اعتباري")]
    [InlineData("public", "جهة عامة")]
    [InlineData(null, "شخص طبيعي")]
    [InlineData("طبيعة غريبة", "طبيعة غريبة")]
    public void PartyNature_ToLabel_NullIsDefaultButUnknownIsRaw(string? nature, string expected)
    {
        Assert.Equal(expected, PartyNatureCatalog.ToLabel(nature));
    }

    [Theory]
    [InlineData("عقار", "عقار")]
    [InlineData("مركبة", "مركبة")]
    [InlineData(null, "عقار")]
    [InlineData("نوع غريب", "نوع غريب")]
    public void AssetKind_ToLabel_NullIsDefaultButUnknownIsRaw(string? kind, string expected)
    {
        Assert.Equal(expected, AssetKindCatalog.ToLabel(kind));
    }

    [Theory]
    [InlineData("عقار", "تمام العقار")]
    [InlineData("مركبة", "تمام المركبة")]
    [InlineData("متجر", "تمام المتجر")]
    public void AssetKind_FullShareLabel_MapsShareableKinds(string? kind, string expected)
    {
        Assert.Equal(expected, AssetKindCatalog.FullShareLabel(kind));
    }

    [Theory]
    [InlineData("متداول", "متداول")]
    [InlineData("تريث", "تريث")]
    [InlineData("حالة غريبة", "حالة غريبة")]
    public void ExecutionStatus_ToStateLabel_ReturnsRawForUnknown(string state, string expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.ToStateLabel(state));
    }

    [Theory]
    [InlineData("منفذ جبريا", "منفذ جبريا")]
    [InlineData("حالة غريبة", "حالة غريبة")]
    public void ExecutionStatus_ToStatusLabel_ReturnsRawForUnknown(string status, string expected)
    {
        Assert.Equal(expected, ExecutionStatusCatalog.ToStatusLabel(status));
    }
}
