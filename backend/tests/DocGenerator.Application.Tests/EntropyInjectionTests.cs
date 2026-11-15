using DocGenerator.Application;
using DocGenerator.Application.Common;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P1-02b — تسجيل عقد العشوائية في الحاوية: مفرد واحد مشترك، والتنفيذ التشغيلي
/// <see cref="SystemEntropySource"/> ما لم يُستبدل صراحةً في بيئة الاختبار/التشغيل.
/// </summary>
public class EntropyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersSingleEntropySource()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IEntropySource>();
        var second = provider.GetRequiredService<IEntropySource>();

        Assert.Same(first, second);
        Assert.IsType<SystemEntropySource>(first);
    }
}
