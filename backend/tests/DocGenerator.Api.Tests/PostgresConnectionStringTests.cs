using DocGenerator.Infrastructure.Persistence;
using Npgsql;

namespace DocGenerator.Api.Tests;

public class PostgresConnectionStringTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_WhenEmpty_ReturnsEmpty(string? value, string expected)
    {
        Assert.Equal(expected, PostgresConnectionString.Normalize(value));
    }

    [Fact]
    public void Normalize_WhenKeywordForm_ReturnsUnchanged()
    {
        const string keyword = "Host=localhost;Port=5432;Database=db;Username=u;Password=p;";
        Assert.Equal(keyword, PostgresConnectionString.Normalize(keyword));
    }

    [Fact]
    public void Normalize_ConvertsPostgresUri()
    {
        var result = PostgresConnectionString.Normalize("postgres://user:pass@host:5432/db");
        Assert.Equal("Host=host;Port=5432;Database=db;Username=user;Password=pass;", result);
    }

    [Fact]
    public void Normalize_ConvertsPostgresqlUri_AndDefaultsPort()
    {
        var result = PostgresConnectionString.Normalize("postgresql://u:p@h/db");
        Assert.Equal("Host=h;Port=5432;Database=db;Username=u;Password=p;", result);
    }

    [Fact]
    public void Normalize_IsSchemeCaseInsensitive()
    {
        var result = PostgresConnectionString.Normalize("POSTGRESQL://u:p@h/db");
        Assert.Equal("Host=h;Port=5432;Database=db;Username=u;Password=p;", result);
    }

    [Fact]
    public void Normalize_DecodesUrlEncodedPassword()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p%40ss@h/db");
        Assert.Equal("Host=h;Port=5432;Database=db;Username=u;Password=p@ss;", result);
    }

    [Fact]
    public void Normalize_DecodesUrlEncodedDatabaseName()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@h/my%20db");
        Assert.Equal("Host=h;Port=5432;Database=my db;Username=u;Password=p;", result);
    }

    [Fact]
    public void Normalize_WithoutPassword_OmitsPassword()
    {
        var result = PostgresConnectionString.Normalize("postgres://user@host/db");
        Assert.Equal("Host=host;Port=5432;Database=db;Username=user;", result);
    }

    [Fact]
    public void Normalize_MapsSslModeQueryParameter()
    {
        var result = PostgresConnectionString.Normalize("postgres://u:p@h/db?sslmode=require");
        Assert.Equal("Host=h;Port=5432;Database=db;Username=u;Password=p;SSL Mode=require;", result);
    }

    [Fact]
    public void Normalize_ResultIsParseableByNpgsql()
    {
        var result = PostgresConnectionString.Normalize("postgresql://user:pass@host:5432/db");
        var builder = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("host", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("db", builder.Database);
        Assert.Equal("user", builder.Username);
        Assert.Equal("pass", builder.Password);
    }
}
