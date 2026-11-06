using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

using static DocGenerator.Application.Tests.TestHelpers.MirrorRequests;

namespace DocGenerator.Application.Tests;

/// <summary>
/// جولات سلامة السباق (B3): محاولات تسطير متزامنة على الأصل نفسه عبر اتصالين
/// مستقلين وخيطين — في كل جولة: فائز واحد + خطأ مجال («إنابة سارية») للخاسر.
/// ملاحظة تجريبية موثقة: في مكدس SQLite داخل العملية تتسلسل قراءات الحارس ذاتيًا
/// (أي انتظار داخل معاملة مفتوحة بعد قراءاتها يجمّد القارئ الآخر)، فلا يمكن إجبار
/// التداخل الكامل حتميًا من الخارج — هذه الجولات تثبت السلامة في كل تداخل متحقق،
/// ومسار التعارض الحتمي مغطى بـ DbExceptionClassifierTests + مراجعة موضع الالتقاط.
/// القيد الفريد ضروري وحاسم على PostgreSQL (MVCC حقيقي بلا تسلسل ذاتي).
/// </summary>
public class DelegationConcurrencyTests : IDisposable
{
    private readonly string _path = Path.GetTempFileName();
    private readonly List<SqliteConnection> _connections = new();

    public void Dispose()
    {
        foreach (var c in _connections)
            c.Dispose();
        try { File.Delete(_path); } catch { /* ملف مؤقت — التجاهل آمن */ }
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection($"Data Source={_path}");
        await connection.OpenAsync();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 10000;";
        await pragma.ExecuteNonQueryAsync();
        _connections.Add(connection);
        return connection;
    }


