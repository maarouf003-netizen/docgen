using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة صندوق الصادر (الخطة §6.4 و§14.1): حارس المعاملة، وشكل الصف، ودورية الحمولة،
/// وتسلسل الإلام والمعالجة.
/// </summary>
public class HeadAlertOutboxTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly ServiceProvider _provider;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _branchId;
    private readonly User _head;
    private readonly User _lawyer;

    public HeadAlertOutboxTests()
    {
        _db = TestDb.Create();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;

        _head = EntityBuilders.NewUser("head_x", "رئيس القسم", UserRole.Head, _branchId);
        _lawyer = EntityBuilders.NewUser("law1", "محامي", UserRole.Lawyer, _branchId);
        _db.Users.AddRange(_head, _lawyer);
        _db.SaveChanges();

        _provider = BuildProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_db);
        services.AddScoped<ITransactionRunner>(sp => new TransactionRunner(sp.GetRequiredService<DocGeneratorDbContext>()));
        services.AddScoped<ITransactionContext>(sp => (ITransactionContext)sp.GetRequiredService<ITransactionRunner>());
        services.AddScoped<IHeadAlertOutboxStore, HeadAlertOutboxStore>();
        services.AddScoped<IHeadAlertOutbox, HeadAlertOutbox>();
        services.AddScoped<IHeadAlertService>(sp => new HeadAlertService(
            new HeadAlertRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new DocumentRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new UserRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new Repository<Branch>(sp.GetRequiredService<DocGeneratorDbContext>()),
            new UnitOfWork(sp.GetRequiredService<DocGeneratorDbContext>()),
            sp.GetRequiredService<ITransactionRunner>(),
            _audit,
            sp.GetRequiredService<ITransactionContext>()));

        return services.BuildServiceProvider();
    }

    private IHeadAlertOutbox Outbox() => _provider.GetRequiredService<IHeadAlertOutbox>();

    private IHeadAlertOutboxStore Store() => _provider.GetRequiredService<IHeadAlertOutboxStore>();

    private ITransactionRunner Tx() => _provider.GetRequiredService<ITransactionRunner>();

    private CreateSystemHeadAlertRequest Request(
        HeadAlertAudience audience = HeadAlertAudience.SingleHead,
        int? recipientUserId = null,
        string kind = HeadAlertKindCatalog.DelegationPendingApproval)
        => new(
            kind,
            "رسالة نظامية",
            audience,
            _branchId,
            _head.Id,
            RecipientUserId: recipientUserId ?? _head.Id,
            DelegationId: 7);

    [Fact]
    public async Task EnqueueSystemAsync_WithoutActiveTransaction_ThrowsAndInsertsNothing()
    {
        var outbox = Outbox();

        await Assert.ThrowsAsync<InvalidOperationException>(() => outbox.EnqueueSystemAsync(
            Request(), null, DateTime.UtcNow));

        Assert.Empty(await _db.HeadAlertOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task EnqueueSystemAsync_InsideTransaction_CreatesOnePendingRow()
    {
        var outbox = Outbox();
        var occurredAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);

        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueSystemAsync(Request(), null, occurredAt, token);
            await _db.SaveChangesAsync(token);
        });

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();

        Assert.Equal(HeadAlertOutboxStatus.Pending, message.Status);
        Assert.Equal(HeadAlertOutboxOperation.Upsert, message.Operation);
        Assert.Equal(HeadAlertKindCatalog.DelegationPendingApproval, message.Kind);
        Assert.Equal(1, message.PayloadVersion);
        Assert.Equal(occurredAt, message.OccurredAt);
        Assert.Equal(0, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(_head.Id, message.TargetUserId);
    }

    [Fact]
    public async Task EnqueueSystemAsync_BroadcastAudience_HasNoTargetUser()
    {
        var outbox = Outbox();
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.EntityRenameLawyers,
            "تعميم",
            HeadAlertAudience.BranchLawyers,
            _branchId,
            _head.Id);

        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueSystemAsync(request, null, DateTime.UtcNow, token);
            await _db.SaveChangesAsync(token);
        });

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Null(message.TargetUserId);
    }

    [Fact]
    public async Task EnqueueSystemAsync_RejectsInvalidKindAudienceAndCreator()
    {
        var outbox = Outbox();

        var badKind = Request() with { Kind = HeadAlertKindCatalog.ManualToBranch };
        var badAudience = Request(audience: HeadAlertAudience.BranchLawyers);
        var badCreator = Request() with { CreatedById = 0 };

        await Tx().RunAsync(async token =>
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                outbox.EnqueueSystemAsync(badKind, null, DateTime.UtcNow, token));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                outbox.EnqueueSystemAsync(badAudience, null, DateTime.UtcNow, token));
            await Assert.ThrowsAsync<ArgumentException>(() =>
                outbox.EnqueueSystemAsync(badCreator, null, DateTime.UtcNow, token));
        });

        Assert.Empty(await _db.HeadAlertOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task EnqueueSystemAsync_RejectsSupersedePlanWithoutScopeOrKinds()
    {
        var outbox = Outbox();
        var plan = new HeadAlertSupersedePlan(null, null, new HashSet<string>(), "سبب");

        await Tx().RunAsync(async token =>
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                outbox.EnqueueSystemAsync(Request(), plan, DateTime.UtcNow, token));
        });

        Assert.Empty(await _db.HeadAlertOutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task EnqueueSystemAsync_PayloadRoundTripsCreatorAndAudience()
    {
        var outbox = Outbox();
        var request = Request(audience: HeadAlertAudience.SingleLawyer, recipientUserId: _lawyer.Id, kind: HeadAlertKindCatalog.DelegationMirrorUpdated);

        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueSystemAsync(request, null, DateTime.UtcNow, token);
            await _db.SaveChangesAsync(token);
        });

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);

        Assert.Equal("upsert", payload.Operation);
        Assert.NotNull(payload.Request);
        Assert.Equal(_head.Id, payload.Request!.CreatedById);
        Assert.Equal(HeadAlertAudience.SingleLawyer, payload.Request.Audience);
        Assert.Equal(_lawyer.Id, payload.Request.RecipientUserId);
        Assert.Equal(HeadAlertKindCatalog.DelegationMirrorUpdated, payload.Request.Kind);
    }

    [Fact]
    public async Task EnqueueCircuitRenamedAsync_ProducesOperationWithoutKind()
    {
        var outbox = Outbox();

        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueCircuitRenamedAsync(9, "أُعيدت تسمية الدائرة", DateTime.UtcNow, token);
            await _db.SaveChangesAsync(token);
        });

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxOperation.Circuit_Renamed, message.Operation);
        Assert.Null(message.Kind);
        Assert.Null(message.TargetUserId);

        var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
        Assert.Equal("circuit_renamed", payload.Operation);
        Assert.Equal(9, payload.CircuitId);
        Assert.Equal("أُعيدت تسمية الدائرة", payload.Message);
    }

    [Fact]
    public async Task Enqueue_SharesTheCallersTransaction_SoFailureRollsBackTogether()
    {
        var outbox = Outbox();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Tx().RunAsync(async token =>
            {
                await outbox.EnqueueSystemAsync(Request(), null, DateTime.UtcNow, token);
                await _db.SaveChangesAsync(token);
                throw new InvalidOperationException("فشل العمل بعد التسجيل");
            }));

        // الرسالة لم تُحفظ: التراجع شملها (ذرّية واحدة).
        Assert.Empty(await _db.HeadAlertOutboxMessages.ToListAsync());
    }

    private static HeadAlertOutboxMessage Terminal(
        HeadAlertOutboxStatus status,
        DateTime createdAt,
        DateTime processedAt)
        => new()
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = status,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = "{}",
            PayloadVersion = 1,
            OccurredAt = createdAt,
            CreatedAt = createdAt,
            NextAttemptAt = createdAt,
            ProcessedAt = status is HeadAlertOutboxStatus.Processed or HeadAlertOutboxStatus.Skipped ? processedAt : null,
        };

    [Fact]
    public async Task CleanupAsync_DeletesOnlyExpiredProcessedAndSkipped()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var expiredProcessed = Terminal(HeadAlertOutboxStatus.Processed, now.AddDays(-9), now.AddDays(-8));
        var expiredSkipped = Terminal(HeadAlertOutboxStatus.Skipped, now.AddDays(-40), now.AddDays(-31));
        var recentProcessed = Terminal(HeadAlertOutboxStatus.Processed, now.AddDays(-2), now.AddDays(-1));
        var recentSkipped = Terminal(HeadAlertOutboxStatus.Skipped, now.AddDays(-10), now.AddDays(-5));
        var pending = Terminal(HeadAlertOutboxStatus.Pending, now.AddDays(-30), default);
        var failed = Terminal(HeadAlertOutboxStatus.Failed, now.AddDays(-30), default);

        _db.HeadAlertOutboxMessages.AddRange(
            expiredProcessed, expiredSkipped, recentProcessed, recentSkipped, pending, failed);
        await _db.SaveChangesAsync();

        var removed = await Store().CleanupAsync(now, processedRetentionDays: 7, skippedRetentionDays: 30);

        Assert.Equal(2, removed);
        var remaining = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(
            new[] { recentProcessed.Id, recentSkipped.Id, pending.Id, failed.Id },
            remaining.Select(m => m.Id));
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsPendingCountOldestAndFailedCount()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var oldest = Terminal(HeadAlertOutboxStatus.Pending, now.AddMinutes(-10), default);
        var newer = Terminal(HeadAlertOutboxStatus.Pending, now.AddMinutes(-1), default);
        var processed = Terminal(HeadAlertOutboxStatus.Processed, now.AddDays(-1), now.AddDays(-1));
        var failed = Terminal(HeadAlertOutboxStatus.Failed, now.AddDays(-1), default);

        _db.HeadAlertOutboxMessages.AddRange(oldest, newer, processed, failed);
        await _db.SaveChangesAsync();

        var health = await Store().GetHealthAsync();

        Assert.Equal(2, health.PendingCount);
        Assert.Equal(oldest.CreatedAt, health.OldestPendingAt);
        Assert.Equal(1, health.FailedCount);
    }

    [Fact]
    public async Task GetHealthAsync_NoPending_ReturnsNullOldest()
    {
        var health = await Store().GetHealthAsync();

        Assert.Equal(0, health.PendingCount);
        Assert.Null(health.OldestPendingAt);
        Assert.Equal(0, health.FailedCount);
    }
}
