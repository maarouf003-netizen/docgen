using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class HeadAlertSuccessionTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly TransactionRunner _tx;
    private readonly HeadAlertSuccessionService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _branchId;
    private readonly User _predecessor;
    private readonly User _successor;
    private readonly User _other;

    public HeadAlertSuccessionTests()
    {
        _db = TestDb.Create();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;

        _predecessor = EntityBuilders.NewUser("head_old", "رئيس سابق", UserRole.Head, _branchId);
        _predecessor.IsActive = false;
        _successor = EntityBuilders.NewUser("head_new", "رئيس خلف", UserRole.Head, _branchId);
        _other = EntityBuilders.NewUser("law_other", "محامي", UserRole.Lawyer, _branchId);
        _db.Users.AddRange(_predecessor, _successor, _other);
        _db.SaveChanges();

        _tx = new TransactionRunner(_db);
        _service = new HeadAlertSuccessionService(
            new HeadAlertRepository(_db),
            new HeadAlertOutboxStore(_db),
            new UnitOfWork(_db),
            _audit,
            new DbExceptionClassifier(),
            _tx);
    }

    public void Dispose() => _db.Dispose();

    private static string Key(int delegationId, int recipientId)
        => $"delegation:{delegationId}:recipient:{recipientId}:{HeadAlertKindCatalog.DelegationPendingApproval}";

    private HeadAlert AddAlert(
        string dedupeKey,
        DateTime changedAt,
        bool archived = false,
        int? targetLawyerId = null,
        params (int UserId, bool IsRead, DateTime? ReadAt)[] recipients)
    {
        var alert = new HeadAlert
        {
            BranchId = _branchId,
            CreatedById = _predecessor.Id,
            TargetType = HeadAlertTargetType.Head,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            DedupeKey = dedupeKey,
            Message = demoMessage,
            TargetLawyerId = targetLawyerId,
            CreatedAt = changedAt,
            LastContentChangedAt = changedAt,
            IsArchived = archived,
            ArchivedAt = archived ? changedAt : null,
            Recipients = recipients.Select(r => new HeadAlertRecipient
            {
                UserId = r.UserId,
                IsRead = r.IsRead,
                ReadAt = r.ReadAt,
            }).ToList(),
        };

        _db.HeadAlerts.Add(alert);
        _db.SaveChanges();
        return alert;
    }

    private Task<HeadAlertSuccessionResult> ApplyAsync(CancellationToken ct = default)
        => _tx.RunAsync(token => _service.ApplyAsync(
            _predecessor.Id, _successor.Id, "رئيس سابق", "رئيس خلف", "admin", token), ct);

    private const string demoMessage = "تنبيه معلّق";

    [Fact]
    public async Task ApplyAsync_MovesUnreadRecipient_Rekeys_AndLeavesReadAndArchived()
    {
        var alert = AddAlert(
            Key(7, _predecessor.Id),
            DateTime.UtcNow.AddHours(-2),
            targetLawyerId: _predecessor.Id,
            recipients: [(_predecessor.Id, false, null), (_other.Id, true, DateTime.UtcNow)]);

        var readAlert = AddAlert(
            Key(8, _predecessor.Id),
            DateTime.UtcNow.AddHours(-1),
            recipients: [(_predecessor.Id, true, DateTime.UtcNow)]);

        var archivedAlert = AddAlert(
            Key(9, _predecessor.Id),
            DateTime.UtcNow.AddHours(-1),
            archived: true,
            recipients: [(_predecessor.Id, false, null)]);

        var result = await ApplyAsync();

        Assert.Equal(1, result.MovedRecipients);
        Assert.Equal(0, result.MergedRecipients);
        Assert.Equal(0, result.MergedAlerts);
        Assert.Equal(1, result.RekeyedAlerts);

        var moved = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == alert.Id);
        Assert.Equal(Key(7, _successor.Id), moved.DedupeKey);
        Assert.Equal(_successor.Id, moved.TargetLawyerId);
        Assert.Equal(
            new[] { _other.Id, _successor.Id }.OrderBy(id => id),
            moved.Recipients.Select(r => r.UserId).OrderBy(id => id));

        var untouchedRead = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == readAlert.Id);
        Assert.Equal(_predecessor.Id, Assert.Single(untouchedRead.Recipients).UserId);
        Assert.Equal(Key(8, _predecessor.Id), untouchedRead.DedupeKey);

        var untouchedArchived = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == archivedAlert.Id);
        Assert.True(untouchedArchived.IsArchived);
        Assert.Equal(_predecessor.Id, Assert.Single(untouchedArchived.Recipients).UserId);
        Assert.Equal(Key(9, _predecessor.Id), untouchedArchived.DedupeKey);

        Assert.Contains("alert_recipient_merged", _audit.Actions);
        Assert.Contains("alert_succession", _audit.Actions);
    }

    [Fact]
    public async Task ApplyAsync_MergesDuplicateRecipient_AndPreservesReadState()
    {
        var readAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        var alert = AddAlert(
            Key(7, _predecessor.Id),
            DateTime.UtcNow.AddHours(-2),
            recipients:
            [
                (_predecessor.Id, false, null),
                (_successor.Id, true, readAt),
            ]);

        var result = await ApplyAsync();

        Assert.Equal(0, result.MovedRecipients);
        Assert.Equal(1, result.MergedRecipients);
        Assert.Equal(1, result.RekeyedAlerts);

        var stored = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == alert.Id);
        var recipient = Assert.Single(stored.Recipients);
        Assert.Equal(_successor.Id, recipient.UserId);
        Assert.True(recipient.IsRead);
        Assert.Equal(readAt, recipient.ReadAt);
        Assert.Equal(Key(7, _successor.Id), stored.DedupeKey);
    }

    [Fact]
    public async Task ApplyAsync_DedupeCollision_KeepsNewerHolder_AndArchivesLoser()
    {
        var older = DateTime.UtcNow.AddHours(-2);
        var newer = DateTime.UtcNow.AddHours(-1);

        var loser = AddAlert(
            Key(7, _predecessor.Id),
            older,
            targetLawyerId: _predecessor.Id,
            recipients: [(_predecessor.Id, false, null)]);
        loser.Message = "النسخة الأقدم";
        _db.SaveChanges();

        var holder = AddAlert(
            Key(7, _successor.Id),
            newer,
            targetLawyerId: _successor.Id,
            recipients: [(_successor.Id, false, null)]);
        holder.Message = "النسخة الأحدث";
        _db.SaveChanges();

        var result = await ApplyAsync();

        Assert.Equal(1, result.MergedAlerts);

        var archived = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == loser.Id);
        Assert.True(archived.IsArchived);
        Assert.NotNull(archived.ArchivedAt);
        Assert.Null(archived.DedupeKey);
        Assert.Empty(archived.Recipients);

        var active = await _db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == holder.Id);
        Assert.False(active.IsArchived);
        Assert.Equal("النسخة الأحدث", active.Message);
        Assert.Equal(Key(7, _successor.Id), active.DedupeKey);
        Assert.Equal(_successor.Id, Assert.Single(active.Recipients).UserId);
        Assert.Contains("archive_alert", _audit.Actions);
    }

    [Fact]
    public async Task ApplyAsync_UpdatesPendingOutboxPayload_ButNotProcessed()
    {
        var occurredAt = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة معلّقة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _predecessor.Id,
            DelegationId: 77,
            RecipientUserId: _predecessor.Id);
        var payload = new HeadAlertOutboxPayloadV1
        {
            Operation = "upsert",
            OccurredAt = occurredAt,
            TargetUserId = _predecessor.Id,
            Request = request,
        };

        var pending = new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(payload),
            PayloadVersion = 1,
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            NextAttemptAt = occurredAt,
            TargetUserId = _predecessor.Id,
        };
        var processed = new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Processed,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(payload),
            PayloadVersion = 1,
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            NextAttemptAt = occurredAt,
            ProcessedAt = occurredAt.AddMinutes(1),
            TargetUserId = _predecessor.Id,
        };
        _db.HeadAlertOutboxMessages.AddRange(pending, processed);
        _db.SaveChanges();

        var result = await ApplyAsync();

        Assert.Equal(1, result.UpdatedOutboxMessages);

        var storedPending = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == pending.Id);
        Assert.Equal(_successor.Id, storedPending.TargetUserId);
        var pendingPayload = HeadAlertOutboxPayloadSerializer.Deserialize(storedPending.Payload);
        Assert.Equal(_successor.Id, pendingPayload.TargetUserId);

        // الطلب الأصلي يبقى على السلف: التجاوز يُحمل في TargetUserId وحده حتى
        // يميّز المعالج تجاوز التعاقب عن مستلم صريح غير موجود (الخطة §8.1).
        Assert.Equal(_predecessor.Id, pendingPayload.Request!.RecipientUserId);

        var storedProcessed = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == processed.Id);
        Assert.Equal(_predecessor.Id, storedProcessed.TargetUserId);
        var processedPayload = HeadAlertOutboxPayloadSerializer.Deserialize(storedProcessed.Payload);
        Assert.Equal(_predecessor.Id, processedPayload.TargetUserId);
    }

    [Fact]
    public async Task ApplyAsync_OutboxColumnPayloadMismatch_ForcesBothToSuccessor()
    {
        var occurredAt = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة معلّقة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _predecessor.Id,
            DelegationId: 77,
            RecipientUserId: _predecessor.Id);
        var payload = new HeadAlertOutboxPayloadV1
        {
            Operation = "upsert",
            OccurredAt = occurredAt,
            // حمولة شاذة لا تطابق العمود: العمود هو مرجع الاختيار، وتُفرض المطابقة عند الكتابة.
            TargetUserId = _other.Id,
            Request = request,
        };

        var pending = new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(payload),
            PayloadVersion = 1,
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            NextAttemptAt = occurredAt,
            TargetUserId = _predecessor.Id,
        };
        _db.HeadAlertOutboxMessages.Add(pending);
        _db.SaveChanges();

        var result = await ApplyAsync();

        Assert.Equal(1, result.UpdatedOutboxMessages);

        var stored = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == pending.Id);
        Assert.Equal(_successor.Id, stored.TargetUserId);
        var storedPayload = HeadAlertOutboxPayloadSerializer.Deserialize(stored.Payload);
        Assert.Equal(_successor.Id, storedPayload.TargetUserId);
        Assert.Equal(_predecessor.Id, storedPayload.Request!.RecipientUserId);
    }

    [Fact]
    public async Task ApplyAsync_OutboxMessagesTargetingOthers_AreUntouched()
    {
        var occurredAt = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);

        HeadAlertOutboxMessage NewMessage(int targetUserId) => new()
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(new HeadAlertOutboxPayloadV1
            {
                Operation = "upsert",
                OccurredAt = occurredAt,
                TargetUserId = targetUserId,
                Request = new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.DelegationPendingApproval,
                    "رسالة معلّقة",
                    HeadAlertAudience.SingleLawyer,
                    _branchId,
                    targetUserId,
                    DelegationId: 77,
                    RecipientUserId: targetUserId),
            }),
            PayloadVersion = 1,
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            NextAttemptAt = occurredAt,
            TargetUserId = targetUserId,
        };

        var matching = NewMessage(_predecessor.Id);
        var unrelated = NewMessage(_other.Id);
        _db.HeadAlertOutboxMessages.AddRange(matching, unrelated);
        _db.SaveChanges();

        var result = await ApplyAsync();

        Assert.Equal(1, result.UpdatedOutboxMessages);

        var storedUnrelated = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == unrelated.Id);
        Assert.Equal(_other.Id, storedUnrelated.TargetUserId);
        var unrelatedPayload = HeadAlertOutboxPayloadSerializer.Deserialize(storedUnrelated.Payload);
        Assert.Equal(_other.Id, unrelatedPayload.TargetUserId);

        var storedMatching = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == matching.Id);
        Assert.Equal(_successor.Id, storedMatching.TargetUserId);
    }

    [Fact]
    public async Task ApplyAsync_InvalidOutboxPayload_LeavesRowUntouched()
    {
        var occurredAt = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);
        var pending = new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            Payload = "not-json-payload",
            PayloadVersion = 1,
            OccurredAt = occurredAt,
            CreatedAt = occurredAt,
            NextAttemptAt = occurredAt,
            TargetUserId = _predecessor.Id,
        };
        _db.HeadAlertOutboxMessages.Add(pending);
        _db.SaveChanges();

        var result = await ApplyAsync();

        Assert.Equal(0, result.UpdatedOutboxMessages);

        var stored = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .SingleAsync(m => m.Id == pending.Id);
        Assert.Equal(_predecessor.Id, stored.TargetUserId);
        Assert.Equal("not-json-payload", stored.Payload);
    }

    [Fact]
    public async Task ApplyAsync_WithoutActiveTransaction_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ApplyAsync(
            _predecessor.Id, _successor.Id, "رئيس سابق", "رئيس خلف", "admin"));
    }
}