    private static IDocumentDelegationService BuildService(DocGeneratorDbContext db)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var branches = new Repository<Branch>(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var audit = new FakeAuditLogger();
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(db), documents, users, branches, new DelegationRepository(db),
            uow, new TransactionRunner(db), audit);
        return new DocumentDelegationService(
            new DelegationRepository(db),
            new DelegationReservationRepository(db),
            new DbExceptionClassifier(),
            documents,
            users,
            branches,
            new Repository<DocumentRegistrationDate>(db),
            new Repository<DocumentOccurrence>(db),
            uow,
            tx,
            audit,
            headAlerts,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            TimeProvider.System,
            TestClock.TimeZone);
    }

    private async Task<(int SourceId, List<int> AssetIds, int LawyerId)> SeedAsync()
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(TestDb.CreateOptions(connection));
        await db.Database.EnsureCreatedAsync();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var lawyer = new User
        {
            Username = "race_lawyer",
            FullName = "محامي السباق",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.Add(lawyer);
        await db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = lawyer.Id,
            BranchId = branch.Id,
            BranchName = branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = "520",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = "أحمد الخطيب المدعي 520",
        };
        db.Documents.Add(source);
        await db.SaveChangesAsync();

        var assetIds = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            var asset = new Asset
            {
                DocumentId = source.Id,
                AssetKind = AssetKindCatalog.RealEstate,
                PropertyNumber = (77 + i).ToString(),
                // بذور محجوزة ليتجاوز السباق حارس «لا إنابة على مالٍ بلا حجز».
                SeizureDate = new DateTime(2026, 8, 1),
            };
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
            assetIds.Add(asset.Id);
        }

        return (source.Id, assetIds, lawyer.Id);
    }

    private static UpsertDelegationRequest Request(int assetId) => new(
        DelegatedCourt: "دائرة تنفيذ حلب",
        IsExternal: false,
        ExternalBranchId: null,
        DelegationDate: "1/8/2026",
        DelegationText: "سباق على الأصل نفسه",
        DepositBookNumber: "كتاب-1",
        DepositBookDate: "2/8/2026",
        AssetIds: new List<int> { assetId });

    private sealed record RaceOutcome(DelegationDto? Dto, Exception? Error);

    [Fact]
    public async Task ConcurrentCreate_OnSameAsset_ExactlyOneWins_WithDomainError()
    {
        var (sourceId, assetIds, lawyerId) = await SeedAsync();

        var connectionA = await OpenConnectionAsync();
        var connectionB = await OpenConnectionAsync();
        await using var dbA = new DocGeneratorDbContext(TestDb.CreateOptions(connectionA));
        await using var dbB = new DocGeneratorDbContext(TestDb.CreateOptions(connectionB));
        var serviceA = BuildService(dbA);
        var serviceB = BuildService(dbB);

        async Task<RaceOutcome> RunAsync(IDocumentDelegationService service, int assetId)
        {
            try
            {
                var dto = await service.CreateAsync(sourceId, Request(assetId), lawyerId, "race");
                return new RaceOutcome(dto, null);
            }
            catch (Exception ex)
            {
                return new RaceOutcome(null, ex);
            }
        }

        foreach (var assetId in assetIds)
        {
            var outcomes = await Task.WhenAll(
                Task.Run(() => RunAsync(serviceA, assetId)),
                Task.Run(() => RunAsync(serviceB, assetId)));

            Assert.Single(outcomes, o => o.Dto is not null);
            var loser = Assert.Single(outcomes, o => o.Error is not null);
            var domainError = Assert.IsType<ArgumentException>(loser.Error);
            Assert.Contains("إنابة سارية", domainError.Message);
        }

        await using var verifyDb = new DocGeneratorDbContext(TestDb.CreateOptions(await OpenConnectionAsync()));
        Assert.Equal(assetIds.Count, await verifyDb.DocumentDelegations.CountAsync(d => d.SourceDocumentId == sourceId));
        Assert.Equal(assetIds.Count, await verifyDb.DelegationAssetReservations.CountAsync(r => r.SourceDocumentId == sourceId));
    }

    /// <summary>خدمات سياقٍ واحد: كل اتصال = سياق مستقل = «مستخدم» مستقل في اختبار السباق.</summary>
    private static (IDocumentDelegationService Delegations, IDocumentService Documents, IHeadAlertService Alerts)
        BuildServices(DocGeneratorDbContext db)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var branches = new Repository<Branch>(db);
        var registrationDates = new Repository<DocumentRegistrationDate>(db);
        var occurrences = new Repository<DocumentOccurrence>(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var audit = new FakeAuditLogger();
        var alerts = new HeadAlertService(
            new HeadAlertRepository(db), documents, users, branches, new DelegationRepository(db),
            uow, tx, audit);
        var delegations = new DocumentDelegationService(
            new DelegationRepository(db),
            new DelegationReservationRepository(db),
            new DbExceptionClassifier(),
            documents,
            users,
            branches,
            registrationDates,
            occurrences,
            uow,
            tx,
            audit,
            alerts,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx),
            TimeProvider.System,
            TestClock.TimeZone);
        var bundle = DocumentServiceFactory.Create(
            db, audit, documents: documents, users: users, registrationDates: registrationDates,
            occurrences: occurrences, uow: uow, tx: tx, alertService: alerts);
        return (delegations, bundle.Documents, alerts);
    }

    private sealed record MirrorSeed(int SourceId, int DelegationId, int TargetId, int LawyerId);

    /// <summary>
    /// إنابة مسجّلة بملف مناب قابل للتعديل: منيب → تسطير → إسناد (يُنشئ الملف المناب) → تسجيل أصول.
    /// </summary>
    private async Task<MirrorSeed> SeedRegisteredMirrorAsync(int round = 0)
    {
        var connection = await OpenConnectionAsync();
        await using var db = new DocGeneratorDbContext(TestDb.CreateOptions(connection));
        await db.Database.EnsureCreatedAsync();

        // لاحقة الجولة: قاعدة الملف واحدة للصنف، والقيم الفريدة (رمز الفرع/اسم المستخدم/رقم
        // الملف) تتكرر بين جولات السباق.
        var suffix = round == 0 ? string.Empty : round.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var branch = new Branch { Name = "دمشق", Code = $"DAM{suffix}" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var owner = new User
        {
            Username = $"mirror_owner{suffix}",
            FullName = "مالك المنيب",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        var head = new User
        {
            Username = $"mirror_head{suffix}",
            FullName = "رئيس القسم",
            Role = UserRole.Head,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        var assignee = new User
        {
            Username = $"mirror_assignee{suffix}",
            FullName = "محامي المناب",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        db.Users.AddRange(owner, head, assignee);
        await db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = owner.Id,
            BranchId = branch.Id,
            BranchName = branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = $"52{suffix}",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = $"أحمد الخطيب المدعي 52{suffix}",
        };
        db.Documents.Add(source);
        await db.SaveChangesAsync();

        var asset = new Asset
        {
            DocumentId = source.Id,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "901",
            // حجز مُبذَّر ليتجاوز حارس «لا إنابة على مالٍ بلا حجز».
            SeizureDate = new DateTime(2026, 8, 1),
        };
        db.Assets.Add(asset);
        // وريث واحد مُبذَّر: تُضاف إليه إضافة محلية لاحقًا، فلا يبدأ الملف المناب بلا أطراف.
        db.Heirs.Add(new Heir
        {
            DocumentId = source.Id,
            GuarantorNumber = null,
            HeirName = "محمود",
            HeirFather = "سامي",
            HeirFamily = "الحلبي",
            AddressType = "عنوان",
            HeirAddress = "المزة",
        });
        await db.SaveChangesAsync();

        var (delegations, _, _) = BuildServices(db);
        var created = await delegations.CreateAsync(source.Id, Request(asset.Id), owner.Id, "owner");
        var assigned = await delegations.AssignAsync(
            created.Id, new AssignDelegationRequest(assignee.Id), head.Id, branch.Id, "head");
        await delegations.RegisterAsync(
            created.Id, new RegisterDelegationRequest($"89{suffix}", "2026", "5/8/2026"), assignee.Id, "assignee");

        return new MirrorSeed(source.Id, created.Id, assigned!.TargetDocumentId!.Value, assignee.Id);
    }

    private static DocumentUpsertRequest RequestWithHeir(Document target, string heirName)
    {
        var request = MirrorRequestWithParties(target);
        request.BorrowerHeirs = request.BorrowerHeirs
            .Append(new HeirDto(null, heirName, "أب", "عائلة", "أصالة", "عنوان", "دمشق"))
            .ToList();
        return request;
    }

    /// <summary>
    /// إصلاح جوهرى (P1): تسجيل الإضافة المحلية كتابةٌ على صف الإنابة، فيجب أن يحرّك توكنه
    /// (<c>DocumentDelegation.Version</c> — مُهيأ <c>IsConcurrencyToken</c>)، وإلا طابق شرط
    /// <c>WHERE Version = old</c> عند كاتبين متزامنَين فسقط أحد العدّادين بصمت.
    /// الاختبار حتميّ لا سباقي: صف يُقرأ قبل التعديل ثم يُكتب بعده ⇒ يجب أن يُرفض.
    /// </summary>
    [Fact]
    public async Task MirrorEdit_MovesDelegationVersion_SoAStaleCounterWriteIsRejected()
    {
        var seed = await SeedRegisteredMirrorAsync();

        // كاتب «قديم»: قرأ صف الإنابة قبل التعديل (نسخة المستخدم المفتوحة على الشاشة).
        var staleConnection = await OpenConnectionAsync();
        await using var staleDb = new DocGeneratorDbContext(TestDb.CreateOptions(staleConnection));
        var stale = await staleDb.DocumentDelegations.SingleAsync(d => d.Id == seed.DelegationId);
        var versionBefore = stale.Version;

        // تعديل حقيقي للملف المناب من سياق آخر ⇒ إضافة محلية واحدة.
        var editConnection = await OpenConnectionAsync();
        await using var editDb = new DocGeneratorDbContext(TestDb.CreateOptions(editConnection));
        var (_, editDocuments, _) = BuildServices(editDb);
        var target = await editDb.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == seed.TargetId);
        await editDocuments.UpdateAsync(
            seed.TargetId, RequestWithHeir(target, "وريث سباق"), "assignee", seed.LawyerId);

        // (1) التوكن تحرّك، والعدّاد سُجّل.
        var verifyConnection = await OpenConnectionAsync();
        await using var verifyDb = new DocGeneratorDbContext(TestDb.CreateOptions(verifyConnection));
        var (verifyDelegations, _, _) = BuildServices(verifyDb);
        var dto = Assert.Single(await verifyDelegations.ListForDocumentAsync(seed.SourceId));
        Assert.NotNull(dto.LocalAddition);
        Assert.Equal(1, dto.LocalAddition!.Count);
        Assert.Equal(versionBefore + 1, dto.Version);

        // (2) كتابة قديمة على العدّاد تُرفض ولا تمحو الزيادة (بدل «آخر كاتب يفوز»).
        stale.LocalAdditionCount += 1;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync());
    }

    /// <summary>
    /// سباق تعديلين على الملف المناب نفسه، كل منهما يضيف وريثًا مختلفًا.
    ///
    /// **الثابت الحاكم:** <c>LocalAdditionCount == عدد النجاحات</c> — لا «فائز واحد» (فالتسلسل
    /// يجعل نجاح الاثنين مشروعًا والعدد 2)، ولا «2 دائمًا» (فالتداخل يُرفض أحدهما والعدد 1).
    /// هذا الثابت يمسك فقدان التحديث في كل تداخل. والخاسر إمّا تعارض تزامن (تداخل حقيقي)
    /// أو رفض مجال من حارس المرآة (تسلسل SQLite: يرى وريث الفرع الآخر كأنه حذف) — كلاهما رفض مشروع.
    ///
    /// قيد موثَّق (كبقية أسرة التزامن هنا): SQLite داخل العملية يُسلسل الكتابات، فلا يمكن
    /// إجبار التداخل حتميًا؛ الحتمية يوفّرها الاختبار السابق (توكن الصف).
    /// </summary>
    [Fact]
    public async Task ConcurrentMirrorEdits_NeverLoseLocalAdditionCount()
    {
        for (var round = 0; round < 3; round++)
        {
            var seed = await SeedRegisteredMirrorAsync(round);

            // لقطة واحدة تبني طلبَي الفرعين: كل الحقول مطابقة للمخزَّن، والاختلاف في الوريث فقط.
            var snapshotConnection = await OpenConnectionAsync();
            await using var snapshotDb = new DocGeneratorDbContext(TestDb.CreateOptions(snapshotConnection));
            var snapshot = await snapshotDb.Documents
                .Include(d => d.RegistrationDate)
                .Include(d => d.Heirs)
                .SingleAsync(d => d.Id == seed.TargetId);
            var requestA = RequestWithHeir(snapshot, $"وريث-أ-{round}");
            var requestB = RequestWithHeir(snapshot, $"وريث-ب-{round}");

            var connectionA = await OpenConnectionAsync();
            var connectionB = await OpenConnectionAsync();
            await using var dbA = new DocGeneratorDbContext(TestDb.CreateOptions(connectionA));
            await using var dbB = new DocGeneratorDbContext(TestDb.CreateOptions(connectionB));
            var (_, documentsA, _) = BuildServices(dbA);
            var (_, documentsB, _) = BuildServices(dbB);

            async Task<Exception?> RunAsync(IDocumentService documents, DocumentUpsertRequest request)
            {
                try
                {
                    await documents.UpdateAsync(seed.TargetId, request, "race", seed.LawyerId);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex;
                }
            }

            var outcomes = await Task.WhenAll(
                Task.Run(() => RunAsync(documentsA, requestA)),
                Task.Run(() => RunAsync(documentsB, requestB)));

            var successes = outcomes.Count(e => e is null);
            Assert.InRange(successes, 1, 2);
            Assert.All(
                outcomes.Where(e => e is not null),
                e => Assert.True(
                    e is DocumentConflictException || e is ArgumentException,
                    $"خطأ غير متوقع من فرع خاسر: {e?.GetType().Name} — {e?.Message}"));

            var verifyConnection = await OpenConnectionAsync();
            await using var verifyDb = new DocGeneratorDbContext(TestDb.CreateOptions(verifyConnection));
            var (_, _, verifyAlerts) = BuildServices(verifyDb);
            await HeadAlertOutboxFlush.AllAsync(verifyDb, verifyAlerts, new TransactionRunner(verifyDb));

            var delegation = await verifyDb.DocumentDelegations
                .AsNoTracking()
                .SingleAsync(d => d.Id == seed.DelegationId);

            // الثابت: العدّاد يحصي النجاحات فقط — لا فقدان تحديث ولا عدّ مزدوج.
            Assert.Equal(successes, delegation.LocalAdditionCount);
            Assert.False(string.IsNullOrWhiteSpace(delegation.LocalAdditionLastMessage));

            var alerts = await verifyDb.HeadAlerts.CountAsync(a =>
                a.DelegationId == seed.DelegationId
                && a.Kind == HeadAlertKindCatalog.DelegationMirrorLocalAddition);
            Assert.Equal(successes, alerts);

            // (حتميّ) بعد احتواء السباق: تعديل إضافي ناجح واحد يجب أن يزيد العدّاد واحدًا
            // بالضبط فوق ما سُجّل — يثبت أن المسار يعمل صحيحًا بعد النزاع لا أنه «توقّف».
            var (_, afterDocuments, _) = BuildServices(verifyDb);
            var afterTarget = await verifyDb.Documents
                .Include(d => d.RegistrationDate)
                .Include(d => d.Heirs)
                .AsNoTracking()
                .SingleAsync(d => d.Id == seed.TargetId);
            await afterDocuments.UpdateAsync(
                seed.TargetId, RequestWithHeir(afterTarget, $"وريث-بعد-السباق-{round}"), "race", seed.LawyerId);

            var after = await verifyDb.DocumentDelegations
                .AsNoTracking()
                .SingleAsync(d => d.Id == seed.DelegationId);
            Assert.Equal(successes + 1, after.LocalAdditionCount);
        }
    }
}
