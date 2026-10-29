using System.Reflection;
using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس عقد التنبيهات على السلك (الخطة §9.2/§14.1–§14.2):
/// يعكس أعضاء سجلات <see cref="HeadAlertSummaryDto"/> و<see cref="HeadAlertDto"/>
/// و<see cref="HeadAlertRecipientDto"/> و<see cref="CreateHeadAlertRequest"/> و<see cref="PagedResult{T}"/>
/// ويقارنها حقلًا بحقل بالبيان المشترك
/// <c>frontend/src/test/contracts/head-alert-contracts.json</c>.
///
/// البيان **مكتوب من الخطة** لا مولَّد من الـDTOs — فلا يكون الاختبار دائريًا.
/// أي حقل جديد أو محذوف أو مُعاد تسميته في أي طرف يُفشل هذا الحارس عمدًا.
/// </summary>
public class HeadAlertContractTests
{
    private const string ManifestRelativePath = "frontend/src/test/contracts/head-alert-contracts.json";

    private static IReadOnlyList<string> Names(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();

    /// <summary>أسماء الحقول على السلك (camelCase) — نفس سياسة ASP.NET الافتراضية.</summary>
    private static IReadOnlyList<string> WireNames(Type type) =>
        Names(type).Select(n => JsonNamingPolicy.CamelCase.ConvertName(n)).ToList();

    private static JsonElement LoadManifest()
    {
        var path = RepoRootLocator.ResolveFile(ManifestRelativePath);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string[] Array(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();

    private static void AssertSameMembers(IEnumerable<string> expected, IEnumerable<string> actual)
        => Assert.Equal(
            expected.OrderBy(n => n, StringComparer.Ordinal),
            actual.OrderBy(n => n, StringComparer.Ordinal));

    [Fact]
    public void SummaryContract_MatchesManifest_WithoutFullMessage()
    {
        var manifest = LoadManifest();
        var actual = WireNames(typeof(HeadAlertSummaryDto));

        AssertSameMembers(Array(manifest, "headAlertSummary"), actual);

        // القائمة تعيد معاينة صريحة لا النص الكامل، ولا حقل مشتق مرفوض (§7.3/§9.2).
        Assert.Contains("messagePreview", actual);
        Assert.Contains("messageLength", actual);
        Assert.Contains("previewTruncated", actual);
        Assert.DoesNotContain("message", actual);
        Assert.DoesNotContain("hasFullMessage", actual);
    }

    [Fact]
    public void DetailContract_MatchesManifest_PlusExplicitlyRetainedFields()
    {
        var manifest = LoadManifest();
        var expected = Array(manifest, "headAlertDetail")
            .Concat(Array(manifest, "retainedHeadAlertDetail"));

        AssertSameMembers(expected, WireNames(typeof(HeadAlertDto)));

        // النص الكامل في التفاصيل وحدها، ولا قائمة مستلمين في جسم التفاصيل (§9.2).
        var actual = WireNames(typeof(HeadAlertDto));
        Assert.Contains("message", actual);
        Assert.DoesNotContain("recipients", actual);
        Assert.Contains("canViewRecipients", actual);
    }

    [Fact]
    public void RecipientContract_MatchesManifest()
    {
        var manifest = LoadManifest();
        AssertSameMembers(Array(manifest, "headAlertRecipient"), WireNames(typeof(HeadAlertRecipientDto)));
    }

    [Fact]
    public void CreateRequestContract_MatchesManifest_AndHasNoControlFields()
    {
        var manifest = LoadManifest();
        var actual = WireNames(typeof(CreateHeadAlertRequest));

        AssertSameMembers(Array(manifest, "createHeadAlertRequest"), actual);

        // العقد اليدوي لا يحمل — بنيويًا — أي حقل تحكم داخلي (§9.2/§12).
        Assert.Equal(4, actual.Count);
        foreach (var forbidden in new[]
                 {
                     "kind", "dedupeKey", "executionCircuitId", "delegationId",
                     "appealId", "recipientUserId", "reviewLetterId", "publicEntityId",
                 })
        {
            Assert.DoesNotContain(forbidden, actual);
        }
    }

    [Fact]
    public void PagedResultContract_MatchesManifest_WithoutHasNextPage()
    {
        var manifest = LoadManifest();
        var actual = WireNames(typeof(PagedResult<>));

        AssertSameMembers(Array(manifest, "pagedResult"), actual);
        Assert.DoesNotContain("hasNextPage", actual);
        Assert.DoesNotContain("pageSize", actual);
    }
}
