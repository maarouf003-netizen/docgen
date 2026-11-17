using System.Reflection;
using DocGenerator.Api.Controllers;
using DocGenerator.Api.Security;
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

    private static string? PolicyOf(Type controller, MethodInfo method)
        => method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName
           ?? controller.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName;

    private static IEnumerable<Type> ControllerTypes()
        => typeof(Program).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type));
}
