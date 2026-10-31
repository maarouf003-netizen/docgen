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

    /// <summary>
    /// قائمة الحقول الممنوعة تُقرأ من البيان نفسه (P3-9) بدل تكرارها في الكود،
    /// فلا تنحرف قائمتان عند تعديل العقد.
    /// </summary>
    private static void AssertNoForbidden(JsonElement manifest, string block, IEnumerable<string> actual)
    {
        var forbidden = Array(manifest.GetProperty("forbidden"), block);
        foreach (var name in forbidden)
            Assert.DoesNotContain(name, actual);
    }

    /// <summary>
    /// قائمة nullability **صغيرة ومقصودة**: الحقول التي يعتمد عليها العميل في تمييز
    /// «غائب» عن «صفر/فراغ». لا خريطة أنواع كاملة (تجنّب صيانة 50 مدخلًا).
    /// </summary>
    private static bool IsNullableWire(Type type, string wireName)
    {
        var property = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name) == wireName);
        return new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;
    }

    private static void AssertNullable(Type type, params string[] wireNames)
    {
        foreach (var name in wireNames)
            Assert.True(IsNullableWire(type, name), $"{type.Name}.{name} يجب أن يكون nullable");
    }

    private static void AssertNotNullable(Type type, params string[] wireNames)
    {
        foreach (var name in wireNames)
            Assert.False(IsNullableWire(type, name), $"{type.Name}.{name} يجب ألا يكون nullable");
    }

    [Fact]
    public void SummaryContract_MatchesManifest_WithoutFullMessage()
    {
        var manifest = LoadManifest();
        var actual = WireNames(typeof(HeadAlertSummaryDto));

        AssertSameMembers(Array(manifest, "headAlertSummary"), actual);
        AssertNoForbidden(manifest, "headAlertSummary", actual);

        // القائمة تعيد معاينة صريحة لا النص الكامل (§7.3/§9.2).
        Assert.Contains("messagePreview", actual);
        Assert.Contains("messageLength", actual);
        Assert.Contains("previewTruncated", actual);
    }

    [Fact]
    public void DetailContract_MatchesManifest_PlusExplicitlyRetainedFields()
    {
        var manifest = LoadManifest();
        var expected = Array(manifest, "headAlertDetail")
            .Concat(Array(manifest, "retainedHeadAlertDetail"));

        AssertSameMembers(expected, WireNames(typeof(HeadAlertDto)));
        AssertNoForbidden(manifest, "headAlertDetail", WireNames(typeof(HeadAlertDto)));

        // النص الكامل في التفاصيل وحدها، وقائمة المستلمين تُطلب من سطحها (§9.2).
        var actual = WireNames(typeof(HeadAlertDto));
        Assert.Contains("message", actual);
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
        AssertNoForbidden(manifest, "pagedResult", actual);
    }

    [Fact]
    public void Nullability_MatchesSmallContractList()
    {
        // الحقول القابلة للفراغ التي يميّز بها العميل «غائب» عن «0/فراغ»:
        // مراجع اختيارية، حالة قراءة غير المستلم، عدّادات غير محسوبة، واسم منشئ محذوف.
        AssertNullable(
            typeof(HeadAlertSummaryDto),
            "documentId", "documentTitle", "targetLawyerId", "targetLawyerName",
            "isRead", "recipientCount", "unreadCount", "createdByName",
            "appealId", "reviewLetterId", "delegationId", "executionCircuitId", "archivedAt");
        AssertNotNullable(
            typeof(HeadAlertSummaryDto),
            "id", "kind", "messagePreview", "messageLength", "previewTruncated",
            "targetType", "createdAt", "lastContentChangedAt", "isArchived");

        AssertNullable(
            typeof(HeadAlertDto),
            "documentId", "documentTitle", "targetLawyerId", "targetLawyerName",
            "delegationId", "appealId", "reviewLetterId", "publicEntityId",
            "executionCircuitId", "archivedAt", "createdByName", "isRead",
            "recipientCount", "unreadCount");
        AssertNotNullable(
            typeof(HeadAlertDto),
            "id", "kind", "message", "targetType", "createdAt",
            "lastContentChangedAt", "isArchived", "canViewRecipients");

        AssertNullable(typeof(HeadAlertRecipientDto), "readAt");
        AssertNotNullable(typeof(HeadAlertRecipientDto), "id", "userId", "fullName", "role", "isRead");
    }
}
