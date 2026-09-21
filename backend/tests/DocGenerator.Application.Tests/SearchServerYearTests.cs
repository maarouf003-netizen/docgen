using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس المرحلة 4: قوائم البحث (ملفات/استئنافات) يجب أن تحسب الرقم الفعّال وسنة التدوير
/// من سنة الخادم (سنة «قرار السنة») لا من «دليل الصف» (index) — فالعبور عبر
/// method group مع دالةٍ أصبحت ذات وسيطين يربط تلقائيًا بحمولة Select ذات الفهارس
/// فيخطئ كل ما يعتمد على السنة. هنا يُثبَّت زمن الخادم على 2026 ويُتحقق أن النتائج
/// لا تتأثر بموضع الصف في الصفحة.
/// </summary>
public class SearchServerYearTests : IDisposable
{
    private static readonly FakeTimeProvider Clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));

    private readonly DocGeneratorDbContext _db;
    private readonly FakeAuditLogger _audit = new();

    public SearchServerYearTests()
    {
        _db = TestDb.Create();
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = null,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
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

    /// <summary>
    /// ملفان في قائمة البحث، أحدهما دوّره المحامي في سنة سابقة (2025) بلا رقم لسنة 2026:
    /// يجب أن يعرض رقمَ الأساس 2025 وسنةَ 2025 واحتياج التدوير، أينما وقع في الصفحة —
    /// وليس وفق موقع الصف (مؤشر 0 أو 1) كما يفعله العبور الخاطئ بحمولة Select ذات الفهرس.
    /// </summary>
    [Fact]
    public async Task SearchAsync_UsesServerYearForDisplayedNumberAndNeedsRotation()
    {
        var userId = 1;
        var rotated = new Document
        {
            CreatedById = userId,
            BorrowerName = "دوّر",
            BorrowerFamily = "أول",
            IsDraft = false,
            FileNumber = "10",
            FileYear = "2020",
            ExecStatus = ExecutionStatusCatalog.None,
            CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var plain = new Document
        {
            CreatedById = userId,
            BorrowerName = "عادي",
            BorrowerFamily = "ثان",
            IsDraft = false,
            FileNumber = "77",
            FileYear = "2020",
            ExecStatus = ExecutionStatusCatalog.None,
            CreatedAt = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _db.Documents.AddRange(rotated, plain);
        _db.SaveChanges();

        _db.BaseNumbers.AddRange(
            new DocumentBaseNumber { DocumentId = rotated.Id, Year = 2024, BaseNumber = "رقم-2024", CreatedById = userId, CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new DocumentBaseNumber { DocumentId = rotated.Id, Year = 2025, BaseNumber = "رقم-2025", CreatedById = userId, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        _db.SaveChanges();

        var service = NewDocumentService();
        var page = await service.SearchAsync(null, null, null, null, null, null, null, null, null,
            page: 1, perPage: 100, visibleBranchId: null, visibleUserId: userId);

        var rotatedRow = page.Items.Single(i => i.Id == rotated.Id);
        var plainRow = page.Items.Single(i => i.Id == plain.Id);

        Assert.Equal("رقم-2025", rotatedRow.DisplayFileNumber);
        Assert.Equal("2025", rotatedRow.DisplayFileYear);
        Assert.True(rotatedRow.NeedsRotation, "الملف المدوَّر بسنة سابقة بلا رقم لسنة 2026 يجب أن يعلن احتياج التدوير");
        Assert.Equal("77", plainRow.DisplayFileNumber);
        Assert.Equal("2020", plainRow.DisplayFileYear);
        Assert.False(plainRow.NeedsRotation);
    }

    /// <summary>
    /// قائمة الاستئنافات: أهلية التدوير والرقم الفعّال تعتمدان على سنة الخادم لا موقع الصف.
    /// </summary>
    [Fact]
    public async Task AppealSearch_UsesServerYearForRotationEligibilityAndEffectiveNumber()
    {
        var userId = 1;
        var doc = new Document
        {
            CreatedById = userId,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            FileNumber = "500",
            FileYear = "2019",
            ExecStatus = ExecutionStatusCatalog.None,
            CreatedAt = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        _db.Documents.Add(doc);
        _db.SaveChanges();

        var appeal1 = new DocumentAppeal
        {
            DocumentId = doc.Id,
            CreatedById = userId,
            AssignedLawyerId = userId,
            Status = AppealStatusCatalog.Pending,
            Direction = AppealDirectionCatalog.Appellants,
        };
        var appeal2 = new DocumentAppeal
        {
            DocumentId = doc.Id,
            CreatedById = userId,
            AssignedLawyerId = userId,
            Status = AppealStatusCatalog.Pending,
            Direction = AppealDirectionCatalog.Appellants,
        };
        _db.DocumentAppeals.AddRange(appeal1, appeal2);
        _db.SaveChanges();

        _db.AppealBaseNumbers.AddRange(
            new Domain.Entities.AppealBaseNumber { AppealId = appeal1.Id, Year = 2025, BaseNumber = "A-25-1", CreatedById = userId, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Domain.Entities.AppealBaseNumber { AppealId = appeal2.Id, Year = 2025, BaseNumber = "A-25-2", CreatedById = userId, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        _db.SaveChanges();

        var service = new DocumentAppealService(
            new AppealRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            new HeadAlertService(new HeadAlertRepository(_db), new DocumentRepository(_db), new UserRepository(_db),
                new Repository<Branch>(_db), new UnitOfWork(_db), new TransactionRunner(_db), _audit),
            Clock,
            TestClock.TimeZone);

        var page = await service.SearchAsync(null, null, null, userId, page: 1, perPage: 100);

        Assert.Equal(2, page.TotalCount);
        foreach (var row in page.Items)
        {
            // مع وجود سنة سابقة (2025) وسنة الخادم 2026 بلا رقم: الاستئناف المعلّق مؤهل للتدوير.
            Assert.True(row.NeedsRotation, $"استئناف (رقم {row.Id}) يجب أن يكون مؤهلًا للتدوير بغض النظر عن موضعه");
            var expected = row.Id == appeal1.Id ? "A-25-1" : "A-25-2";
            Assert.Equal(expected, row.CurrentBaseNumber);
        }
        // رقم الملف الفعّال يُحسب بسنة الخادم: بلا أرقام أساس للملف يعود لرقمه الأصلي.
        Assert.All(page.Items, row => Assert.Equal("500", row.DocumentEffectiveNumber));
    }
}