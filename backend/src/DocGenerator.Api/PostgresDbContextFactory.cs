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
        // بلا أي اعتماد احتياطي (RF-006/SEC-005): مصنع زمن-التصميم فقط، والاتصال الصريح إلزامي.
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
            ?? throw new InvalidOperationException(
                "POSTGRES_CONNECTION is required to create the design-time Postgres context "
                + "(e.g. $env:POSTGRES_CONNECTION='Host=localhost;Port=5432;Database=docgen;Username=...;Password=...'). "
                + "No default credentials are provided by design.");
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new DocGeneratorPostgresDbContext(options);
    }
}
