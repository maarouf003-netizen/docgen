using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class TransactionRunnerTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly TransactionRunner _tx;

    public TransactionRunnerTests()
    {
        _db = TestDb.Create();
        _tx = new TransactionRunner(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RunAsync_OnSuccess_CommitsAllInnerSaves()
    {
        await _tx.RunAsync(async token =>
        {
            _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
            await _db.SaveChangesAsync(token);
            _db.AuditLogs.Add(new AuditLog { UserName = "u", ActionType = "test" });
            await _db.SaveChangesAsync(token);
        });

        Assert.Equal(1, _db.Branches.Count());
        Assert.Equal(1, _db.AuditLogs.Count());
    }

    [Fact]
    public async Task RunAsync_WhenInnerThrows_RollsBackBothSaves()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _tx.RunAsync(async token =>
            {
                _db.Branches.Add(new Branch { Name = "حلب", Code = "ALP" });
                await _db.SaveChangesAsync(token);
                _db.AuditLogs.Add(new AuditLog { UserName = "u", ActionType = "test" });
                await _db.SaveChangesAsync(token);
                throw new InvalidOperationException("boom");
            }));

        _db.ChangeTracker.Clear();
        Assert.Equal(0, _db.Branches.Count());
        Assert.Equal(0, _db.AuditLogs.Count());
    }

    [Fact]
    public async Task RunAsync_Generic_ReturnsResultOnSuccess()
    {
        var id = await _tx.RunAsync(async token =>
        {
            var branch = new Branch { Name = "درعا", Code = "DAR" };
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync(token);
            return branch.Id;
        });

        Assert.True(id > 0);
        Assert.Equal(1, _db.Branches.Count());
    }

    [Fact]
    public async Task RunAsync_Generic_WhenInnerThrows_RollsBack()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _tx.RunAsync<int>(async token =>
            {
                _db.Branches.Add(new Branch { Name = "حمص", Code = "HMS" });
                await _db.SaveChangesAsync(token);
                throw new InvalidOperationException("boom");
            }));

        _db.ChangeTracker.Clear();
        Assert.Equal(0, _db.Branches.Count());
    }

    [Fact]
    public async Task RunAsync_OnSuccess_ResultIsCommittedAndVisible()
    {
        var branch = new Branch { Name = "اللاذقية", Code = "LAT" };
        await _tx.RunAsync(async token =>
        {
            _db.Branches.Add(branch);
            await _db.SaveChangesAsync(token);
        });

        Assert.True(branch.Id > 0);
        var reloaded = await _db.Branches.AsNoTracking().SingleAsync(b => b.Code == "LAT");
        Assert.Equal("اللاذقية", reloaded.Name);
    }
}
