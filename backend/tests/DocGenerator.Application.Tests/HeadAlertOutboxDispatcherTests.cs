using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Outbox;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة مشغّل صندوق الصادر (الخطة §6.4 و§14.1): الالتقاط المرتّب، والمعالجة داخل
/// معاملة واحدة، والتخطي، والتعارض، والحمولة التالفة، وتنظيف الصفوف.
/// </summary>
public class HeadAlertOutboxDispatcherTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DocGeneratorDbContext> _options;
    private readonly DocGeneratorDbContext _db;
    private readonly ServiceProvider _provider;
    private readonly HeadAlertOutboxDispatcher _dispatcher;
    private readonly int _branchId;
    private readonly User _head;
    private readonly User _lawyer;

    public HeadAlertOutboxDispatcherTests()
    {
        // اتصال مشترك + سياق لكل نطاق (كما في الإنتاج): كل دورة معالجة تحصل على
        // سياق نظيف، فلا تتبّع كيانات ملوّث ولا تعارض تتبّع بين النطاقات.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = TestDb.CreateOptions(_connection);
        _db = new DocGeneratorDbContext(_options);
        _db.Database.EnsureCreated();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;

        _head = EntityBuilders.NewUser("head_x", "رئيس القسم", UserRole.Head, _branchId);
        _lawyer = EntityBuilders.NewUser("law1", "محامي", UserRole.Lawyer, _branchId);
        _db.Users.AddRange(_head, _lawyer);
        _db.SaveChanges();

        // فحص المراجع قبل الإدراج يقرأ الإنابات من القاعدة: صفوف حقيقية للمعرّفات
        // الثابتة التي تستعملها الاختبارات.
        HeadAlertReferences.SeedDelegations(_db, _branchId, _head.Id, 7, 8, 101, 102);

        _provider = BuildProvider();
        _dispatcher = CreateDispatcher(_provider);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    private ServiceProvider BuildProvider(
        Func<IServiceProvider, IAuditLogger>? auditFactory = null,
        ConflictGate? conflictGate = null,
        Func<Exception?>? interceptCreate = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new DocGeneratorDbContext(_options));
        services.AddScoped<ITransactionRunner>(sp => new TransactionRunner(sp.GetRequiredService<DocGeneratorDbContext>()));
        services.AddScoped<ITransactionContext>(sp => (ITransactionContext)sp.GetRequiredService<ITransactionRunner>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IHeadAlertOutboxStore, HeadAlertOutboxStore>();
        services.AddScoped<IHeadAlertOutbox, HeadAlertOutbox>();
        services.AddScoped<IHeadAlertService>(sp =>
        {
            var service = new HeadAlertService(
                new HeadAlertRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
                new DocumentRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
                new UserRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
                new Repository<Branch>(sp.GetRequiredService<DocGeneratorDbContext>()),
                new DelegationRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
                new UnitOfWork(sp.GetRequiredService<DocGeneratorDbContext>()),
                sp.GetRequiredService<ITransactionRunner>(),
                auditFactory?.Invoke(sp) ?? new FakeAuditLogger(),
                sp.GetRequiredService<ITransactionContext>());

            if (conflictGate is not null || interceptCreate is not null)
                return new CreateFailureAlertService(service, conflictGate, interceptCreate);

            return service;
        });

        return services.BuildServiceProvider();
    }

    /// <summary>مشغّل على مزوّد بديل — للاختبارات التي تحتاج حقن فشل في مسار المعالجة.</summary>
    private static HeadAlertOutboxDispatcher CreateDispatcher(IServiceProvider provider) => new(
        provider.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new HeadAlertOutboxOptions { Enabled = true, MaxAttempts = 3 }),
        new HeadAlertOutboxLock("Data Source=:memory:", false, NullLogger<HeadAlertOutboxLock>.Instance),
        new DbExceptionClassifier(),
        NullLogger<HeadAlertOutboxDispatcher>.Instance);

    private IHeadAlertOutbox Outbox() => _provider.GetRequiredService<IHeadAlertOutbox>();

    private ITransactionRunner Tx() => _provider.GetRequiredService<ITransactionRunner>();

    private async Task<HeadAlertOutboxMessage> EnqueueAsync(
        CreateSystemHeadAlertRequest? request = null,
        DateTime? occurredAt = null)
    {
        var outbox = Outbox();
        var effective = request ?? new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "بانتظار اعتماد الإنابة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 7);

        // الحفظ عبر سياق النطاق نفسه (لا سياق الاختبار) — فالصف المُسجَّل في سياق
        // النطاق هو ما يُحفظ ضمن معاملة العمل.
        var uow = _provider.GetRequiredService<IUnitOfWork>();
        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueSystemAsync(effective, null, occurredAt ?? DateTime.UtcNow, token);
            await uow.SaveChangesAsync(token);
        });

        return await _db.HeadAlertOutboxMessages.AsNoTracking().OrderByDescending(m => m.Id).FirstAsync();
    }

    [Fact]
    public async Task ProcessNext_NoMessages_ReturnsFalse()
    {
        Assert.False(await _dispatcher.ProcessNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ProcessNext_ValidUpsert_CreatesAlertAndMarksRowProcessed()
    {
        await EnqueueAsync();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var alert = await _db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertKindCatalog.DelegationPendingApproval, alert.Kind);
        Assert.Equal("بانتظار اعتماد الإنابة", alert.Message);
        Assert.NotNull(alert.DedupeKey);
        Assert.Contains(HeadAlertKindCatalog.DelegationPendingApproval, alert.DedupeKey!, StringComparison.Ordinal);
        Assert.Equal(_head.Id, Assert.Single(alert.Recipients).UserId);

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Processed, message.Status);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public async Task ProcessNext_ClaimsInIdOrder_AndProcessesOneRowPerCycle()
    {
        // حدثان مختلفان (إنابتان) => مفتاحان مختلفان => صفّان نشطان.
        var first = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "إنابة أولى",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 101);
        var second = first with { Message = "إنابة ثانية", DelegationId = 102 };

        await EnqueueAsync(first);
        await EnqueueAsync(second);

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));
        Assert.False(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        Assert.Equal(2, await _db.HeadAlerts.CountAsync());
        Assert.All(
            await _db.HeadAlertOutboxMessages.AsNoTracking().ToListAsync(),
            m => Assert.Equal(HeadAlertOutboxStatus.Processed, m.Status));
    }

    [Fact]
    public async Task ProcessNext_NoEligibleRecipients_SkipsWithoutFailing()
    {
        // مستلم غير موجود: يُتخطّى (لا مستلمين) ولا يُفشل.
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة بلا مستلم",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: 9999,
            DelegationId: 8);

        await EnqueueAsync(request);

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Skipped, message.Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.NoRecipients, message.LastError);
        Assert.Empty(await _db.HeadAlerts.ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_SingleHeadWithoutRecipient_SkipsAsNoRecipientsWithoutRetry()
    {
        // مسارات الإنابة تُسجّل الرسالة حتى عند غياب رئيس نشط (ownerId = null)؛
        // غياب المستلم حالة عمل تُتخطّى بلا إعادة، لا خطأ عام يُستهلك فيه رصيد المحاولات.
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة بلا رئيس",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            DelegationId: 7);

        await EnqueueAsync(request);

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Skipped, message.Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.NoRecipients, message.LastError);
        Assert.Equal(1, message.Attempts);
        Assert.Empty(await _db.HeadAlerts.ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_MissingDelegation_SkipsAsMissingReferenceWithoutRetry()
    {
        // إنابة محذوفة بين التسجيل والمعالجة: لا مفتاح أجنبي على DelegationId،
        // فيكشفها الفحص قبل الإدراج ويحوّل الرسالة إلى skipped بلا إعادة.
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة لإنابة محذوفة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 4242);

        await EnqueueAsync(request);

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Skipped, message.Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.MissingReference, message.LastError);
        Assert.Equal(1, message.Attempts);
        Assert.Empty(await _db.HeadAlerts.ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_InactiveSuccessionOverride_SkipsAsRecipientInactive()
    {
        var successor = EntityBuilders.NewUser("head_inactive", "خلف معطّل", UserRole.Head, _branchId);
        successor.IsActive = false;
        _db.Users.Add(successor);
        await _db.SaveChangesAsync();

        var message = await EnqueueAsync(new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة مع تعاقب",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 7));

        // محاكاة ما يفعله تطبيق التعاقب: TargetUserId وحده يُعاد توجيهه للخلف،
        // مع بقاء مستلم الحدث الأصلي في الطلب.
        var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"HeadAlertOutboxMessages\" SET \"Payload\" = {0}, \"TargetUserId\" = {1} WHERE \"Id\" = {2}",
            HeadAlertOutboxPayloadSerializer.Serialize(payload with { TargetUserId = successor.Id }),
            successor.Id,
            message.Id);
        _db.ChangeTracker.Clear();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var reloaded = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Skipped, reloaded.Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.RecipientInactive, reloaded.LastError);
        Assert.Empty(await _db.HeadAlerts.ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_ActiveSuccessionOverride_ReassignsRecipient()
    {
        var successor = EntityBuilders.NewUser("head_successor", "خلف فعّال", UserRole.SubHead, _branchId);
        _db.Users.Add(successor);
        await _db.SaveChangesAsync();

        var message = await EnqueueAsync(new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة مع تعاقب ناجح",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 7));

        var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"HeadAlertOutboxMessages\" SET \"Payload\" = {0}, \"TargetUserId\" = {1} WHERE \"Id\" = {2}",
            HeadAlertOutboxPayloadSerializer.Serialize(payload with { TargetUserId = successor.Id }),
            successor.Id,
            message.Id);
        _db.ChangeTracker.Clear();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var alert = await _db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().SingleAsync();
        Assert.Equal(successor.Id, Assert.Single(alert.Recipients).UserId);
        Assert.Contains($"recipient:{successor.Id}", alert.DedupeKey!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessNext_InvalidPayload_FailsWithoutRetry()
    {
        var message = await EnqueueAsync();
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"HeadAlertOutboxMessages\" SET \"Payload\" = 'not-json-payload' WHERE \"Id\" = {0}",
            message.Id);
        _db.ChangeTracker.Clear();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var reloaded = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, reloaded.Status);
        Assert.Contains("invalid_payload", reloaded.LastError!, StringComparison.Ordinal);
        Assert.Equal(1, reloaded.Attempts);
    }

    [Fact]
    public async Task ProcessNext_KindMismatchBetweenColumnAndPayload_FailsWithoutRetry()
    {
        var message = await EnqueueAsync();
        var payload = HeadAlertOutboxPayloadSerializer.Deserialize(message.Payload);
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"HeadAlertOutboxMessages\" SET \"Payload\" = {0} WHERE \"Id\" = {1}",
            HeadAlertOutboxPayloadSerializer.Serialize(payload with
            {
                Request = payload.Request! with { Kind = HeadAlertKindCatalog.CircuitPendingDocuments },
            }),
            message.Id);
        _db.ChangeTracker.Clear();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var reloaded = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, reloaded.Status);
        Assert.Contains("invalid_payload", reloaded.LastError!, StringComparison.Ordinal);
        Assert.Equal(1, reloaded.Attempts);
        Assert.Empty(await _db.HeadAlerts.ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_UnknownPayloadVersion_FailsWithoutRetry()
    {
        var message = await EnqueueAsync();
        await _db.Database.ExecuteSqlRawAsync(
            "UPDATE \"HeadAlertOutboxMessages\" SET \"PayloadVersion\" = 99 WHERE \"Id\" = {0}",
            message.Id);
        _db.ChangeTracker.Clear();

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var reloaded = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, reloaded.Status);
        Assert.Contains("invalid_payload", reloaded.LastError!, StringComparison.Ordinal);
        Assert.Equal(1, reloaded.Attempts);
    }

    [Fact]
    public async Task ProcessNext_SecondIdenticalEvent_UpdatesAlertWithoutCreatingSecondRow()
    {
        var occurredAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        await EnqueueAsync(occurredAt: occurredAt);

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));
        Assert.Single(await _db.HeadAlerts.ToListAsync());

        // حدث لاحق بالنص نفسه: يبقى صف واحد (بلا تحديث زمني ولا إعادة ضبط قراءة).
        await EnqueueAsync(occurredAt: occurredAt.AddMinutes(5));
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        Assert.Single(await _db.HeadAlerts.ToListAsync());
        var alert = await _db.HeadAlerts.AsNoTracking().SingleAsync();
        Assert.Equal(occurredAt, alert.LastContentChangedAt);
    }

    [Fact]
    public async Task ProcessNext_CircuitRenamedWithoutActiveAlerts_IsSkipped()
    {
        // لا تنبيهات نشطة مرتبطة بالدائرة ⇒ تخطٍّ صريح (لا «نجاح» فارغ).
        var outbox = Outbox();
        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueCircuitRenamedAsync(4242, "أُعيدت تسمية الدائرة", DateTime.UtcNow, token);
            await _provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(token);
        });

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Skipped, message.Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.NoActiveAlerts, message.LastError);
    }

    [Fact]
    public async Task ProcessNext_CircuitRenamedWithOnlyNewerAlerts_IsProcessedNotSkipped()
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = _branchId,
            Name = "دائرة التنفيذ",
            NameNorm = "دائرة التنفيذ",
            CreatedById = _head.Id,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();

        var occurredAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        await EnqueueAsync(
            new CreateSystemHeadAlertRequest(
                HeadAlertKindCatalog.CircuitPendingDocuments,
                "تنبيه دائرة",
                HeadAlertAudience.SingleLawyer,
                _branchId,
                _head.Id,
                ExecutionCircuitId: circuit.Id,
                RecipientUserId: _lawyer.Id),
            occurredAt);
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        // إعادة تسمية أقدم من آخر تغيير محتوى: لا تمحو النص الأحدث، لكن وجود صف
        // نشط يعني «معالَجة» لا «no_active_alerts».
        var outbox = Outbox();
        await Tx().RunAsync(async token =>
        {
            await outbox.EnqueueCircuitRenamedAsync(
                circuit.Id, "اسم قديم", occurredAt.AddMinutes(-5), token);
            await _provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(token);
        });

        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking()
            .OrderByDescending(m => m.Id)
            .FirstAsync();
        Assert.Equal(HeadAlertOutboxStatus.Processed, message.Status);
        var alert = await _db.HeadAlerts.AsNoTracking().SingleAsync();
        Assert.Equal("تنبيه دائرة", alert.Message);
        Assert.Equal(occurredAt, alert.LastContentChangedAt);
    }

    [Fact]
    public async Task ProcessNext_OlderEvent_IsSkippedAsStale()
    {
        var occurredAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        await EnqueueAsync(occurredAt: occurredAt);
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        // حدث أقدم: لا يمحو الأحدث.
        await EnqueueAsync(occurredAt: occurredAt.AddMinutes(-5));
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var messages = await _db.HeadAlertOutboxMessages.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(HeadAlertOutboxStatus.Processed, messages[0].Status);
        Assert.Equal(HeadAlertOutboxStatus.Skipped, messages[1].Status);
        Assert.Equal(HeadAlertOutboxSkipReasons.StaleEvent, messages[1].LastError);

        var alert = await _db.HeadAlerts.AsNoTracking().SingleAsync();
        Assert.Equal(occurredAt, alert.LastContentChangedAt);
    }

    [Fact]
    public async Task ProcessNext_ChangedContent_ResetsReadState()
    {
        var occurredAt = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        await EnqueueAsync(occurredAt: occurredAt);
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var alert = await _db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().SingleAsync();
        var recipient = Assert.Single(alert.Recipients);
        recipient.IsRead = true;
        recipient.ReadAt = DateTime.UtcNow;
        _db.HeadAlertRecipients.Update(recipient);
        await _db.SaveChangesAsync();

        var changed = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "نص جديد بعد تعديل الإنابة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 7);

        await EnqueueAsync(changed, occurredAt.AddMinutes(5));
        Assert.True(await _dispatcher.ProcessNextAsync(CancellationToken.None));

        var updated = await _db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().SingleAsync();
        Assert.Equal("نص جديد بعد تعديل الإنابة", updated.Message);
        Assert.Equal(occurredAt.AddMinutes(5), updated.LastContentChangedAt);
        Assert.Equal(occurredAt, updated.CreatedAt);
        Assert.False(Assert.Single(updated.Recipients).IsRead);
    }

    // ── سياسة الإرجاع بعد الفشل (الخطة §6.4 و§18:3081) ──

    [Fact]
    public async Task ProcessNext_BusinessTransactionFails_RollsBackAlertAndReturnsRowToPending()
    {
        // فشل داخل معاملة المعالجة بعد إدراج التنبيه (تسجيل التدقيق) يجب أن يُرجع
        // المعاملة كاملة: لا تنبيه جزئي ولا مستلمين، والصف يعود pending بمحاولة واحدة.
        // `ThrowingAuditLogger` يتحقق أولًا أن الصف أُدرج فعلًا، فلا يمرّ الاختبار
        // لو صار الفشل قبل الإدراج (كان يعني «لا كتابة» لا «تراجعًا»).
        await EnqueueAsync();

        using var provider = BuildProvider(
            auditFactory: sp => new ThrowingAuditLogger(sp.GetRequiredService<DocGeneratorDbContext>()));
        var dispatcher = CreateDispatcher(provider);

        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        Assert.Empty(await _db.HeadAlerts.AsNoTracking().ToListAsync());
        Assert.Empty(await _db.HeadAlertRecipients.AsNoTracking().ToListAsync());

        var message = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.LockedUntil);
        Assert.Contains("فشل تسجيل التدقيق", message.LastError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessNext_DedupeConflict_RetriesOnceWithMarkerThenSucceeds()
    {
        await EnqueueAsync();
        var gate = new ConflictGate(remainingConflicts: 1);

        using var provider = BuildProvider(conflictGate: gate);
        var dispatcher = CreateDispatcher(provider);

        // المحاولة الأولى: تعارض ⇒ pending فورًا بعلامة conflict_retry (لا failed،
        // ولا استهلاك عدّاد MaxAttempts)، وبلا تنبيه محفوظ من المحاولة الفاشلة.
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        var retried = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Pending, retried.Status);
        Assert.Equal(1, retried.Attempts);
        Assert.Equal(1, retried.ConflictRetries);
        Assert.Equal("conflict_retry", retried.LastError);
        Assert.Null(retried.ProcessedAt);
        Assert.Null(retried.LockedUntil);
        Assert.Empty(await _db.HeadAlerts.AsNoTracking().ToListAsync());

        // المحاولة الثانية (بعد زوال التعارض) تنجح: processed وتنبيه واحد.
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        var processed = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Processed, processed.Status);
        Assert.Equal(2, processed.Attempts);
        Assert.Equal(1, processed.ConflictRetries);
        Assert.Null(processed.LastError);
        Assert.Single(await _db.HeadAlerts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_RepeatedDedupeConflict_FailsWithConflictRetryPrefix()
    {
        // التعارض المتكرر لا يعيد إلى ما لا نهاية: المحاولة الثانية تُسجَّل failed
        // ببادئة العلامة نفسها (عقد §6.4)، ولا يُحفظ تنبيه.
        await EnqueueAsync();
        var gate = new ConflictGate(remainingConflicts: 2);

        using var provider = BuildProvider(conflictGate: gate);
        var dispatcher = CreateDispatcher(provider);

        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        var failed = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, failed.Status);
        Assert.Equal(2, failed.Attempts);
        Assert.Equal(1, failed.ConflictRetries);
        Assert.StartsWith("conflict_retry:", failed.LastError!, StringComparison.Ordinal);
        Assert.Empty(await _db.HeadAlerts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ProcessNext_DedupeKeyUniqueViolation_UsesConflictPolicyNotTransientBackoff()
    {
        // سباق الفهرس الفريد الجزئي يصل من `SaveChangesAsync` كـ`DbUpdateException`
        // (لا كـ`HeadAlertConflictException` من الخدمة)، فيمرّ بالفرع الإنتاجي
        // `IsDedupeKeyConflict` — وهذا ما لا يغطيه محقن التعارض في الاختبارات أعلاه.
        await EnqueueAsync();

        using var provider = BuildProvider(interceptCreate: BuildDedupeKeyUniqueViolation);
        var dispatcher = CreateDispatcher(provider);

        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        var retried = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Pending, retried.Status);
        Assert.Equal(1, retried.Attempts);
        Assert.Equal(1, retried.ConflictRetries);
        // العلامة وحدها (لا وصف استثناء عابر ولا backoff) — دليل أن التصنيف تعارض لا عابر.
        Assert.Equal("conflict_retry", retried.LastError);
        Assert.Null(retried.ProcessedAt);
        Assert.Null(retried.LockedUntil);
        Assert.Empty(await _db.HeadAlerts.AsNoTracking().ToListAsync());

        // التعارض المتكرر عبر المسار نفسه ⇒ failed ببادئة العلامة (إعادة واحدة فقط).
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));

        var failed = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, failed.Status);
        Assert.Equal(2, failed.Attempts);
        Assert.Equal(1, failed.ConflictRetries);
        Assert.StartsWith("conflict_retry:", failed.LastError!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessNext_ConflictThenTransientThenConflict_StillFailsAfterOneRetry()
    {
        // العقد: «إعادة واحدة» تعني واحدة **عبر كل أنواع الفشل**، لا واحدة بين كل خطأين
        // عابرين. العمود `ConflictRetries` هو الحالة، فلا يطمسها خطأ عابر في `LastError`.
        await EnqueueAsync();

        var attempts = new Queue<Exception?>(new Exception?[]
        {
            new HeadAlertConflictException("تعارض أول"),
            new InvalidOperationException("فشل عابر بين التعارضين"),
            new HeadAlertConflictException("تعارض ثانٍ"),
        });

        using var provider = BuildProvider(interceptCreate: () => attempts.Dequeue());
        var dispatcher = CreateDispatcher(provider);

        // 1) تعارض ⇒ pending + ConflictRetries=1.
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
        var afterConflict = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Pending, afterConflict.Status);
        Assert.Equal(1, afterConflict.ConflictRetries);
        Assert.Equal("conflict_retry", afterConflict.LastError);

        // 2) خطأ عابر ⇒ pending بتباعد، و`LastError` صار وصفًا (طمس العلامة) لكن العدّ بقي.
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
        var afterTransient = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Pending, afterTransient.Status);
        Assert.Equal(1, afterTransient.ConflictRetries);
        Assert.NotEqual("conflict_retry", afterTransient.LastError);

        // تجاوز التباعد التصاعدي: نُقدّم موعد المحاولة بدل انتظار 30 ثانية.
        var row = await _db.HeadAlertOutboxMessages.SingleAsync();
        row.NextAttemptAt = DateTime.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        // 3) تعارض ثانٍ ⇒ failed، رغم أن `LastError` لم يعد يحمل العلامة.
        Assert.True(await dispatcher.ProcessNextAsync(CancellationToken.None));
        var failed = await _db.HeadAlertOutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(HeadAlertOutboxStatus.Failed, failed.Status);
        Assert.Equal(3, failed.Attempts);
        Assert.Equal(1, failed.ConflictRetries);
        Assert.StartsWith("conflict_retry:", failed.LastError!, StringComparison.Ordinal);
        Assert.Empty(await _db.HeadAlerts.AsNoTracking().ToListAsync());
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
    public async Task RunMaintenanceAsync_CleansExpiredTerminalRows_AndReturnsHealth()
    {
        var now = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
        var expired = Terminal(HeadAlertOutboxStatus.Processed, now.AddDays(-9), now.AddDays(-8));
        var pending = Terminal(HeadAlertOutboxStatus.Pending, now.AddMinutes(-10), default);
        var failed = Terminal(HeadAlertOutboxStatus.Failed, now.AddDays(-1), default);

        _db.HeadAlertOutboxMessages.AddRange(expired, pending, failed);
        await _db.SaveChangesAsync();

        var health = await _dispatcher.RunMaintenanceAsync(now, CancellationToken.None);

        Assert.Equal(0, await _db.HeadAlertOutboxMessages.CountAsync(m => m.Id == expired.Id));
        Assert.Equal(1, health.PendingCount);
        Assert.Equal(pending.CreatedAt, health.OldestPendingAt);
        Assert.Equal(1, health.FailedCount);
    }

    /// <summary>
    /// بديل اختباري يحقن فشلًا في <see cref="IHeadAlertService.CreateSystemAsync"/> قبل
    /// التفويض، بلا تكرار تمرير بقية الأعضاء:
    /// <list type="bullet">
    /// <item><b>بوابة تعارض</b> (<see cref="ConflictGate"/>): ترمي
    /// <see cref="HeadAlertConflictException"/> — مسار استثناء الخدمة.</item>
    /// <item><b>مُعترِض</b> (<c>intercept</c>): يعيد استثناءً جاهزًا ليرميه، أو <c>null</c>
    /// للمرور للخدمة الحقيقية — يسمح بتسلسل فشل متغيّر بين المحاولات (تعارض/عابر).</item>
    /// </list>
    /// </summary>
    private sealed class CreateFailureAlertService : IHeadAlertService
    {
        private readonly IHeadAlertService _inner;
        private readonly ConflictGate? _gate;
        private readonly Func<Exception?>? _intercept;

        public CreateFailureAlertService(
            IHeadAlertService inner,
            ConflictGate? gate = null,
            Func<Exception?>? intercept = null)
        {
            _inner = inner;
            _gate = gate;
            _intercept = intercept;
        }

        public Task<HeadAlertWriteResult> CreateSystemAsync(
            CreateSystemHeadAlertRequest request,
            DateTime occurredAt,
            HeadAlertSupersedePlan? supersedePlan,
            int? targetUserIdOverride,
            CancellationToken ct = default)
        {
            if (_intercept?.Invoke() is { } intercepted)
                throw intercepted;

            if (_gate?.TryConsumeConflict() == true)
                throw new HeadAlertConflictException("تعارض محاكى على مفتاح منع التكرار");

            return _inner.CreateSystemAsync(request, occurredAt, supersedePlan, targetUserIdOverride, ct);
        }

        // بقية الأعضاء تمرير مباشر: الاختبار يستهدف مسار CreateSystemAsync وحده.
        public Task<PagedResult<HeadAlertSummaryDto>> ListForActorAsync(
            HeadAlertActor actor, HeadAlertListQuery query, CancellationToken ct = default)
            => _inner.ListForActorAsync(actor, query, ct);

        public Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
            HeadAlertActor actor, AlertArchiveFilter archive, AlertPageRequest page, string? cursor,
            CancellationToken ct = default)
            => _inner.ListSentAsync(actor, archive, page, cursor, ct);

        public Task<PagedResult<HeadAlertSummaryDto>> ListByDelegationAsync(
            int delegationId, HeadAlertActor actor, AlertPageRequest page, CancellationToken ct = default)
            => _inner.ListByDelegationAsync(delegationId, actor, page, ct);

        public Task<IReadOnlyDictionary<int, DelegationLocalAdditionDto>> GetLocalAdditionSummariesAsync(
            IReadOnlyCollection<int> delegationIds, CancellationToken ct = default)
            => _inner.GetLocalAdditionSummariesAsync(delegationIds, ct);

        public Task<HeadAlertDto?> GetDetailsAsync(int id, HeadAlertActor actor, CancellationToken ct = default)
            => _inner.GetDetailsAsync(id, actor, ct);

        public Task<List<HeadAlertRecipientDto>?> GetRecipientsAsync(
            int id, HeadAlertActor actor, CancellationToken ct = default)
            => _inner.GetRecipientsAsync(id, actor, ct);

        public Task<int> CountUnreadAsync(
            HeadAlertActor actor, AlertUnreadScope scope, CancellationToken ct = default)
            => _inner.CountUnreadAsync(actor, scope, ct);

        public Task<HeadAlertSummaryDto> CreateAsync(
            CreateHeadAlertRequest request, HeadAlertActor actor, string? actorName, CancellationToken ct = default)
            => _inner.CreateAsync(request, actor, actorName, ct);

        public Task<bool> MarkReadAsync(int alertId, HeadAlertActor actor, CancellationToken ct = default)
            => _inner.MarkReadAsync(alertId, actor, ct);

        public Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default)
            => _inner.RemoveAppealRecipientAsync(appealId, userId, ct);

        public Task<int> ApplyCircuitRenamedAsync(
            int circuitId, string message, DateTime occurredAt, CancellationToken ct = default)
            => _inner.ApplyCircuitRenamedAsync(circuitId, message, occurredAt, ct);

        public Task<int> ArchiveByDelegationAsync(
            int delegationId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
            => _inner.ArchiveByDelegationAsync(delegationId, archiveKinds, reason, ct);

        public Task<int> ArchiveByAppealAsync(
            int appealId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
            => _inner.ArchiveByAppealAsync(appealId, archiveKinds, reason, ct);
    }

    /// <summary>بوابة تعارض مشتركة بين النطاقات (الخدمة مسجّلة Scoped والمشغّل ينشئ نطاقًا لكل دورة).</summary>
    private sealed class ConflictGate
    {
        private int _remaining;

        public ConflictGate(int remainingConflicts) => _remaining = remainingConflicts;

        public bool TryConsumeConflict() => Interlocked.Decrement(ref _remaining) >= 0;
    }

    /// <summary>
    /// مُدقِّق يفشل دائمًا داخل معاملة المعالجة — لكنه يتحقق أولًا أن التنبيه ومستلميه
    /// أُدرجا فعلًا في القاعدة (نفس نطاق المعاملة)، فلا يمرّ الاختبار لو صار الفشل قبل
    /// الإدراج: كان يعني «لا كتابة» لا «تراجعًا».
    /// </summary>
    private sealed class ThrowingAuditLogger : IAuditLogger
    {
        private readonly DocGeneratorDbContext _db;

        public ThrowingAuditLogger(DocGeneratorDbContext db) => _db = db;

        public async Task LogAsync(string? userName, string actionType, int? documentId = null,
            string? documentType = null, string? details = null, CancellationToken ct = default)
        {
            var alerts = await _db.HeadAlerts.AsNoTracking().CountAsync(ct);
            var recipients = await _db.HeadAlertRecipients.AsNoTracking().CountAsync(ct);
            if (alerts != 1 || recipients == 0)
                throw new InvalidOperationException(
                    $"لم يُدرج التنبيه قبل التدقيق (alerts={alerts}, recipients={recipients}) — لا يثبت الاختبار تراجعًا");

            throw new InvalidOperationException("فشل تسجيل التدقيق (محاكى)");
        }

        public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
            => throw new InvalidOperationException("فشل تسجيل التدقيق (محاكى)");

        public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
            string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
            CancellationToken ct = default)
            => throw new InvalidOperationException("فشل تسجيل التدقيق (محاكى)");
    }

    /// <summary>
    /// يولّد <see cref="DbUpdateException"/> حقيقيًا من تعارض الفهرس الفريد الجزئي على
    /// <c>DedupeKey</c> (SQLite 19/2067، ونصّه يحوي اسم العمود) — كما يحدث في الإنتاج
    /// عند سباق إدراجين متزامنين، بدل تلفيق استثناء لا يشبه الواقع.
    /// </summary>
    private static DbUpdateException BuildDedupeKeyUniqueViolation()
    {
        using var db = TestDb.Create();

        var branch = db.Branches.Add(new Branch { Name = "حلب", Code = "ALP" }).Entity;
        db.SaveChanges();

        var head = new User
        {
            Username = "dedupe_racer",
            FullName = "رئيس متسابق",
            Role = UserRole.Head,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(head);
        db.SaveChanges();

        var occurredAt = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc);
        HeadAlert Row() => new()
        {
            BranchId = branch.Id,
            CreatedById = head.Id,
            TargetType = HeadAlertTargetType.Head,
            Kind = HeadAlertKindCatalog.DelegationPendingApproval,
            DedupeKey = "race:dedupe-key",
            Message = "نص متسابق",
            CreatedAt = occurredAt,
            LastContentChangedAt = occurredAt,
        };

        db.HeadAlerts.Add(Row());
        db.SaveChanges();

        db.HeadAlerts.Add(Row());
        try
        {
            db.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            return ex;
        }

        throw new InvalidOperationException(
            "لم يُولَّد تعارض فريد على DedupeKey — تحقق من الفهرس الجزئي UX_HeadAlerts_DedupeKey_Active");
    }
}
