using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات الوحدة لمصدر التسلسل المشترك للقطات أطراف الاستئناف:
/// فكّ/إعادة تسلسل، وتحديث صور الجهات العامة بواسطة (Kind, PartyId) مع تغطية
/// الحالات الحدّية (فارغ/null، بلا تطابق، صفوف متعددة، اسم مخزَّن مختلف).
/// </summary>
public class AppealSnapshotSerializerTests
{
    [Fact]
    public void Serialize_Deserialize_RoundTrips()
    {
        var parties = new List<AppealPartyDto>
        {
            new("applicant-entity", 7, "مصلحة الضرائب"),
            new("guarantor", 3, "ناجي"),
        };
        var json = AppealSnapshotSerializer.SerializeParties(parties);
        var back = AppealSnapshotSerializer.DeserializeParties(json);

        Assert.Equal(2, back.Count);
        Assert.Equal("applicant-entity", back[0].Kind);
        Assert.Equal(7, back[0].PartyId);
        Assert.Equal("مصلحة الضرائب", back[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("null")]
    public void DeserializeParties_NullOrEmpty_ReturnsEmpty(string json)
    {
        var result = AppealSnapshotSerializer.DeserializeParties(json);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void UpdateEntityParties_NoMatch_ReturnsSameJson()
    {
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto> { new("applicant-entity", 1, "جهة أ") });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 999), "جهة ب" } });

        Assert.Equal(json, result);
    }

    [Fact]
    public void UpdateEntityParties_EmptyJson_ReturnsSame()
    {
        const string json = "[]";
        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 1), "جديد" } });
        Assert.Equal(json, result);
    }

    [Fact]
    public void UpdateEntityParties_DifferingStoredName_MatchesByPartyId()
    {
        // الاسم المخزَّن يختلف عن الاسم المعياري — تطابق الثغرة عبر PartyId لا عبر الاسم
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto> { new("applicant-entity", 42, "مصلحة الضرائب") });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 42), "الهيئة الضريبية الوطنية" } });

        var parties = AppealSnapshotSerializer.DeserializeParties(result);
        Assert.Single(parties);
        Assert.Equal("الهيئة الضريبية الوطنية", parties[0].Name);
    }

    [Fact]
    public void UpdateEntityParties_MultipleRows_UpdatesOnlyMatchingPublic()
    {
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto>
            {
                new("applicant-entity", 1, "جهة أ"),
                new("applicant-entity", 2, "جهة ب"),
                new("guarantor", 3, "ناجي الفردي"),
            });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 2), "الجهة الموحدة" } });

        var parties = AppealSnapshotSerializer.DeserializeParties(result);
        Assert.Equal(3, parties.Count);
        Assert.Equal("جهة أ", parties[0].Name);
        Assert.Equal("الجهة الموحدة", parties[1].Name);
        Assert.Equal("ناجي الفردي", parties[2].Name);
    }

    [Fact]
    public void UpdateEntityParties_IgnoresNonPublicKinds()
    {
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto> { new("borrower", 99, "المقترض") });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("borrower", 99), "يجب ألا يتغير" } });

        Assert.Equal(json, result);
    }

    [Fact]
    public void UpdateEntityParties_NullJson_NormalizesToEmptyArray()
    {
        string? json = null;
        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 1), "جديد" } });
        Assert.Equal("[]", result);
    }

    [Fact]
    public void DeserializeParties_CorruptedJson_ReturnsEmptyWithoutThrow()
    {
        const string corrupted = "{ bad json";
        var result = AppealSnapshotSerializer.DeserializeParties(corrupted);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void UpdateEntityParties_CorruptedJson_DoesNotThrowAndKeepsOriginal()
    {
        const string corrupted = "{ bad json";
        var result = AppealSnapshotSerializer.UpdateEntityParties(
            corrupted, new Dictionary<(string, int), string> { { ("applicant-entity", 1), "جديد" } });
        Assert.Equal(corrupted, result);
    }

    [Fact]
    public void UpdateEntityParties_NullLiteral_NormalizesToEmptyArray()
    {
        const string json = "null";
        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("applicant-entity", 1), "جديد" } });
        Assert.Equal("[]", result);
    }

    [Fact]
    public void UpdateEntityParties_ExecutionApplicant_MatchingPartyId()
    {
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto> { new("execution-applicant", 901, "المؤسسة السورية للتجارة") });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("execution-applicant", 901), "هيئة التجارة الموحدة" } });

        var parties = AppealSnapshotSerializer.DeserializeParties(result);
        Assert.Single(parties);
        Assert.Equal("execution-applicant", parties[0].Kind);
        Assert.Equal(901, parties[0].PartyId);
        Assert.Equal("هيئة التجارة الموحدة", parties[0].Name);
    }

    [Fact]
    public void UpdateEntityParties_ExecutionApplicant_NoMatch_KeepsStoredName()
    {
        var json = AppealSnapshotSerializer.SerializeParties(
            new List<AppealPartyDto> { new("execution-applicant", 901, "المؤسسة السورية للتجارة") });

        var result = AppealSnapshotSerializer.UpdateEntityParties(
            json, new Dictionary<(string, int), string> { { ("execution-applicant", 999), "جهة أخرى" } });

        var parties = AppealSnapshotSerializer.DeserializeParties(result);
        Assert.Single(parties);
        Assert.Equal("المؤسسة السورية للتجارة", parties[0].Name);
    }
}
