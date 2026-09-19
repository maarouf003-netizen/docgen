using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// شارة «استئناف» في قائمة الملفات التنفيذية تعني وجود استئناف منظور فقط:
/// تظهر للمنظور، وتختفي بعد حسم/شطب آخر منظور، وتشير لأول منظور (الأقدم) عند التعدد.
/// </summary>
public class DocumentSearchAppealBadgeTests : IDisposable
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    private readonly DocGeneratorDbContext _db;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _userId;

    public DocumentSearchAppealBadgeTests()
    {
        _db = TestDb.Create();
        var user = new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = null,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(user);
        _db.SaveChanges();
        _userId = user.Id;
    }

    public void Dispose() => _db.Dispose();

    private DocumentService NewDocumentService()
    {
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        return new DocumentService(
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            baseNumbers,
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new DelegationRepository(_db),
            new AppealRepository(_db),
            new HeadAlertService(new HeadAlertRepository(_db), new DocumentRepository(_db), new UserRepository(_db), new Repository<Branch>(_db), new UnitOfWork(_db), new TransactionRunner(_db), _audit),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            Options.Create(new ExportOptions()),
            Clock,
            TestClock.TimeZone);
    }

    private Document NewDoc(string borrowerName = "أحمد")
    {
        return new Document
        {
            CreatedById = _userId,
            BorrowerName = borrowerName,
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            FileNumber = "520",
            FileYear = "2024",
            ExecStatus = ExecutionStatusCatalog.None,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
    }

    private DocumentAppeal NewAppeal(int documentId, string status)
    {
        return new DocumentAppeal
        {
            DocumentId = documentId,
            CreatedById = _userId,
            Status = status,
            Direction = AppealDirectionCatalog.Appellants,
            AppellantsJson = "[]",
            AppelleesJson = "[]",
        };
    }

    private async Task<DTOs.DocumentResponse> SearchSingleAsync(int documentId)
    {
        var service = NewDocumentService();
        var page = await service.SearchAsync(null, null, null, null, null, null, null, null, null,
            page: 1, perPage: 100, visibleBranchId: null, visibleUserId: _userId);
        return page.Items.Single(i => i.Id == documentId);
    }

    [Fact]
    public async Task SearchAsync_NoAppeal_NoBadge()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.False(row.HasAppeals);
        Assert.Null(row.MatchedAppealId);
    }

    [Fact]
    public async Task SearchAsync_PendingAppeal_ShowsBadge()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();
        var appeal = NewAppeal(doc.Id, AppealStatusCatalog.Pending);
        _db.DocumentAppeals.Add(appeal);
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.True(row.HasAppeals);
        Assert.Equal(appeal.Id, row.MatchedAppealId);
    }

    [Fact]
    public async Task SearchAsync_DecidedOnly_HidesBadge()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.DocumentAppeals.Add(NewAppeal(doc.Id, AppealStatusCatalog.Decided));
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.False(row.HasAppeals);
        Assert.Null(row.MatchedAppealId);
    }

    [Fact]
    public async Task SearchAsync_StruckOffOnly_HidesBadge()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.DocumentAppeals.Add(NewAppeal(doc.Id, AppealStatusCatalog.StruckOff));
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.False(row.HasAppeals);
        Assert.Null(row.MatchedAppealId);
    }

    [Fact]
    public async Task SearchAsync_DecidedThenPending_ShowsPendingOnly()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();
        // الأقدم محسوم، والأحدث منظور: الشارة يجب أن تشير للمنظور لا للأول مطلقًا.
        var decided = NewAppeal(doc.Id, AppealStatusCatalog.Decided);
        _db.DocumentAppeals.Add(decided);
        _db.SaveChanges();
        var pending = NewAppeal(doc.Id, AppealStatusCatalog.Pending);
        _db.DocumentAppeals.Add(pending);
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.True(row.HasAppeals);
        Assert.Equal(pending.Id, row.MatchedAppealId);
    }

    [Fact]
    public async Task SearchAsync_MultiplePending_ShowsOldestPending()
    {
        var doc = NewDoc();
        _db.Documents.Add(doc);
        _db.SaveChanges();
        var first = NewAppeal(doc.Id, AppealStatusCatalog.Pending);
        _db.DocumentAppeals.Add(first);
        _db.SaveChanges();
        var second = NewAppeal(doc.Id, AppealStatusCatalog.Pending);
        _db.DocumentAppeals.Add(second);
        _db.SaveChanges();

        var row = await SearchSingleAsync(doc.Id);

        Assert.True(row.HasAppeals);
        Assert.Equal(first.Id, row.MatchedAppealId);
    }

    [Fact]
    public async Task MapFirstAppealId_EmptyIds_ReturnsEmpty()
    {
        var repo = new AppealRepository(_db);

        var map = await repo.MapFirstAppealIdByDocumentIdsAsync(new List<int>());

        Assert.Empty(map);
    }
}
