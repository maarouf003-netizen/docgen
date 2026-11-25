using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس سياسة إصدار `API` (`P4-04b` / `ADR-0011`): يمنع انحراف السطح العام بلا قرار
/// إصدار موثّق — معرّفات عمليات حتمية، وعلامة إصدار اللقطة مطابقة لاسم ملفها ولأي بادئة
/// مسار مُصدَّرة، وسياسة الإصدار/الإهمال موثّقة ومربوطة من الفهرس، وسجل الإهمال مطابق
/// لمخططه وللقطة.
///
/// <para>
/// لا يتصل بقاعدة ولا بمضيف: يقرأ ملفات العقد من المستودع فقط، فيعمل في كل بيئة. أي
/// مخالفة تفشل هنا قبل أن تصل إلى العميل المولَّد أو إلى مستهلك خارجي.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ApiVersioningPolicyTests
{
    private const string SnapshotRelativePath = "docs/openapi/docgen-v1.json";
    private const string DeprecationsRelativePath = "docs/openapi/deprecations.json";
    private const string PolicyRelativePath = "docs/API_VERSIONING.md";
    private const string AdrRelativePath = "docs/adr/0011-api-versioning.md";
    private const string ReadmeRelativePath = "docs/README.md";

    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete"];

    /// <summary>`Controller_Action` بحروف وأرقام فقط — الشكل الذي يثبته `P4-04`.</summary>
    private static readonly Regex StableOperationId = new(
        "^[A-Za-z][A-Za-z0-9]*_[A-Za-z][A-Za-z0-9]*$",
        RegexOptions.CultureInvariant);

    /// <summary>علامة إصدار العقد المقبولة: `v` + رقم موجب بلا أصفار بادئة.</summary>
    private static readonly Regex ContractVersion = new(
        "^v(?<number>[1-9][0-9]*)$",
        RegexOptions.CultureInvariant);

    /// <summary>بادئة مسار مُصدَّرة: `/api/v1` أو `/api/v1/…`.</summary>
    private static readonly Regex VersionedPathPrefix = new(
        "^/api/v(?<number>[1-9][0-9]*)(?:/|$)",
        RegexOptions.CultureInvariant);

    /// <summary>مخطط سجل الإهمال: لا مفاتيح خارج هذه المجموعة (يمنع الانحراف الصامت).</summary>
    private static readonly HashSet<string> DeprecationKeys = new(StringComparer.Ordinal)
    {
        "operationId",
        "deprecatedSince",
        "removalVersion",
        "removalDate",
        "replacement",
    };

    private const string DateFormat = "yyyy-MM-dd";

    [Fact]
    public void Snapshot_FileNameAndVersionMarkerAgreeWithRoutes()
    {
        using var snapshot = ReadSnapshot();
        var root = snapshot.RootElement;
        var version = root.GetProperty("info").GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version), "لقطة OpenAPI بلا علامة إصدار في info.version.");

        var fileName = Path.GetFileName(SnapshotRelativePath);
        Assert.True(
            fileName.EndsWith($"-{version}.json", StringComparison.Ordinal),
            $"اسم لقطة OpenAPI '{fileName}' لا يحمل علامة الإصدار المعلنة '{version}' — " +
            "الإصدار في info.version والاسم يجب أن يتطابقا قبل أي نشر.");

        var current = VersionNumber(version!);
        foreach (var (path, _) in Operations(root))
        {
            var match = VersionedPathPrefix.Match(path);
            if (!match.Success)
                continue;

            Assert.True(
                match.Groups["number"].Value == current.ToString(CultureInfo.InvariantCulture),
                $"المسار '{path}' يحمل إصدارًا غير إصدار العقد '{version}' — " +
                "لا تُصدَّر مسارات بنسخة أخرى قبل قرار إصدار موثّق.");
        }
    }

    [Fact]
    public void Snapshot_OperationIdsAreStableAndUnique()
    {
        using var snapshot = ReadSnapshot();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;

        foreach (var (path, operation) in Operations(snapshot.RootElement))
        {
            count++;
            var operationId = operation.TryGetProperty("operationId", out var value)
                ? value.GetString()
                : null;

            Assert.False(
                string.IsNullOrWhiteSpace(operationId),
                $"'{path}' عملية بلا operationId — التوليد والعميل لا يستقران.");
            Assert.True(
                StableOperationId.IsMatch(operationId!),
                $"operationId '{operationId}' خارج النمط المستقر Controller_Action في '{path}'.");
            Assert.True(ids.Add(operationId!), $"operationId مكرر: '{operationId}'.");
        }

        Assert.True(count >= 100, $"عدد عمليات العقد غير متوقع: {count}.");
    }

    [Fact]
    public void PolicyAndAdr_AreLinkedAndDocumentTheContractPaths()
    {
        var policy = ReadText(PolicyRelativePath);
        var adr = ReadText(AdrRelativePath);
        var readme = ReadText(ReadmeRelativePath);

        Assert.Contains(SnapshotRelativePath, policy, StringComparison.Ordinal);
        Assert.Contains(DeprecationsRelativePath, policy, StringComparison.Ordinal);
        Assert.Contains("operationId", policy, StringComparison.Ordinal);
        Assert.Contains("API_VERSIONING.md", adr, StringComparison.Ordinal);
        Assert.Contains("API_VERSIONING.md", readme, StringComparison.Ordinal);
        Assert.Contains("adr/0011-api-versioning.md", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void DeprecationManifest_MatchesSchemaAndSnapshot()
    {
        using var snapshot = ReadSnapshot();
        var operations = Operations(snapshot.RootElement).ToList();
        var operationIds = operations
            .Select(item => item.Operation.GetProperty("operationId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var deprecatedOperationIds = operations
            .Where(item => item.Operation.TryGetProperty("deprecated", out var flag)
                && flag.ValueKind == JsonValueKind.True)
            .Select(item => item.Operation.GetProperty("operationId").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        var currentVersion = VersionNumber(
            snapshot.RootElement.GetProperty("info").GetProperty("version").GetString()!);

        using var manifest = JsonDocument.Parse(ReadText(DeprecationsRelativePath));
        Assert.Equal(JsonValueKind.Array, manifest.RootElement.ValueKind);

        var documented = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in manifest.RootElement.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Object, entry.ValueKind);

            var unknownKeys = entry.EnumerateObject()
                .Select(property => property.Name)
                .Where(key => !DeprecationKeys.Contains(key))
                .ToArray();
            Assert.True(
                unknownKeys.Length == 0,
                $"سجل الإهمال يحمل مفاتيح خارج المخطط: {string.Join(", ", unknownKeys)}.");

            var operationId = RequiredString(entry, "operationId");
            Assert.True(documented.Add(operationId), $"عملية مكررة في سجل الإهمال: '{operationId}'.");
            Assert.True(
                operationIds.Contains(operationId),
                $"سجل الإهمال يشير إلى عملية غير موجودة في اللقطة: '{operationId}'.");
            Assert.True(
                deprecatedOperationIds.Contains(operationId),
                $"'{operationId}' في سجل الإهمال لكنها غير موسومة deprecated: true في اللقطة — " +
                "الإعلان يجب أن يظهر في العقد لا في السجل وحده.");

            var deprecatedSince = ParseDate(RequiredString(entry, "deprecatedSince"), "deprecatedSince", operationId);

            // وجوده غير الفارغ إلزامي: إهمال بلا مسار هجرة ممنوع.
            RequiredString(entry, "replacement");

            var hasVersion = entry.TryGetProperty("removalVersion", out var removalVersion);
            var hasDate = entry.TryGetProperty("removalDate", out var removalDateElement);
            Assert.True(
                hasVersion || hasDate,
                $"'{operationId}' بلا removalVersion أو removalDate — لا إزالة بلا موعد معلن.");

            if (hasVersion)
            {
                var raw = RequiredString(entry, "removalVersion");
                var match = ContractVersion.Match(raw);
                Assert.True(match.Success, $"removalVersion غير صالح لـ'{operationId}': '{raw}'.");
                var version = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
                Assert.True(
                    version > currentVersion,
                    $"removalVersion '{raw}' يجب أن يكون لاحقًا لإصدار العقد الحالي (v{currentVersion}).");
            }

            if (hasDate)
            {
                Assert.Equal(JsonValueKind.String, removalDateElement.ValueKind);
                var removal = ParseDate(RequiredString(entry, "removalDate"), "removalDate", operationId);
                Assert.True(
                    removal > deprecatedSince,
                    $"removalDate يجب أن يكون بعد deprecatedSince في '{operationId}'.");
            }
        }

        var undocumented = deprecatedOperationIds
            .Where(id => !documented.Contains(id))
            .ToArray();
        Assert.True(
            undocumented.Length == 0,
            "عمليات موسومة deprecated في اللقطة بلا سجل إهمال: " + string.Join(", ", undocumented));
    }

    /// <summary>يقرأ لقطة العقد من المستودع (لا من الخادم) — حارس مستقل عن البيئة.</summary>
    private static JsonDocument ReadSnapshot()
        => JsonDocument.Parse(ReadText(SnapshotRelativePath));

    private static IEnumerable<(string Path, JsonElement Operation)> Operations(JsonElement root)
    {
        foreach (var pathItem in root.GetProperty("paths").EnumerateObject())
        {
            foreach (var method in HttpMethods)
            {
                if (pathItem.Value.TryGetProperty(method, out var operation))
                    yield return (pathItem.Name, operation);
            }
        }
    }

    private static int VersionNumber(string version)
    {
        var match = ContractVersion.Match(version);
        Assert.True(match.Success, $"علامة إصدار العقد غير صالحة: '{version}'.");
        return int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
    }

    private static DateOnly ParseDate(string value, string field, string operationId)
    {
        Assert.True(
            DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date),
            $"{field} غير صالح في '{operationId}' — الصيغة المعتمدة {DateFormat}: '{value}'.");
        return date;
    }

    private static string RequiredString(JsonElement owner, string property)
    {
        Assert.True(owner.TryGetProperty(property, out var value), $"سجل الإهمال بلا الحقل الإلزامي '{property}'.");
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        var text = value.GetString();
        Assert.False(string.IsNullOrWhiteSpace(text), $"الحقل '{property}' فارغ في سجل الإهمال.");
        return text!;
    }

    private static string ReadText(string relativePath)
    {
        var path = Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"ملف العقد/السياسة مفقود: {relativePath} (المتوقع: {path}).");
        return File.ReadAllText(path);
    }

    /// <summary>يصعد من مجلد تشغيل الاختبار حتى يجد علامة الجذر (<c>AGENTS.md</c>).</summary>
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر العثور على جذر المستودع (AGENTS.md) ابتداءً من '{AppContext.BaseDirectory}'.");
    }
}
