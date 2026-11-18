using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P4-04` (الجزء الأول): عقد `OpenAPI` مولَّد من الكود ومثبَّت في المستودع، مع ثوابت
/// تمنع الانحراف الصامت — أساس توليد عميل `TS` لاحقًا (`P6-06`).
///
/// <para>
/// اللقطة في <c>docs/openapi/docgen-v1.json</c> **مُطبَّعة** (مفاتيح مرتّبة، `LF`، بلا
/// مفاتيح فارغة) كي لا يتغيّر الملف بين منصّة وأخرى. أي تغيير في سطح `API` يفشل هنا
/// حتى يُحدَّث العقد واعٍ: <c>UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiContractTests</c>.
/// </para>
/// </summary>
[Collection(ApiTestCollection.Name)]
public class OpenApiContractTests
{
    private const string SnapshotRelativePath = "docs/openapi/docgen-v1.json";
    private const string UpdateVariable = "UPDATE_OPENAPI_SNAPSHOT";

    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete"];

    private readonly ApiFactory _factory;

    public OpenApiContractTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SwaggerDocument_MatchesTheCommittedContract()
    {
        var actual = await FetchCanonicalAsync();
        var snapshotPath = Path.Combine(FindRepoRoot(), SnapshotRelativePath);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, actual + "\n");
            return;
        }

        Assert.True(
            File.Exists(snapshotPath),
            $"لقطة OpenAPI مفقودة: {SnapshotRelativePath} — ولّدها بـ{UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(snapshotPath).TrimEnd('\n'), actual);
    }

    [Fact]
    public async Task SwaggerDocument_HoldsUniqueOperationIdsAndDeclaredPathParameters()
    {
        var root = JsonNode.Parse(await FetchRawAsync())!;
        var paths = root["paths"]?.AsObject();
        Assert.NotNull(paths);
        Assert.NotEmpty(paths);

        var operationIds = new HashSet<string>(StringComparer.Ordinal);
        var operationCount = 0;

        foreach (var (path, item) in paths)
        {
            if (item is not JsonObject pathItem) continue;

            var pathLevelParameters = ParameterNames(pathItem);
            var declaredInPath = Regex.Matches(path, @"\{(?<name>[^}]+)\}")
                .Select(match => match.Groups["name"].Value)
                .ToArray();

            foreach (var method in HttpMethods)
            {
                if (pathItem[method] is not JsonObject operation) continue;
                operationCount++;

                var operationId = operation["operationId"]?.GetValue<string>();
                Assert.False(
                    string.IsNullOrWhiteSpace(operationId),
                    $"{method.ToUpperInvariant()} {path}: عملية بلا operationId.");
                Assert.True(
                    operationIds.Add(operationId!),
                    $"operationId مكرر: {operationId} ({method.ToUpperInvariant()} {path}).");

                var declared = pathLevelParameters.Concat(ParameterNames(operation)).ToHashSet(StringComparer.Ordinal);
                foreach (var name in declaredInPath)
                {
                    Assert.True(
                        declared.Contains(name),
                        $"{method.ToUpperInvariant()} {path}: معامل المسار '{name}' غير معلن في العملية.");
                }
            }
        }

        Assert.True(operationCount >= 100, $"عدد العمليات في العقد غير متوقع: {operationCount}.");
        Assert.NotNull(root["components"]?["securitySchemes"]?["Bearer"]);

        // P4-04: الأمان لكل عملية — الدخول مجهول، ولوحة التحكم تحتاج Bearer.
        var anonymousLogin = root["paths"]?["/api/auth/login"]?["post"]?["security"]?.AsArray();
        Assert.True(anonymousLogin is null || anonymousLogin.Count == 0);
        var authenticatedDashboard = root["paths"]?["/api/dashboard"]?["get"]?["security"]?.AsArray();
        Assert.NotNull(authenticatedDashboard);
        Assert.NotEmpty(authenticatedDashboard);

        // P6-06: الفلتر يشترط الحقول غير القابلة للفراغ (أنواع القيم وسجلات record).
        // غياب `required` هنا يعني رجوع كل الأنواع المولَّدة إلى «اختيارية».
        var dashboardStatsRequired = root["components"]?["schemas"]?["DashboardStatsDto"]?["required"]?.AsArray();
        Assert.NotNull(dashboardStatsRequired);
        Assert.Equal(8, dashboardStatsRequired.Count);
    }

    private async Task<string> FetchRawAsync()
    {
        var client = _factory.CreateClient();
        return await client.GetStringAsync("/swagger/v1/swagger.json");
    }

    private async Task<string> FetchCanonicalAsync()
        => (Canonical(JsonNode.Parse(await FetchRawAsync()))
                ?? throw new InvalidOperationException("Swagger document is empty."))
            .ToJsonString(new JsonSerializerOptions { WriteIndented = true })
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    /// <summary>ترتيب مفاتيح كل كائن متداخل — لقطة مستقرة بين المنصّات وترتيب التوليد.</summary>
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => KeyValuePair.Create(pair.Key, Canonical(pair.Value)))),
        JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
        _ => node?.DeepClone(),
    };

    private static IEnumerable<string> ParameterNames(JsonObject owner)
    {
        if (owner["parameters"] is not JsonArray parameters)
            return [];

        return parameters
            .Select(parameter => parameter?["name"]?.GetValue<string>())
            .OfType<string>()
            .Where(name => !string.IsNullOrWhiteSpace(name));
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
