using System.Reflection;
using System.Text.Json;
using DocGenerator.Api.Controllers;
using DocGenerator.Api.Security;
using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P2-08`: كل مسار مكلف (تصدير أو استيراد جماعي) محمي بسياسة <c>expensive</c>.
/// الحارس انعكاسي لا نصّي: أي إجراء جديد يحمل <see cref="SingleExportPerUserAttribute"/>
/// بلا حد معدل يُفشل الاختبار، وكذلك استيراد الجهات (نقطتان ثقيلتان بلا تصدير).
/// </summary>
[Trait("Category", "Unit")]
public class RateLimitCoverageTests
{
    [Fact]
    public void EveryConcurrentExportAction_CarriesTheExpensivePolicy()
    {
        var offenders = new List<string>();

        foreach (var controller in ControllerTypes())
        {
            foreach (var method in controller.GetMethods(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (method.GetCustomAttribute<SingleExportPerUserAttribute>() is null)
                    continue;

                if (PolicyOf(controller, method) != RateLimitingSetup.ExpensivePolicy)
                    offenders.Add($"{controller.Name}.{method.Name}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "تصدير بلا سياسة expensive (P2-08): " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData(typeof(EntityRegistryController), nameof(EntityRegistryController.ImportPreview))]
    [InlineData(typeof(EntityRegistryController), nameof(EntityRegistryController.ImportCommit))]
    public void BulkImportActions_CarryTheExpensivePolicy(Type controller, string actionName)
    {
        var method = controller.GetMethod(actionName);

        Assert.NotNull(method);
        Assert.Equal(RateLimitingSetup.ExpensivePolicy, PolicyOf(controller, method!));
    }

    [Fact]
    public void StatisticsReads_CarryTheStatsPolicy_NotTheExpensiveOne()
    {
        var policy = typeof(StatisticsController)
            .GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

        Assert.Equal(RateLimitingSetup.StatsReadPolicy, policy);
        Assert.True(new DocGenerator.Application.Common.RateLimitOptions().StatsReadPerMinute >= 60);
    }

    /// <summary>
    /// `W5-F-d`: قراءات دوائر التنفيذ على سياسة قراءة مستقلة، والكتابة/التصدير على
    /// «المكلفة» صراحةً. انعكاسي حتى يشمل كل إجراء جديد تلقائيًا (لا نص يدوي يتقادم).
    /// </summary>
    [Fact]
    public void ExecutionCircuitReads_CarryCircuitsRead_AndWritesStayExpensive()
    {
        var offenders = new List<string>();
        var reads = 0;

        foreach (var method in typeof(ExecutionCircuitsController).GetMethods(
                     BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var isGet = method.GetCustomAttribute<HttpGetAttribute>() is not null;
            var isWrite = method.GetCustomAttribute<HttpPostAttribute>() is not null
                || method.GetCustomAttribute<HttpPutAttribute>() is not null
                || method.GetCustomAttribute<HttpDeleteAttribute>() is not null;
            if (!isGet && !isWrite)
                continue;

            var isExport = method.GetCustomAttribute<SingleExportPerUserAttribute>() is not null;
            var expected = isExport || isWrite
                ? RateLimitingSetup.ExpensivePolicy
                : RateLimitingSetup.CircuitsReadPolicy;
            if (!isExport && isGet)
                reads += 1;

            var actual = PolicyOf(typeof(ExecutionCircuitsController), method);
            if (actual != expected)
                offenders.Add($"{method.Name}: {actual ?? "(none)"} ≠ {expected}");
        }

        // لا يمرّ الاختبار بصمت إن اختفت الإجراءات من الانعكاس.
        Assert.True(reads >= 5, $"قراءات دوائر التنفيذ المكتشفة {reads} < 5.");
        Assert.True(
            offenders.Count == 0,
            "سياسة معدل دوائر التنفيذ مخالفة (W5-F-d): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// `W5-F-e`: القيم الفعلية في `appsettings.json` (لا الافتراضية في الكلاس) — أعلى من
    /// «المكلفة» بحد أدنى معقول، وكل مفتاح في العقد موجود في الملف (لا افتراضي صامت).
    /// </summary>
    [Fact]
    public void AppSettings_ReadLimitsAreConfiguredAboveExpensive_AndContractKeysPresent()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(FindApiAppSettings()),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var section = document.RootElement.GetProperty("RateLimiting");

        var expensive = ConfiguredValue(section, nameof(RateLimitOptions.ExpensivePerMinute));
        var stats = ConfiguredValue(section, nameof(RateLimitOptions.StatsReadPerMinute));
        var circuits = ConfiguredValue(section, nameof(RateLimitOptions.CircuitsReadPerMinute));

        Assert.True(stats >= 60, $"RateLimiting:StatsReadPerMinute={stats} أقل من 60 في appsettings.");
        Assert.True(circuits >= 30, $"RateLimiting:CircuitsReadPerMinute={circuits} أقل من 30 في appsettings.");
        Assert.True(stats > expensive, "StatsReadPerMinute يجب أن يتجاوز ExpensivePerMinute.");
        Assert.True(circuits > expensive, "CircuitsReadPerMinute يجب أن يتجاوز ExpensivePerMinute.");

        foreach (var property in typeof(RateLimitOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.True(
                section.TryGetProperty(property.Name, out _),
                $"RateLimiting:{property.Name} مفقود من appsettings (سيعمل بالافتراضي بصمت).");
        }
    }

    private static int ConfiguredValue(JsonElement section, string name)
    {
        Assert.True(section.TryGetProperty(name, out var value), $"RateLimiting:{name} مفقود من appsettings.");
        return value.GetInt32();
    }

    private static string FindApiAppSettings()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "src", "DocGenerator.Api", "appsettings.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "appsettings.json غير موجود فوق مجلد مخرجات الاختبار — تعذّر التحقق من قيم المعدل الفعلية.");
    }

    private static string? PolicyOf(Type controller, MethodInfo method)
        => method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
           ?? controller.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

    private static IEnumerable<Type> ControllerTypes()
        => typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));
}
