using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Tests;

/// <summary>
/// قواعد الممثل الشرعي للورثة: التطبيع المشترك وحارس «لا يجتمع الممثل مع الورثة».
/// </summary>
public class HeirRepresentativeTests
{
    [Fact]
    public void NormalizeHeirs_FillsRepresentativeAndNormalizesFields()
    {
        var heirs = DocumentText.NormalizeHeirs(new[]
        {
            new HeirDto(null, "حسن", "محمود", "الحلبي", "أصالة", "وكيل", "المحامي سامر",
                "سمير", "حسن", "علي", "ولي", "وكيل قانوني", "المحامي سامر"),
        }, null);

        var heir = Assert.Single(heirs);
        Assert.Equal("سمير", heir.RepresentativeName);
        Assert.Equal("حسن", heir.RepresentativeFather);
        Assert.Equal("علي", heir.RepresentativeFamily);
        Assert.Equal("ولي", heir.RepresentativeCapacity);
        Assert.Equal("وكيل قانوني", heir.RepresentativeAddressType);
        Assert.Equal("المحامي سامر", heir.RepresentativeAddress);
        // عنوان الوريث نفسه يُصفَّر عند وجود ممثل (عنوان الممثل هو المعتبر).
        Assert.Null(heir.AddressType);
        Assert.Null(heir.HeirAddress);
    }

    [Fact]
    public void NormalizeHeirs_EmptyTriple_ClearsAllRepresentativeFields()
    {
        var heirs = DocumentText.NormalizeHeirs(new[]
        {
            new HeirDto(null, "حسن", null, null, "أصالة", "عنوان", "عنوان",
                null, null, null, "ولي", "وكيل قانوني", "عنوان-ممثل"),
        }, null);

        var heir = Assert.Single(heirs);
        Assert.Null(heir.RepresentativeName);
        Assert.Null(heir.RepresentativeFather);
        Assert.Null(heir.RepresentativeFamily);
        Assert.Null(heir.RepresentativeCapacity);
        Assert.Null(heir.RepresentativeAddressType);
        Assert.Null(heir.RepresentativeAddress);
    }

    [Fact]
    public void NormalizeHeirs_InvalidCapacityAndAddressType_UseDefaults()
    {
        var heirs = DocumentText.NormalizeHeirs(new[]
        {
            new HeirDto(null, "حسن", null, null, "أصالة", "عنوان", "عنوان",
                "سمير", "حسن", "علي", "غير صالح", "غير صالح", "عنوان-ممثل"),
        }, null);

        var heir = Assert.Single(heirs);
        Assert.Equal(string.Empty, heir.RepresentativeCapacity);
        Assert.Equal("عنوان", heir.RepresentativeAddressType);
    }

    [Fact]
    public void NormalizeExecutedHeirs_FillsRepresentativeAndSkipsNamelessHeir()
    {
        var heirs = DocumentText.NormalizeExecutedHeirs(new[]
        {
            new ExecutedHeirDto(null, "", null, null, "عنوان", "", "ممثل", "أب", "عائلة", "ولي", "عنوان", "عنوان"),
            new ExecutedHeirDto(null, "وريث", "أب", "عائلة", "وكيل", "المحامي سامر",
                "سمير", "حسن", "علي", "وصي", "موطن مختار", "حلب"),
        });

        var heir = Assert.Single(heirs);
        Assert.Equal("وريث", heir.HeirName);
        Assert.Equal("سمير", heir.RepresentativeName);
        Assert.Equal("وصي", heir.RepresentativeCapacity);
        Assert.Equal("موطن مختار", heir.RepresentativeAddressType);
        Assert.Equal("حلب", heir.RepresentativeAddress);
        Assert.Null(heir.AddressType);
        Assert.Null(heir.HeirAddress);
    }

    [Fact]
    public void ValidateExclusivity_RejectsBorrowerHeirsWithRepresentative()
    {
        var request = new DocumentUpsertRequest
        {
            BorrowerNature = "natural",
            BorrowerHeirs = new List<HeirDto>
            {
                new(null, "وريث", "أب", "عائلة", "أصالة", "عنوان", "عنوان"),
            },
            BorrowerRepresentativeName = "ممثل",
            BorrowerRepresentativeFather = "أب",
            BorrowerRepresentativeFamily = "عائلة",
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            DocumentValidator.ValidateRepresentativeHeirExclusivity(request));
        Assert.Contains("المقترض", ex.Message);
        Assert.Contains("لا يجتمع الممثل الشرعي مع الورثة", ex.Message);
    }

