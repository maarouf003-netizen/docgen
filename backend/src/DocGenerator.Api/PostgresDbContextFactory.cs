using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocGenerator.Api;

/// <summary>
/// مصنع تصميم-وقت لتوليد هجرات Postgres المنفصلة عن هجرات SQLite:
/// dotnet ef migrations add &lt;Name&gt; --context DocGeneratorPostgresDbContext
/// </summary>
public class PostgresDbContextFactory : IDesignTimeDbContextFactory<DocGeneratorPostgresDbContext>
{
    public DocGeneratorPostgresDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=docgen;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new DocGeneratorPostgresDbContext(options);
    }
}
