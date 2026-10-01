namespace DocGenerator.Api.Tests;

/// <summary>
/// اختبار إظهار RF-006 (SEC-005): مصنع التصميم يرفض العمل بلا اتصال صريح —
/// لا اعتماد احتياطي ملتزم.
/// </summary>
public class RF006PostgresFactoryTests
{
    [Fact]
    public void CreateDbContext_WithoutEnv_ThrowsHelpfulError()
    {
        var previous = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION");
        try
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION", null);
            var factory = new PostgresDbContextFactory();
            var error = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext(Array.Empty<string>()));
            Assert.Contains("POSTGRES_CONNECTION", error.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("POSTGRES_CONNECTION", previous);
        }
    }
}