    [Fact]
    public void ValidateExclusivity_RejectsGuarantorHeirsWithRepresentative()
    {
        var request = new DocumentUpsertRequest
        {
            Guarantors = new List<GuarantorDto>
            {
                new(
                    Id: null,
                    GuarantorNumber: 2,
                    Name: "كفيل",
                    Father: "أب",
                    Family: "عائلة",
                    Mother: null,
                    Birth: null,
                    Register: null,
                    NationalId: null,
                    Address: "عنوان",
                    AddressType: "موطن مختار",
                    RepresentativeName: "ممثل",
                    RepresentativeFather: "أب الممثل",
                    RepresentativeFamily: "عائلة الممثل",
                    RepresentativeCapacity: "ولي",
                    RepresentativeAddressType: "عنوان",
                    RepresentativeAddress: "عنوان الممثل",
                    Heirs: new List<HeirDto>
                    {
                        new(null, "وريث", "أب", "عائلة", "أصالة", "عنوان", "عنوان"),
                    }),
            },
        };

        var ex = Assert.Throws<ArgumentException>(() =>
            DocumentValidator.ValidateRepresentativeHeirExclusivity(request));
        Assert.Contains("الكفيل 2", ex.Message);
    }

    [Theory]
    [InlineData("executed")]
    [InlineData("deposit")]
    public void ValidateExclusivity_AllowsExecutionApplicantEstateWithRepresentative_OnExecutedSide(string side)
    {
        var request = new DocumentUpsertRequest
        {
            GeneralEntitySide = side,
            ExecutionApplicants = new List<ExecutionApplicantDto>
            {
                new(
                    Id: null,
                    Name: "طالب",
                    Father: null,
                    Family: null,
                    LegalRepresentative: null,
                    RepresentationType: "إضافة لتركة",
                    DeceasedName: "مورث",
                    DeceasedFather: null,
                    DeceasedFamily: null,
                    RepresentativeName: "ممثل",
                    RepresentativeFather: "أب",
                    RepresentativeFamily: "عائلة",
                    RepresentativeCapacity: "ولي",
                    RepresentativeLegalRepresentative: null),
            },
        };

        // طالب التنفيذ هنا وريث حي، وممثله الشرعي له لا للمورث؛ فيجتمع مع ورثة المورث.
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
    }

    [Theory]
    [InlineData("executed")]
    [InlineData("deposit")]
    public void ValidateExclusivity_AllowsExecutedNaturalPersonHeirsWithRepresentative_OnExecutedSide(string side)
    {
        var request = new DocumentUpsertRequest
        {
            GeneralEntitySide = side,
            ExecutedNaturalPersons = new List<ExecutedNaturalPersonDto>
            {
                new(
                    Id: null,
                    Name: "منفذ",
                    Father: null,
                    Family: null,
                    AddressType: "عنوان",
                    AddressOrRepresentative: "عنوان",
                    RepresentationType: "أصالة",
                    DeceasedName: null,
                    DeceasedFather: null,
                    DeceasedFamily: null,
                    RepresentativeName: "ممثل",
                    RepresentativeFather: "أب",
                    RepresentativeFamily: "عائلة",
                    RepresentativeCapacity: "ولي",
                    RepresentativeAddressType: "عنوان",
                    RepresentativeAddress: "عنوان الممثل",
                    Heirs: new List<ExecutedHeirDto>
                    {
                        new(null, "وريث", null, null, "عنوان", ""),
                    }),
            },
        };

        // المنفذ عليه الطبيعي هنا حي/وريث حي، وممثله الشرعي له لا للمورث.
        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
    }

    [Fact]
    public void ValidateExclusivity_IgnoresLegalParties()
    {
        var request = new DocumentUpsertRequest
        {
            BorrowerNature = "legal",
            BorrowerHeirs = new List<HeirDto>
            {
                new(null, "وريث", "أب", "عائلة", "أصالة", "عنوان", "عنوان"),
            },
            BorrowerRepresentativeName = "ممثل",
            BorrowerRepresentativeFather = "أب",
            BorrowerRepresentativeFamily = "عائلة",
        };

        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
    }

    [Fact]
    public void ValidateExclusivity_AllowsRepresentativeWithoutHeirs()
    {
        var request = new DocumentUpsertRequest
        {
            BorrowerNature = "natural",
            BorrowerRepresentativeName = "ممثل",
            BorrowerRepresentativeFather = "أب",
            BorrowerRepresentativeFamily = "عائلة",
        };

        DocumentValidator.ValidateRepresentativeHeirExclusivity(request);
    }
}
