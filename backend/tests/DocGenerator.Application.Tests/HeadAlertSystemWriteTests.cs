using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة معالج التنبيه النظامي (الخطة §8.1 و§6.4): نطاق المفتاح المبنيّ من
/// المستلِم المُحلّول، وترتيب الأرشفة مع الاستثناء، وتبديل المستلم عند التعاقب،
/// وحارس المعرّف المستثنى.
/// </summary>
public class HeadAlertSystemWriteTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<DocGeneratorDbContext> _options;
    private readonly ServiceProvider _provider;
    private readonly int _branchId;
    private readonly User _head;
    private readonly User _lawyer;
    private readonly User _otherLawyer;

    public HeadAlertSystemWriteTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = TestDb.CreateOptions(_connection);

        using var seed = new DocGeneratorDbContext(_options);
        seed.Database.EnsureCreated();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        seed.Branches.Add(branch);
        seed.SaveChanges();
        _branchId = branch.Id;

        _head = EntityBuilders.NewUser("head_x", "رئيس القسم", UserRole.Head, _branchId);
        _lawyer = EntityBuilders.NewUser("law1", "محامي", UserRole.Lawyer, _branchId);
        _otherLawyer = EntityBuilders.NewUser("law2", "محامي آخر", UserRole.Lawyer, _branchId);
        seed.Users.AddRange(_head, _lawyer, _otherLawyer);
        seed.SaveChanges();

        _provider = BuildProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new DocGeneratorDbContext(_options));
        services.AddScoped<ITransactionRunner>(sp => new TransactionRunner(sp.GetRequiredService<DocGeneratorDbContext>()));
        services.AddScoped<ITransactionContext>(sp => (ITransactionContext)sp.GetRequiredService<ITransactionRunner>());
        services.AddScoped<IHeadAlertService>(sp => new HeadAlertService(
            new HeadAlertRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new DocumentRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new UserRepository(sp.GetRequiredService<DocGeneratorDbContext>()),
            new Repository<Branch>(sp.GetRequiredService<DocGeneratorDbContext>()),
            new UnitOfWork(sp.GetRequiredService<DocGeneratorDbContext>()),
            sp.GetRequiredService<ITransactionRunner>(),
            new FakeAuditLogger(),
            sp.GetRequiredService<ITransactionContext>()));

        return services.BuildServiceProvider();
    }

    private IHeadAlertService Alerts() => _provider.GetRequiredService<IHeadAlertService>();

    private ITransactionRunner Tx() => _provider.GetRequiredService<ITransactionRunner>();

    /// <summary>سياق جديد للقراءة — سياق الجذر مُخزَّن مؤقتًا فلا يصلح للاستعمال بعد الإهمال.</summary>
    private DocGeneratorDbContext NewDb() => new(_options);

    private async Task<Document> AddDocumentAsync(User owner)
    {
        using var db = NewDb();
        var document = new Document
        {
            BranchId = _branchId,
            CreatedById = owner.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        return document;
    }

    [Fact]
    public async Task CreateSystemAsync_DocumentCreatorWithoutRecipientUser_UsesResolvedCreatorInKey()
    {
        // «منشئ الملف» يصحّح عبر الملف وحده (بلا RecipientUserId) — مسار مسموح (§9.2).
        var document = await AddDocumentAsync(_lawyer);
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationAssignedToLawyer,
            "إسناد ملف إنابة",
            HeadAlertAudience.DocumentCreator,
            _branchId,
            _head.Id,
            DocumentId: document.Id,
            DelegationId: 42);

        var result = await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(request, DateTime.UtcNow, null, null, token));

        Assert.Equal(HeadAlertWriteResult.Created, result);

        using var db = NewDb();
        var alert = await db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().SingleAsync();
        Assert.Equal(_lawyer.Id, Assert.Single(alert.Recipients).UserId);
        Assert.Contains($"document:{document.Id}:recipient:{_lawyer.Id}", alert.DedupeKey!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSystemAsync_EntityGroupKeyWithoutPublicEntityId_UsesGroupScope()
    {
        // أحداث المجموعة (بث P2/P3) لا تحمل PublicEntityId — المفتاح يُبنى من EntityGroupKey.
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.EntityRenameLawyers,
            "أُعيدت تسمية جهة",
            HeadAlertAudience.BranchLawyers,
            _branchId,
            _head.Id,
            EntityOperation: ActionKindCatalog.Rename,
            EntityGroupKey: "77");

        var result = await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(request, DateTime.UtcNow, null, null, token));

        Assert.Equal(HeadAlertWriteResult.Created, result);

        using var db = NewDb();
        var alert = await db.HeadAlerts.AsNoTracking().SingleAsync();
        Assert.Equal(
            $"entity:rename:77:branch:{_branchId}:audience:lawyers:{HeadAlertKindCatalog.EntityRenameLawyers}",
            alert.DedupeKey);
    }

    [Fact]
    public async Task CreateSystemAsync_SupersedePlan_DoesNotArchiveTheNewAlert()
    {
        // تنبيه قديم بنفس الإنابة ونفس النوع — يُؤرشف بالبديل، والبديل نفسه يبقى نشطًا.
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "بانتظار اعتماد الإنابة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 77);

        await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(request, DateTime.UtcNow, null, null, token));

        var plan = new HeadAlertSupersedePlan(
            77, null, new HashSet<string>(StringComparer.Ordinal) { HeadAlertKindCatalog.DelegationPendingApproval },
            "delegation_completed");

        var changed = request with { Message = "نص بديل" };
        var result = await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(changed, DateTime.UtcNow.AddMinutes(1), plan, null, token));

        Assert.Equal(HeadAlertWriteResult.Updated, result);

        using var db = NewDb();
        var alerts = await db.HeadAlerts.AsNoTracking().OrderBy(a => a.Id).ToListAsync();

        // صف واحد نشط (البديل) — لا صفان، ولا أرشفة للبديل نفسه.
        var active = Assert.Single(alerts.Where(a => !a.IsArchived));
        Assert.Equal("نص بديل", active.Message);
        Assert.Null(active.ArchivedAt);
    }

    [Fact]
    public async Task CreateSystemAsync_SuccessionOverride_ReplacesPredecessorRecipient()
    {
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationMirrorUpdated,
            "تحديث نسخة المناب",
            HeadAlertAudience.SingleLawyer,
            _branchId,
            _head.Id,
            RecipientUserId: _lawyer.Id,
            TargetLawyerId: _lawyer.Id,
            DelegationId: 88);

        await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(request, DateTime.UtcNow, null, null, token));

        // التعاقب: الخلف يستلم التنبيه (تجاوز صريح من صف Outbox).
        var overridden = request with
        {
            RecipientUserId = _otherLawyer.Id,
            TargetLawyerId = _otherLawyer.Id,
        };

        var result = await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(overridden, DateTime.UtcNow.AddMinutes(1), null, _otherLawyer.Id, token));

        // مفتاح منع التكرارincludes المستلِم ⇒ حدث الخلف تيار مختلف ⇒ صف جديد
        // (تنبيه السلف يُؤرشف/يُدمج في مسار التعاقب — PR-2 الجزء الثاني).
        Assert.Equal(HeadAlertWriteResult.Created, result);

        using var db = NewDb();
        var alerts = await db.HeadAlerts.Include(a => a.Recipients).AsNoTracking().OrderBy(a => a.Id).ToListAsync();

        var successorAlert = alerts.Last();
        // «لا يبقى السلف في التنبيه» — الصف المكتوب يحمل البديل وحده.
        Assert.Equal(_otherLawyer.Id, Assert.Single(successorAlert.Recipients).UserId);
        Assert.Equal(_otherLawyer.Id, successorAlert.TargetLawyerId);
        Assert.Contains($"recipient:{_otherLawyer.Id}", successorAlert.DedupeKey!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSystemAsync_SuccessionOverride_NewRecipientInheritsPredecessorReadState()
    {
        // دفاعي (§8.5): صف قائم بمفتاح الخلف لكن مستلمه السلف (حالة تعاقب غير مكتملة).
        // عند وصول حدث الخلف يُضاف الخلف الجديد بحالة قراءة السلف بدل فقدانها.
        var occurredAt = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Utc);
        var readAt = occurredAt.AddMinutes(-5);
        var key = $"delegation:88:recipient:{_otherLawyer.Id}:{HeadAlertKindCatalog.DelegationMirrorUpdated}";

        int alertId;
        using (var seed = NewDb())
        {
            var alert = new HeadAlert
            {
                BranchId = _branchId,
                CreatedById = _head.Id,
                TargetType = HeadAlertTargetType.Lawyer,
                Kind = HeadAlertKindCatalog.DelegationMirrorUpdated,
                DedupeKey = key,
                Message = "تحديث نسخة المناب",
                TargetLawyerId = _lawyer.Id,
                CreatedAt = occurredAt.AddMinutes(-10),
                LastContentChangedAt = occurredAt.AddMinutes(-10),
                Recipients = new List<HeadAlertRecipient>
                {
                    new() { UserId = _lawyer.Id, IsRead = true, ReadAt = readAt },
                },
            };
            seed.HeadAlerts.Add(alert);
            await seed.SaveChangesAsync();
            alertId = alert.Id;
        }

        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationMirrorUpdated,
            "تحديث نسخة المناب",
            HeadAlertAudience.SingleLawyer,
            _branchId,
            _head.Id,
            RecipientUserId: _lawyer.Id,
            TargetLawyerId: _lawyer.Id,
            DelegationId: 88);

        var result = await Tx().RunAsync(token =>
            Alerts().CreateSystemAsync(request, occurredAt, null, _otherLawyer.Id, token));

        Assert.Equal(HeadAlertWriteResult.Unchanged, result);

        using var db = NewDb();
        var stored = await db.HeadAlerts.Include(a => a.Recipients)
            .AsNoTracking().SingleAsync(a => a.Id == alertId);
        Assert.Equal(_otherLawyer.Id, stored.TargetLawyerId);
        var recipient = Assert.Single(stored.Recipients);
        Assert.Equal(_otherLawyer.Id, recipient.UserId);
        Assert.True(recipient.IsRead);
        Assert.Equal(readAt, recipient.ReadAt);
    }

    [Fact]
    public async Task ArchiveByScopeAndKinds_UnknownExcludeIsRejected()
    {
        // حارس المعرّف المستثنى (الخطة §8.1): معرّف غير موجود أو مؤرشف = خطأ
        // استدعاء — لا أرشفة جماعية صامتة. والعقد العام بلا معرّف مستثنى أصلًا.
        using var db = NewDb();
        var repository = new HeadAlertRepository(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.ArchiveByScopeAndKindsAsync(
            555,
            HeadAlertScopeKind.Delegation,
            new HashSet<string>(StringComparer.Ordinal) { HeadAlertKindCatalog.DelegationPendingApproval },
            4242,
            CancellationToken.None));
    }

    [Fact]
    public async Task CreateSystemAsync_WithoutActiveTransaction_Throws()
    {
        var request = new CreateSystemHeadAlertRequest(
            HeadAlertKindCatalog.DelegationPendingApproval,
            "رسالة",
            HeadAlertAudience.SingleHead,
            _branchId,
            _head.Id,
            RecipientUserId: _head.Id,
            DelegationId: 99);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Alerts().CreateSystemAsync(request, DateTime.UtcNow, null, null, CancellationToken.None));
    }
}
