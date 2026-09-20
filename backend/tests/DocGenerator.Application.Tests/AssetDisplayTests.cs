using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// تثبيت تطبيع تسمية الأصول (E2): مرآة حرفية لـ assetDisplayName الأمامية —
/// التقليم قبل الفحص والإخراج معًا في كل الفروع، فاللقطة المخزنة والحارس
/// والمرآة الأمامية يتفقون حرفيًا حتى مع قيم مدخلة بمسافات محيطة.
/// </summary>
public class AssetDisplayTests
{
    [Fact]
    public void Label_RealEstate_TrimsSpacedPropertyNumber()
    {
        Assert.Equal("عقار رقم 77", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "77 ",
        }));
        Assert.Equal("عقار رقم 77", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "  77  ",
        }));
    }

    [Fact]
    public void Label_RealEstate_WhitespaceOnlyPropertyNumber_FallsBackToId()
    {
        Assert.Equal("عقار 5", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "   ",
        }));
    }

    [Fact]
    public void Label_RealEstate_PropertyPreferredOverNumber()
    {
        Assert.Equal("بيت المزة", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.RealEstate,
            Property = "  بيت المزة ",
            PropertyNumber = "77",
        }));
    }

    [Fact]
    public void Label_Vehicle_TrimsParts()
    {
        Assert.Equal("مركبة سيارة — لوحة 123", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.Vehicle,
            VehicleType = " سيارة ",
            PlateNumber = " 123 ",
        }));
    }

    [Fact]
    public void Label_Shop_TrimsRegister()
    {
        Assert.Equal("متجر سجل رقم 888", AssetDisplay.Label(new Asset
        {
            Id = 5,
            AssetKind = AssetKindCatalog.Shop,
            RegisterNumber = " 888 ",
        }));
    }
}
