using DocGenerator.Api.Tests.TestHelpers;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P0-04 على PostgreSQL الحقيقي: الحجز الفريد وإعادة الحالة/النوع/الموقع
/// والسباق المتزامن — سلوك يعتمد على قيد فريد ومزوّد حقيقي، لا يكفي فيه SQLite.
/// يعمل فقط عند تعريف <c>DOCGEN_TEST_POSTGRES</c> (وفي CI يفشل صراحةً عند غيابه).
/// </summary>
public sealed class IdempotencyStorePostgresTests
{
    [Fact]
    public async Task BeginCompleteReplay_RoundTripsStatusAndLocation()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_idem_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var db = new DocGeneratorPostgresDbContext(options);
            await db.Database.MigrateAsync();
            var store = new IdempotencyStore(db);

            var first = await store.BeginAsync(
                "pg-key", "POST api/things", 7, "fp", DateTime.UtcNow.AddMinutes(5));
            Assert.NotNull(first.Ticket);

            await store.CompleteAsync(
                first.Ticket!, "{\"id\":1}", 201, "application/json; charset=utf-8", "/api/things/1");

            var replay = await store.BeginAsync(
                "pg-key", "POST api/things", 7, "fp", DateTime.UtcNow.AddMinutes(5));

            Assert.Null(replay.Ticket);
            Assert.Equal(201, replay.ReplayStatusCode);
            Assert.Equal("application/json; charset=utf-8", replay.ReplayContentType);
            Assert.Equal("/api/things/1", replay.ReplayLocation);
            Assert.Equal("{\"id\":1}", replay.ReplayBody);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }

    [Fact]
    public async Task ConcurrentBegin_SameKey_YieldsSingleTicket()
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip();
        if (baseConnectionString is null)
            return;

        var databaseName = "docgen_idem_" + Guid.NewGuid().ToString("N")[..12];
        var connectionString = await PostgresTestDatabase.CreateAsync(baseConnectionString, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
                .UseNpgsql(connectionString)
                .Options;
            await using var setupDb = new DocGeneratorPostgresDbContext(options);
            await setupDb.Database.MigrateAsync();

            await using var db1 = new DocGeneratorPostgresDbContext(options);
            await using var db2 = new DocGeneratorPostgresDbContext(options);
            var store1 = new IdempotencyStore(db1);
            var store2 = new IdempotencyStore(db2);
            var expires = DateTime.UtcNow.AddMinutes(5);

            var results = await Task.WhenAll(
                store1.BeginAsync("pg-race", "POST api/things", 7, "fp", expires),
                store2.BeginAsync("pg-race", "POST api/things", 7, "fp", expires));

            Assert.Equal(1, results.Count(r => r.Ticket is not null));
            Assert.Contains(results, r => r.Busy);
        }
        finally
        {
            await PostgresTestDatabase.DropAsync(baseConnectionString, databaseName);
        }
    }
}
