using DocGenerator.Architecture.Tests.Fixtures;
using NetArchTest.Rules;

namespace DocGenerator.Architecture.Tests;

/// <summary>
/// تحكم إيجابي دائم لمحرك قواعد المعمارية: يثبت أن NetArchTest يرصد الاعتماديات
/// الفعلية (بما فيها داخل أجسام الدوال) قبل الاعتماد على نجاح بقية القواعد.
/// <para>
/// نجاح الحراس الآخرين لا يعني شيئًا إن كان المحرك لا يرى الاعتماديات أصلًا؛
/// وهذا الاختبار يفشل صراحةً في تلك الحالة بدل «خضرة كاذبة».
/// </para>
/// </summary>
public class ArchitectureRuleEngineTests
{
    [Fact]
    public void DependencyRule_DetectsDependencyThatExistsOnlyInsideMethodBody()
    {
        var result = Types.InAssembly(typeof(MemoryStreamOnlyInMethodBody).Assembly)
            .That()
            .HaveNameEndingWith(nameof(MemoryStreamOnlyInMethodBody))
            .ShouldNot()
            .HaveDependencyOn("System.IO")
            .GetResult();

        Assert.False(
            result.IsSuccessful,
            "محرك NetArchTest لم يرصد اعتمادًا موجودًا في جسم دالة — كل حراس الحدود الأخرى غير موثوقة حينها.");

        Assert.Contains(
            result.FailingTypeNames ?? [],
            name => name.EndsWith(nameof(MemoryStreamOnlyInMethodBody), StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyRule_DoesNotFlagUnrelatedNamespace()
    {
        // الضبط المقابل: النوع نفسه لا يُتَّهم بمساحة أسماء لا يستخدمها،
        // فلا يكون «الرصد» ناتجًا عن فشل عام في المحرك.
        var result = Types.InAssembly(typeof(MemoryStreamOnlyInMethodBody).Assembly)
            .That()
            .HaveNameEndingWith(nameof(MemoryStreamOnlyInMethodBody))
            .ShouldNot()
            .HaveDependencyOn("Npgsql")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "المحرك اتّهم النوع باعتماد غير موجود — نتائج القواعد الأخرى ستكون غير دقيقة.");
    }
}
