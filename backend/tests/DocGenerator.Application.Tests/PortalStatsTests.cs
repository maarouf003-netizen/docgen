using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>اختبارات إحصاءات بوابة الجهة (المرحلة 4): عزل النطاق وتطابق التصنيف مع فلاتر القائمة.</summary>
public class PortalStatsTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPortalService _portal;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _groupAId;
    private readonly int _entryAId;
    private readonly int _entryBId;
    private readonly int _delegateGroupId;

    public PortalStatsTests()
    {
        _db = TestDb.Create();

        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        var groupA = new PublicEntityGroup { CanonicalName = "وزارة التعليم", EntityType = PublicEntityTypeCatalog.Ministry };
        groupA.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        groupA.Entries.Add(new PublicEntity { Governorate = "حلب", BranchName = "فرع حلب", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        var groupB = new PublicEntityGroup { CanonicalName = "مديرية النقل", EntityType = PublicEntityTypeCatalog.Administration };
        groupB.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "فرع النقل", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.AddRange(groupA, groupB);
        _db.SaveChanges();
        _groupAId = groupA.Id;
        _entryAId = groupA.Entries.First(e => e.Governorate == "دمشق").Id;
        _entryBId = groupB.Entries.First().Id;

        var delegateGroup = new User { Username = "delegate_group", FullName = "مندوب الوزارة", Role = UserRole.EntityManager, PortalGroupId = _groupAId, PasswordHash = "x" };
        _db.Users.Add(delegateGroup);
        _db.SaveChanges();
        _delegateGroupId = delegateGroup.Id;

        _portal = new PortalService(
            new PortalRepository(_db),
            new Repository<Document>(_db),
            new AppealRepository(_db),
            new ExcelExportService(),
            _audit,
            Options.Create(new ExportOptions { MaxRows = 10_000 }));
    }

    public void Dispose() => _db.Dispose();

    /// <summary>ينشئ ملف طالب مرتبطًا بقيد من نطاق الوزارة بحالة ووقت إنشاء محددين.</summary>
    private async Task<int> SeedApplicantDocAsync(string name, string? execStatus, bool isDraft, DateTime createdAt,
        string currency = "ليرة سورية", decimal amount = 100m, int? registryId = null)
    {
        // الارتباط الافتراضي بقيد دمشق ضمن نطاق المندوب ما لم يُمرَّر غيره.
        var linkedEntryId = registryId ?? _entryAId;
        var doc = new Document
        {
            CreatedById = 1,
            BorrowerName = name,
            IsDraft = isDraft,
            ExecStatus = execStatus ?? string.Empty,
            AmountNumeric = amount,
            Currency = currency,
            GeneralEntitySide = "applicant",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = name, Governorate = "دمشق", RegistryId = linkedEntryId });
        doc.ApplicantRegistryId = linkedEntryId;
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task Stats_ClassifyStatuses_ExactlyLikeListFilters()
    {
        var now = DateTime.UtcNow.AddMonths(-1);
        await SeedApplicantDocAsync("متداول أ", null, isDraft: false, now);
        await SeedApplicantDocAsync("مسودة ب", null, isDraft: true, now);
        await SeedApplicantDocAsync("منفذ ج", ExecutionStatusCatalog.ExecutedBySettlement, false, now);
        await SeedApplicantDocAsync("منفذ د", ExecutionStatusCatalog.DelegationExecuted, false, now);
        await SeedApplicantDocAsync("تريث هـ", ExecutionStatusCatalog.Deferred, false, now);
        // مشطوب: مستبعد من القائمة والإحصاء معًا.
        var struck = new Document { CreatedById = 1, BorrowerName = "مشطوب", IsDraft = false, ExecStatus = ExecutionStatusCatalog.StateStruckOff, AmountNumeric = 10, GeneralEntitySide = "applicant" };
        _db.Documents.Add(struck);
        await _db.SaveChangesAsync();

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.Equal(5, stats.TotalFiles); // المشطوب خارج الحساب
        Assert.Equal(1, stats.DraftFiles);
        Assert.Equal(1, stats.CirculatingFiles);
        Assert.Equal(2, stats.ExecutedFiles);
        Assert.Equal(1, stats.DeferredFiles);
    }

    [Fact]
    public async Task Stats_Isolation_OnlyScopeEntriesCounted()
    {
        var now = DateTime.UtcNow.AddMonths(-2);
        await SeedApplicantDocAsync("داخل النطاق", null, false, now, registryId: _entryAId);
        // ملف مرتبط بهوية أخرى (مديرية النقل) لا يدخل إحصاء مندوب الوزارة.
        var foreign = new Document
        {
            CreatedById = 1, BorrowerName = "خارج النطاق", IsDraft = false, ExecStatus = "",
            AmountNumeric = 500, GeneralEntitySide = "executed",
        };
        foreign.ExecutedPublicEntities.Add(new ExecutedPublicEntity { EntityName = "مديرية النقل", EntityNature = "public", RegistryId = _entryBId });
        _db.Documents.Add(foreign);
        await _db.SaveChangesAsync();

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.Equal(1, stats.TotalFiles);
        // كل قيود النطاق تظهر (حتى الصفري منها)؛ قيد الهوية الأخرى لا يظهر إطلاقًا.
        Assert.Equal(2, stats.PerEntry.Count);
        var damascus = stats.PerEntry.Single(e => e.EntryId == _entryAId);
        Assert.Equal(1, damascus.Files);
        var aleppo = stats.PerEntry.Single(e => e.Governorate == "حلب");
        Assert.Equal(0, aleppo.Files);
        Assert.DoesNotContain(stats.PerEntry, e => e.EntryId == _entryBId);
    }

    [Fact]
    public async Task Stats_MonthlySeries_IsTwelveConnectedBuckets_IncludingZeroMonths_AndCountsThisMonth()
    {
        var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 15, 0, 0, 0, DateTimeKind.Utc);
        var elevenMonthsAgo = thisMonth.AddMonths(-11).AddDays(3);
        await SeedApplicantDocAsync("هذا الشهر", null, false, thisMonth, registryId: _entryAId);
        await SeedApplicantDocAsync("قبل أحد عشر شهرًا", null, false, elevenMonthsAgo, registryId: _entryAId);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.Equal(12, stats.Monthly.Count);
        Assert.Equal((elevenMonthsAgo.Year, elevenMonthsAgo.Month), (stats.Monthly[0].Year, stats.Monthly[0].Month));
        Assert.Equal(1, stats.Monthly[0].Files);
        Assert.Equal(thisMonth.Month, stats.Monthly[^1].Month);
        Assert.Equal(1, stats.Monthly[^1].Files);
        Assert.All(stats.Monthly.Skip(1).Take(10), m => Assert.Equal(0, m.Files));
    }

    [Fact]
    public async Task Stats_PerEntry_MayDoubleCount_FileLinkedToTwoEntries_OfSameGroup()
    {
        var secondEntryId = _db.PublicEntities.AsNoTracking().Single(e => e.Governorate == "حلب").Id;
        var doc = new Document
        {
            CreatedById = 1, BorrowerName = "ملف بطرفين", IsDraft = false, ExecStatus = "", AmountNumeric = 0,
            GeneralEntitySide = "applicant",
        };
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = "وزارة التعليم", Governorate = "دمشق", RegistryId = _entryAId });
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = "وزارة التعليم", Governorate = "حلب", RegistryId = secondEntryId });
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        // توزيع ارتباط لا تجزئة حصرية (موثّق): الملف يُحتسب تحت كل قيد ارتبط به.
        Assert.Equal(2, stats.PerEntry.Count);
        Assert.All(stats.PerEntry, e => Assert.Equal(1, e.Files));
        Assert.Equal(2, stats.PerEntry.Sum(e => e.Files));
        Assert.Equal(1, stats.TotalFiles);
    }

    [Fact]
    public async Task Stats_TopCurrencies_GroupPerCurrency_NoCrossCurrencySum()
    {
        var now = DateTime.UtcNow.AddDays(-3);
        await SeedApplicantDocAsync("ليرة 1", null, false, now, currency: "ليرة سورية", amount: 100, registryId: _entryAId);
        await SeedApplicantDocAsync("ليرة 2", null, false, now, currency: "ليرة سورية", amount: 250, registryId: _entryAId);
        await SeedApplicantDocAsync("دولار 1", null, false, now, currency: "دولار أمريكي", amount: 90, registryId: _entryAId);
        await SeedApplicantDocAsync("بلا عملة", null, false, now, currency: "", amount: 7, registryId: _entryAId);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.Equal(4, stats.TotalFiles);
        var lira = stats.TopCurrencies.First(c => c.Currency == "ليرة سورية");
        Assert.Equal(2, lira.Files);
        Assert.Equal(350m, lira.TotalAmount);
        Assert.Contains(stats.TopCurrencies, c => c.Currency == "غير محددة" && c.Files == 1);
        Assert.DoesNotContain(stats.TopCurrencies, c => c.Currency == "ليرة سورية" && c.TotalAmount == 357m);
    }

    [Fact]
    public async Task Stats_AppealsBreakdown_PendingVsClosed_WithinScopeOnly()
    {
        var inScopeDoc = await SeedApplicantDocAsync("ملف باستئناف", null, false, DateTime.UtcNow.AddDays(-1), registryId: _entryAId);
        var outScopeDoc = new Document
        {
            CreatedById = 1, BorrowerName = "خارج النطاق", IsDraft = false, ExecStatus = "", AmountNumeric = 0,
            GeneralEntitySide = "executed",
        };
        outScopeDoc.ExecutedPublicEntities.Add(new ExecutedPublicEntity { EntityName = "مديرية النقل", EntityNature = "public", RegistryId = _entryBId });
        _db.Documents.Add(outScopeDoc);
        await _db.SaveChangesAsync();

        _db.DocumentAppeals.AddRange(
            new DocumentAppeal { DocumentId = inScopeDoc, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Pending, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = inScopeDoc, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Decided, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = outScopeDoc.Id, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Pending, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 });
        await _db.SaveChangesAsync();

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.Equal(1, stats.PendingAppeals);  // استئناف الخارج عن النطاق غير محسوب
        Assert.Equal(1, stats.ClosedAppeals);
    }
}
