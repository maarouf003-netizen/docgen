using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Tests;

public class DocumentValidatorTests
{
    private static DocumentUpsertRequest NewRequest(string side = "") => new()
    {
        GeneralEntitySide = side,
        Guarantors = new List<GuarantorDto>(),
        Assets = new List<AssetDto>(),
        BorrowerHeirs = new List<HeirDto>(),
    };

    [Theory]
    [InlineData("applicant")]
    [InlineData("executed")]
    public void ValidateSide_AcceptsValidSides_AndNormalizesWhitespace(string side)
    {
        var request = NewRequest(side: $"  {side}  ");

        DocumentValidator.ValidateSide(request);

        Assert.Equal(side, request.GeneralEntitySide);
    }

    [Fact]
    public void ValidateSide_EmptyFallsBackToApplicant()
    {
        var request = NewRequest(side: "");

        DocumentValidator.ValidateSide(request);

        Assert.Equal("applicant", request.GeneralEntitySide);
    }

    [Fact]
    public void ValidateSide_InvalidSide_Throws()
    {
        var request = NewRequest(side: "غير معروف");

        var ex = Assert.Throws<ArgumentException>(() => DocumentValidator.ValidateSide(request));
        Assert.Contains("صفة الجهة العامة غير صالحة", ex.Message);
    }

    [Fact]
    public void ValidateExecutedRequest_NonExecutedSide_PassesWithoutConstraints()
    {
        var request = NewRequest(side: "applicant");

        DocumentValidator.ValidateExecutedRequest(request);
    }

    [Fact]
    public void ValidateExecutedRequest_ExecutedSide_RequiresFileNumberAndYear()
    {
        var request = NewRequest(side: "executed");

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.ValidateExecutedRequest(request));
        Assert.Contains("رقم وسنة الملف", ex.Message);
    }

    [Fact]
    public void ValidateExecutedRequest_ExecutedSide_RejectsBankingContract()
    {
        var request = NewRequest(side: "executed");
        request.FileNumber = "100";
        request.FileYear = "2026";
        request.ContractTypeSelector = "مصرفي";

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.ValidateExecutedRequest(request));
        Assert.Contains("بعقد عادي فقط", ex.Message);
    }

    [Fact]
    public void ValidateRegistrationDate_ApplicantWithFileNumberButNoDate_Throws()
    {
        var request = NewRequest(side: "applicant");
        request.FileNumber = "100";
        request.FileYear = "2026";

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.ValidateRegistrationDate(request));
        Assert.Contains("تاريخ قيد الملف مطلوب", ex.Message);
    }

    [Fact]
    public void ValidateRegistrationDate_InvalidFreeDate_ThrowsWithExampleMessage()
    {
        var request = NewRequest(side: "applicant");
        request.FileNumber = "100";
        request.FileYear = "2026";
        request.FileRegistrationDate = "32/13/2026";

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.ValidateRegistrationDate(request));
        Assert.Contains("1/8/2026", ex.Message);
    }

    [Fact]
    public void ValidateRegistrationDate_ValidFreeDate_Passes()
    {
        var request = NewRequest(side: "applicant");
        request.FileNumber = "100";
        request.FileYear = "2026";
        request.FileRegistrationDate = "1/8/2026";

        DocumentValidator.ValidateRegistrationDate(request);
    }

    [Fact]
    public void TryParseDate_SupportsDocumentedFormats()
    {
        Assert.True(DocumentValidator.TryParseDate("1/8/2026", out var date));
        Assert.Equal(new DateTime(2026, 8, 1), date);

        Assert.True(DocumentValidator.TryParseDate("2026-08-01", out _));
        Assert.False(DocumentValidator.TryParseDate("not-a-date", out _));
        Assert.False(DocumentValidator.TryParseDate(null, out _));
    }

    [Fact]
    public void ParseDateTime_EmptyMeansNull_AndInvalidThrowsWithFieldName()
    {
        Assert.Null(DocumentValidator.ParseDateTime("", "تاريخ التجربة"));
        Assert.Null(DocumentValidator.ParseDateTime(null, "تاريخ التجربة"));

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.ParseDateTime("bad", "تاريخ التجربة"));
        Assert.Contains("تاريخ التجربة", ex.Message);
    }

    [Fact]
    public void RequireField_MissingKeyThrowsWithValueLabel()
    {
        var fields = new Dictionary<string, string?> { ["k"] = "  " };

        var ex = Assert.Throws<ArgumentException>(
            () => DocumentValidator.RequireField(fields, "k", "التسمية"));
        Assert.Contains("التسمية", ex.Message);
    }
}

public class AssetMapperTests
{
    [Fact]
    public void NormalizeOwners_TrimsSkipsEmpty_RemovesDuplicatesKeepsOrder()
    {
        var result = AssetMapper.NormalizeOwners(new[]
        {
            "  سامر ",
            "",
            null,
            "سامر",
            "أحمد",
            " خالد ",
        });

        Assert.Equal(new[] { "سامر", "أحمد", "خالد" }, result.Select(o => o.Name));
        Assert.Equal(0, result[0].Order);
        Assert.Equal(1, result[1].Order);
        Assert.Equal(2, result[2].Order);
    }

    [Fact]
    public void NormalizeOwners_NullInput_ReturnsEmptyList()
    {
        var result = AssetMapper.NormalizeOwners(null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
