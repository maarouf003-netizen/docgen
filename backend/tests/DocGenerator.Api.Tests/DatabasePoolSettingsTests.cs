using DocGenerator.Application.Common;
using DocGenerator.Infrastructure.Persistence;
using Npgsql;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P3-09`: قيم مجمّع الاتصالات تُطبَّق كافتراضات لا تمسّ تصريح المشغّل، والحدود غير
/// الصالحة ترفض الإقلاع بدل المرور إلى Npgsql بسلوك غامض.
/// </summary>
[Trait("Category", "Unit")]
public class DatabasePoolSettingsTests
{
    [Fact]
    public void ApplyPoolDefaults_AddsEveryLimit()
    {
        var result = PostgresConnectionString.ApplyPoolDefaults(
            "Host=h;Port=5432;Database=d;Username=u;Password=p;",
            new DatabasePoolOptions());

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(20, builder.MaxPoolSize);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(15, builder.Timeout);
        Assert.Equal(30, builder.CommandTimeout);
        Assert.Equal(300, builder.ConnectionIdleLifetime);
    }

    [Fact]
    public void ApplyPoolDefaults_PreservesExplicitConnectionStringValues()
    {
        const string explicitValues =
            "Host=h;Database=d;Maximum Pool Size=5;Minimum Pool Size=2;Timeout=3;"
            + "Command Timeout=90;Connection Idle Lifetime=60;";

        var result = PostgresConnectionString.ApplyPoolDefaults(
            explicitValues,
            new DatabasePoolOptions { MaxPoolSize = 50, MinPoolSize = 10, ConnectionTimeoutSeconds = 40 });

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(5, builder.MaxPoolSize);
        Assert.Equal(2, builder.MinPoolSize);
        Assert.Equal(3, builder.Timeout);
        Assert.Equal(90, builder.CommandTimeout);
        Assert.Equal(60, builder.ConnectionIdleLifetime);
    }

    [Fact]
    public void ApplyPoolDefaults_CustomOptionsBecomeTheDefaults()
    {
        var result = PostgresConnectionString.ApplyPoolDefaults(
            "Host=h;Database=d;",
            new DatabasePoolOptions
            {
                MaxPoolSize = 7,
                MinPoolSize = 1,
                ConnectionTimeoutSeconds = 5,
                CommandTimeoutSeconds = 45,
                IdleLifetimeSeconds = 120,
            });

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(7, builder.MaxPoolSize);
        Assert.Equal(1, builder.MinPoolSize);
        Assert.Equal(5, builder.Timeout);
        Assert.Equal(45, builder.CommandTimeout);
        Assert.Equal(120, builder.ConnectionIdleLifetime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyPoolDefaults_EmptyConnectionString_IsUnchanged(string? connectionString)
        => Assert.Equal(connectionString, PostgresConnectionString.ApplyPoolDefaults(connectionString!, new DatabasePoolOptions()));

    [Fact]
    public void ApplyPoolDefaults_QuotedPasswordIsNotMistakenForAKey()
    {
        // `;Maximum Pool Size=` داخل قيمة مقتبسة ليس مفتاحًا؛ الافتراضي يجب أن يُطبَّق.
        const string connectionString = "Host=h;Database=d;Password=\"p;Maximum Pool Size=1\";";

        var result = PostgresConnectionString.ApplyPoolDefaults(connectionString, new DatabasePoolOptions { MaxPoolSize = 9 });

        Assert.Equal(9, new NpgsqlConnectionStringBuilder(result).MaxPoolSize);
    }

    [Fact]
    public void ApplyPoolDefaults_AppliesToNormalizedUri()
    {
        var normalized = PostgresConnectionString.Normalize("postgres://u:p@h:5432/db?sslmode=require");

        var result = PostgresConnectionString.ApplyPoolDefaults(normalized, new DatabasePoolOptions { MaxPoolSize = 12 });

        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(12, builder.MaxPoolSize);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal("h", builder.Host);
    }

    [Theory]
    [InlineData(0, "MaxPoolSize")]
    [InlineData(201, "MaxPoolSize")]
    public void ApplyPoolDefaults_InvalidMaxPoolSize_Refuses(int value, string key)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionString.ApplyPoolDefaults(
                "Host=h;Database=d;", new DatabasePoolOptions { MaxPoolSize = value }));

        Assert.Contains($"Database:Pool:{key}", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_MinPoolSizeAboveMax_Refuses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabasePoolOptions { MaxPoolSize = 5, MinPoolSize = 6 }.Validate());

        Assert.Contains("Database:Pool:MinPoolSize", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void Validate_ConnectionTimeoutOutOfRange_Refuses(int value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabasePoolOptions { ConnectionTimeoutSeconds = value }.Validate());

        Assert.Contains("Database:Pool:ConnectionTimeoutSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_CommandTimeoutOutOfRange_Refuses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabasePoolOptions { CommandTimeoutSeconds = 601 }.Validate());

        Assert.Contains("Database:Pool:CommandTimeoutSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_IdleLifetimeOutOfRange_Refuses()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabasePoolOptions { IdleLifetimeSeconds = 3601 }.Validate());

        Assert.Contains("Database:Pool:IdleLifetimeSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Validate_HealthCheckTimeoutOutOfRange_Refuses(int value)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabasePoolOptions { HealthCheckTimeoutSeconds = value }.Validate());

        Assert.Contains("Database:Pool:HealthCheckTimeoutSeconds", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseOptions_Validate_DelegatesToPool()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DatabaseOptions { Pool = new DatabasePoolOptions { MaxPoolSize = 0 } }.Validate());

        Assert.Contains("Database:Pool:MaxPoolSize", exception.Message, StringComparison.Ordinal);
    }
}
