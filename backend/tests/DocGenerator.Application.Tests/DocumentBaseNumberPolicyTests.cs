using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

public class DocumentBaseNumberPolicyTests
{
    private static Document Doc(int? circuitId = 1, string? fileNumber = "500", string? fileYear = "2024") => new()
    {
        Id = 1,
        ExecutionCircuitId = circuitId,
        FileNumber = fileNumber,
        FileYear = fileYear,
        FileType = "صلح",
        IsDraft = false,
        NeedsRegistration = false,
    };

    private static DocumentBaseNumber Row(
        int year, string number, int? circuitId = 1, DateTime? created = null,
        DateTime? voided = null, string source = NumberSources.Registration) => new()
    {
        // معرّف حتمي: `string.GetHashCode` عشوائي لكل عملية (وقد يتصادم عبر السنوات
        // عند القص إلى 4 بت) فيسقط `Assert.Single(IsEffective)` عشوائيًا. الرقم نفسه
        // فريد داخل السنة في كل استدعاءات هذا الملف.
        Id = year * 1000 + int.Parse(number, CultureInfo.InvariantCulture),
        DocumentId = 1,
        ExecutionCircuitId = circuitId,
        CircuitName = circuitId == 1 ? "دائرة أ" : "دائرة ب",
        Year = year,
        BaseNumber = number,
        NumberKey = DocumentBaseNumberPolicy.NormalizeNumber(number),
        TypeKey = "صلح",
        FileType = "صلح",
        Source = source,
        CreatedById = 1,
        CreatedAt = created ?? new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        VoidedAt = voided,
    };

    [Theory]
    [InlineData("صلح", "صلح")]
    [InlineData("  صُلح  ", "صلح")]
    [InlineData("إثبات", "اثبات")]
    [InlineData("دعوى", "دعوي")]
    public void NormalizeType_IsStableAndArabicAware(string input, string expected)
    {
        var first = DocumentBaseNumberPolicy.NormalizeType(input);
        Assert.Equal(expected, first);
        Assert.Equal(first, DocumentBaseNumberPolicy.NormalizeType(first));
    }

    [Theory]
    [InlineData(" ١٢٣ ٤٥ ", "12345")]
    [InlineData("۱۲۳", "123")]
    [InlineData("1 2\t3", "123")]
    public void NormalizeNumber_NormalizesDigitsAndRemovesWhitespace(string input, string expected)
        => Assert.Equal(expected, DocumentBaseNumberPolicy.NormalizeNumber(input));

    [Fact]
    public void NormalizeNumber_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() => DocumentBaseNumberPolicy.NormalizeNumber("   "));

    [Theory]
    [InlineData("2026", 2026, 2026)]
    [InlineData(" ٢٠٢٥ ", 2026, 2025)]
    [InlineData("", 2026, null)]
    public void ValidateYear_AcceptsRangeAndEmpty(string raw, int current, int? expected)
        => Assert.Equal(expected, DocumentBaseNumberPolicy.ValidateYear(raw, current));

    [Theory]
    [InlineData("1899")]
    [InlineData("2027")]
    [InlineData("abc")]
    public void ValidateYear_RejectsOutOfRangeOrNonNumeric(string raw) =>
        Assert.Throws<ArgumentException>(() => DocumentBaseNumberPolicy.ValidateYear(raw, 2026));

    [Fact]
    public void IsEligible_RequiresPriorIdentityInCurrentCircuit_AndNoCurrentIdentity()
    {
        var doc = Doc();
        doc.BaseNumbers.Add(Row(2024, "500"));
        Assert.True(DocumentBaseNumberPolicy.IsEligible(doc, 2026));

        doc.BaseNumbers.Add(Row(2026, "600"));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(doc, 2026));
    }

    [Fact]
    public void IsEligible_IgnoresRowsFromOtherCircuit_AndVoidedRows()
    {
        var doc = Doc(circuitId: 1);
        doc.BaseNumbers.Add(Row(2024, "500", circuitId: 2));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(doc, 2026));

        doc.BaseNumbers.Add(Row(2024, "501", circuitId: 1, voided: DateTime.UtcNow));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(doc, 2026));
    }

    [Fact]
    public void IsEligible_RejectsDraftPendingDeletedAndCircuitless()
    {
        var draft = Doc();
        draft.IsDraft = true;
        draft.BaseNumbers.Add(Row(2024, "500"));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(draft, 2026));

        var pending = Doc();
        pending.NeedsRegistration = true;
        pending.BaseNumbers.Add(Row(2024, "500"));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(pending, 2026));

        var deleted = Doc();
        deleted.IsDeleted = true;
        deleted.BaseNumbers.Add(Row(2024, "500"));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(deleted, 2026));

        var noCircuit = Doc(circuitId: null);
        noCircuit.BaseNumbers.Add(Row(2024, "500"));
        Assert.False(DocumentBaseNumberPolicy.IsEligible(noCircuit, 2026));
    }

    [Fact]
    public void ResolveEffective_PicksLatestCurrentCircuitRow_NotFutureOrVoidedOrOtherCircuit()
    {
        var doc = Doc();
        doc.BaseNumbers.Add(Row(2023, "100"));
        doc.BaseNumbers.Add(Row(2025, "200"));
        doc.BaseNumbers.Add(Row(2026, "300"));
        doc.BaseNumbers.Add(Row(2027, "400"));
        doc.BaseNumbers.Add(Row(2026, "999", circuitId: 2));
        doc.BaseNumbers.Add(Row(2025, "888", voided: DateTime.UtcNow));

        var effective = DocumentBaseNumberPolicy.ResolveEffective(doc, 2026);
        Assert.NotNull(effective);
        Assert.Equal(2026, effective!.Year);
        Assert.Equal("300", effective.Number);
        Assert.Equal("300", EffectiveFileIdentity.Number(doc, 2026));
    }

    [Fact]
    public void ResolveEffective_FallsBackToNull_WhenNoCurrentCircuitIdentity()
    {
        var doc = Doc();
        doc.BaseNumbers.Add(Row(2024, "100", circuitId: 2));
        Assert.Null(DocumentBaseNumberPolicy.ResolveEffective(doc, 2026));
        Assert.Equal("500", EffectiveFileIdentity.Number(doc, 2026));
    }

    [Fact]
    public void BuildHistory_IncludesVoidedRows_AndMarksOnlyEffective()
    {
        var doc = Doc();
        doc.BaseNumbers.Add(Row(2024, "100", voided: DateTime.UtcNow));
        doc.BaseNumbers.Add(Row(2025, "200"));
        doc.BaseNumbers.Add(Row(2026, "300"));

        var history = DocumentBaseNumberPolicy.BuildHistory(doc, doc.BaseNumbers, 2026);

        Assert.Equal(3, history.Count);
        Assert.Single(history, h => h.IsEffective);
        Assert.Single(history, h => h.IsVoided);
        Assert.Equal(2026, history[0].Year);
    }
}
