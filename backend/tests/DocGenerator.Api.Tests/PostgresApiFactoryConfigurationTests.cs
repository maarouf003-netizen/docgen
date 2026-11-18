using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// يحرس قرار <see cref="PostgresApiFactory"/>: المخطط يُبنى بهجرات EF الحقيقية
/// (<c>MigrateAsync</c>) لا بـ<c>EnsureCreated</c>، و<c>Respawn</c> يعيد الضبط فعلًا.
/// </summary>
public sealed class PostgresApiFactoryConfigurationTests
    : IClassFixture<PostgresApiFactory>, IAsyncLifetime
{
    private readonly PostgresApiFactory _factory;

    public PostgresApiFactoryConfigurationTests(PostgresApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [PostgresFact]
    public async Task UsesRealMigrations_AndSeededBranches()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.True(options.AutoMigrate, "PostgresApiFactory يجب أن يطبّق الهجرات لا EnsureCreated.");

        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(
            await db.Database.GetAppliedMigrationsAsync(),
            name => name.Contains("InitialPostgresBaseline", StringComparison.Ordinal));

        Assert.True(
            await db.Branches.AnyAsync(b => b.Code == "DAM"),
            "بذر التطوير (DbSeeder) لم يُنشئ الفرع الرئيسي.");
    }

    [PostgresFact]
    public async Task ResetAsync_TruncatesWrites_AndRestoresSeedData()
    {
        const string probeCode = "ZZZ";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            db.Branches.Add(new Branch { Name = "فرع مسبار", Code = probeCode });
            await db.SaveChangesAsync();
            Assert.True(await db.Branches.AnyAsync(b => b.Code == probeCode));
        }

        await _factory.ResetAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocGeneratorDbContext>();
            Assert.False(
                await db.Branches.AnyAsync(b => b.Code == probeCode),
                "Respawn لم يفرّغ الكتابات السابقة.");
            Assert.True(
                await db.Branches.AnyAsync(b => b.Code == "DAM"),
                "إعادة البذر بعد Respawn لم تُعد الفرع الرئيسي.");
            Assert.Contains(
                await db.Database.GetAppliedMigrationsAsync(),
                name => name.Contains("InitialPostgresBaseline", StringComparison.Ordinal));
        }
    }
}
