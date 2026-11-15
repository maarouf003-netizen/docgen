using System.Reflection;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using NetArchTest.Rules;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// P1-06 — حراس حدود الطبقات (`docs/PERFECTION_ROADMAP.md` §`P1-06`).
/// <para>
/// كل قاعدة تُفحص بتجميعة الطبقة نفسها (لا بالاعتماد على المشروع المرجعي)، وتتحقق
/// أولًا من أن المجموعة المفحوصة غير فارغة: قاعدة على مجموعة فارغة تنجح دائمًا،
/// وهي «خضرة كاذبة» تسقط الحارس عند إعادة التسمية/النقل.
/// </para>
/// </summary>
public class LayerBoundaryTests
{
    private static readonly Assembly DomainAssembly = typeof(Document).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(DocumentService).Assembly;
    private static readonly Assembly InfrastructureAssembly =
        typeof(DocGenerator.Infrastructure.Persistence.DocGeneratorDbContext).Assembly;

    [Fact]
    public void Application_DoesNotDependOn_InfrastructureOrApi()
    {
        AssertNoForbiddenDependencies(
            ApplicationAssembly,
            "طبقة Application يجب ألا تعتمد على Infrastructure أو Api.",
            "DocGenerator.Infrastructure",
            "DocGenerator.Api");
    }

    [Fact]
    public void Application_DoesNotDependOn_EfCore()
    {
        // يمنع أي وصول بيانات مباشر (DbContext/DbSet/FromSql...) من الطبقة التطبيقية،
        // ويجعل قاعدة «لا raw SQL في Application» صحيحة بنيويًا لا نصيًا فقط.
        AssertNoForbiddenDependencies(
            ApplicationAssembly,
            "طبقة Application يجب ألا تعتمد على Microsoft.EntityFrameworkCore.",
            "Microsoft.EntityFrameworkCore");
    }

    [Fact]
    public void Domain_DoesNotDependOn_EfCoreOrOuterLayers()
    {
        AssertNoForbiddenDependencies(
            DomainAssembly,
            "طبقة Domain يجب أن تبقى نقية: بلا EF Core ولا Application/Infrastructure/Api.",
            "Microsoft.EntityFrameworkCore",
            "DocGenerator.Application",
            "DocGenerator.Infrastructure",
            "DocGenerator.Api");
    }

    [Fact]
    public void Infrastructure_DoesNotDependOn_Api()
    {
        AssertNoForbiddenDependencies(
            InfrastructureAssembly,
            "طبقة Infrastructure يجب ألا تعتمد على Api.",
            "DocGenerator.Api");
    }

    private static void AssertNoForbiddenDependencies(
        Assembly assembly,
        string description,
        params string[] forbiddenDependencies)
    {
        var inspectedTypes = Types.InAssembly(assembly).GetTypes().ToList();
        Assert.True(
            inspectedTypes.Count > 0,
            $"لم يُعثر على أي نوع في التجميعة '{assembly.GetName().Name}' — لا يمكن أن يكون الفحص صحيحًا.");

        var result = Types.InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAny(forbiddenDependencies)
            .GetResult();

        Assert.True(result.IsSuccessful, BuildFailureMessage(description, result));
    }

    private static string BuildFailureMessage(string description, TestResult result)
    {
        var failingTypes = result.FailingTypeNames?.ToList() ?? [];
        var details = failingTypes.Count > 0
            ? string.Join(", ", failingTypes)
            : "(لم تُرجع المكتبة أسماء الأنواع المخالفة)";

        return $"{description}{Environment.NewLine}الأنواع المخالفة: {details}";
    }
}
