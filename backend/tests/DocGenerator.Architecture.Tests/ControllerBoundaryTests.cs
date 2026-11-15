using System.Reflection;
using DocGenerator.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using NetArchTest.Rules;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P1-06 — المتحكمات لا تلامس قاعدة البيانات مباشرة.
/// <para>
/// القاعدة تغطي <c>DbContext</c> وكل واجهات EF Core (<c>DbSet</c>، <c>Include</c>،
/// <c>AsNoTracking</c>…) لأن نطاق التحقق هو مساحة الأسماء كاملة، وتغطي كذلك
/// <c>DocGenerator.Infrastructure</c> حيث تسكن السياقات الملموسة، فلا يكفي تمرير نوع
/// مشتق للالتفاف على حظر EF.
/// </para>
/// </summary>
public class ControllerBoundaryTests
{
    private static readonly Assembly ApiAssembly = typeof(DocumentsController).Assembly;

    [Fact]
    public void Controllers_DoNotDependOn_DbContextOrEfCoreOrInfrastructure()
    {
        Assert.True(SelectControllers().Count > 0, "لم يُعثر على أي متحكم يرث ControllerBase في تجميعة Api.");

        var result = Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "DocGenerator.Infrastructure")
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(result));
    }

    [Fact]
    public void ControllerSelection_CoversEveryControllerBaseSubclass()
    {
        // حارس ضد التفاف صامت: لو صار اختيار NetArchTest غير شامل (مثلًا متحكم يرث
        // قاعدة وسيطة)، تصبح قاعدة DbContext أعلاه معطّلة جزئيًا بلا أي إشعار.
        var expected = ApiAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var selected = SelectControllers()
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, selected);
    }

    [Fact]
    public void EveryControllerType_InheritsControllerBase()
    {
        var controllerLikeTypes = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .GetTypes()
            .ToList();

        Assert.NotEmpty(controllerLikeTypes);

        var offenders = controllerLikeTypes
            .Where(type => !typeof(ControllerBase).IsAssignableFrom(type))
            .Select(type => type.FullName ?? type.Name)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "كل نوع اسمه ينتهي بـ Controller يجب أن يرث ControllerBase حتى تدخل عليه حراس المعمارية."
            + Environment.NewLine
            + string.Join(", ", offenders));
    }

    private static IReadOnlyList<Type> SelectControllers()
        => Types.InAssembly(ApiAssembly)
            .That()
            .Inherit(typeof(ControllerBase))
            .GetTypes()
            .ToList();

    private static string BuildFailureMessage(TestResult result)
    {
        var failingTypes = result.FailingTypeNames?.ToList() ?? [];
        var details = failingTypes.Count > 0
            ? string.Join(", ", failingTypes)
            : "(لم تُرجع المكتبة أسماء الأنواع المخالفة)";

        return "المتحكمات يجب أن تمر عبر خدمات Application، لا عبر DbContext/EF Core/Infrastructure."
            + Environment.NewLine
            + $"الأنواع المخالفة: {details}";
    }
}
