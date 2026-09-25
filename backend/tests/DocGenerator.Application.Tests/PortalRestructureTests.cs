using System.Reflection;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;

namespace DocGenerator.Application.Tests;

/// <summary>
/// إعادة هيكلة عرض المندوب: فلتر الفرع (إجمالي/قيد)، رقم الأساس الأحدث دائمًا،
/// البطاقة الجديدة (ثلاثي + فرع + أساس/نوع/دائرة)، وتفصيل المبالغ حسب العملة.
/// </summary>
public class PortalRestructureTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPortalService _portal;
    private readonly FakeAuditLogger _audit = new();
    private readonly int _groupAId;
    private readonly int _entryDamascusId;
    private readonly int _entryAleppoId;
    private readonly int _delegateGroupId;
    private readonly int _delegateEntryId;

    public PortalRestructureTests()
    {
        _db = TestDb.Create();

        _db.Users.Add(new User { Username = "creator", FullName = "منشئ", Role = UserRole.Admin, PasswordHash = "x" });
        var groupA = new PublicEntityGroup { CanonicalName = "المصرف التجاري السوري", EntityType = PublicEntityTypeCatalog.Company };
        groupA.Entries.Add(new PublicEntity { Governorate = "دمشق", BranchName = "الفرع الرئيسي", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        groupA.Entries.Add(new PublicEntity { Governorate = "اللاذقية", BranchName = "فرع 1", Status = EntityStatusCatalog.Final, CreatedById = 1 });
        _db.PublicEntityGroups.Add(groupA);
        _db.SaveChanges();
        _groupAId = groupA.Id;
        _entryDamascusId = groupA.Entries.First(e => e.Governorate == "دمشق").Id;
        _entryAleppoId = groupA.Entries.First(e => e.Governorate == "اللاذقية").Id;

        var delegateGroup = new User { Username = "delegate_group", FullName = "مندوب المصرف", Role = UserRole.EntityManager, PortalGroupId = _groupAId, PasswordHash = "x" };
        var delegateEntry = new User { Username = "delegate_entry", FullName = "مندوب القيد", Role = UserRole.EntityManager, PortalEntryId = _entryDamascusId, PasswordHash = "x" };
        _db.Users.AddRange(delegateGroup, delegateEntry);
        _db.SaveChanges();
        _delegateGroupId = delegateGroup.Id;
        _delegateEntryId = delegateEntry.Id;

        _portal = PortalServiceFactory.Create(_db, _audit);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedDocAsync(
        string borrower, string? father, string? family,
        int registryId, string? execStatus, bool isDraft,
        string currency, decimal amount,
        string? fileType = null, string? court = null,
        string? fileNumber = null, string? fileYear = null,
        int? sourceDelegationId = null, string? execSubStatus = null,
        string? currency2 = null, decimal amount2 = 0,
        string? inclusionCurrency = null, decimal inclusionAmount = 0,
        DateTime? createdAt = null,
        string? currency3 = null, decimal amount3 = 0,
        string? inclusionCurrency2 = null, decimal inclusionAmount2 = 0,
        string? inclusionCurrency3 = null, decimal inclusionAmount3 = 0,
        string generalEntitySide = "applicant",
        string? executedStatus = null)
    {
        var doc = new Document
        {
            CreatedById = 1,
            BorrowerName = borrower,
            BorrowerFather = father,
            BorrowerFamily = family,
            IsDraft = isDraft,
            ExecStatus = execStatus ?? string.Empty,
            ExecSubStatus = execSubStatus,
            AmountNumeric = amount,
            Currency = currency,
            Amount2Numeric = amount2,
            Currency2 = currency2,
            Amount3Numeric = amount3,
            Currency3 = currency3,
            InclusionAmountNumeric = inclusionAmount,
            InclusionCurrency = inclusionCurrency,
            InclusionAmount2Numeric = inclusionAmount2,
            InclusionCurrency2 = inclusionCurrency2,
            InclusionAmount3Numeric = inclusionAmount3,
            InclusionCurrency3 = inclusionCurrency3,
            FileType = fileType,
            Court = court,
            FileNumber = fileNumber,
            FileYear = fileYear,
            SourceDelegationId = sourceDelegationId,
            GeneralEntitySide = generalEntitySide,
            ExecutedStatus = executedStatus ?? string.Empty,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        doc.ApplicantPublicEntities.Add(new ApplicantPublicEntity { Name = borrower, Governorate = "دمشق", RegistryId = registryId });
        doc.ApplicantRegistryId = registryId;
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task ListFiles_BranchFilter_ReturnsOnlySelectedEntry()
    {
        await SeedDocAsync("أحمد", "خالد", "الخطيب", _entryDamascusId, null, false, "ليرة سورية", 100);
        await SeedDocAsync("سعيد", "علي", "النور", _entryAleppoId, null, false, "ليرة سورية", 200);

        var all = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Equal(2, all.TotalCount);

        var damascus = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20, default, _entryDamascusId);
        Assert.Equal(1, damascus.TotalCount);
        Assert.Equal("أحمد", damascus.Items[0].BorrowerName);
    }

    [Fact]
    public async Task ListFiles_BranchOutsideScope_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20, default, 999999));
    }

    [Fact]
    public async Task ListFiles_CardFields_LatestBaseNumber_FileType_Court_Triple_MatchedEntries()
    {
        var id = await SeedDocAsync("أحمد", "خالد", "الخطيب", _entryDamascusId, null, false,
            "ليرة سورية", 100, fileType: "سند مصارف", court: "دائرة تنفيذ دمشق",
            fileNumber: "99", fileYear: "2026");
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = id, Year = 2025, BaseNumber = "900", CreatedById = 1,
            CreatedAt = new DateTime(2025, 5, 1), UpdatedAt = DateTime.UtcNow,
        });
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = id, Year = 2026, BaseNumber = "1500", CreatedById = 1,
            CreatedAt = new DateTime(2026, 6, 1), UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        var item = page.Items.Single(i => i.Id == id);

        // أحدث رقم أساس دائمًا مع السنة (غير مقيّد بسنة الفحص).
        Assert.Equal("1500", item.DisplayBaseNumber);
        Assert.Equal("2026", item.DisplayBaseYear);
        // نوع الملف والدائرة والثلاثي.
        Assert.Equal("سند مصارف", item.FileType);
        Assert.Equal("دائرة تنفيذ دمشق", item.Court);
        Assert.Equal("خالد", item.BorrowerFather);
        Assert.Equal("الخطيب", item.BorrowerFamily);
        // فرع الجهة المطابق.
        Assert.NotNull(item.MatchedEntries);
        Assert.Contains(item.MatchedEntries!, e => e.Id == _entryDamascusId);
    }

    [Fact]
    public async Task ListFiles_BaseNumberFallback_ToFileNumber_WhenNoBaseRows()
    {
        var id = await SeedDocAsync("بلا أساس", null, null, _entryDamascusId, null, false,
            "ليرة سورية", 50, fileNumber: "77", fileYear: "2024");

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        var item = page.Items.Single(i => i.Id == id);
        Assert.Equal("77", item.DisplayBaseNumber);
        Assert.Equal("2024", item.DisplayBaseYear);
    }

    [Fact]
    public async Task Stats_BranchFilter_ScopesCountsAndAmounts()
    {
        await SeedDocAsync("دمشق 1", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);
        await SeedDocAsync("لاذقية 1", null, null, _entryAleppoId, ExecutionStatusCatalog.Deferred, false, "دولار أمريكي", 90);

        var all = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(2, all.TotalFiles);

        var damascus = await _portal.GetStatsAsync(_delegateGroupId, default, _entryDamascusId);
        Assert.Equal(1, damascus.TotalFiles);
        Assert.Equal(1, damascus.CirculatingFiles);
        Assert.Single(damascus.PerEntry);
        Assert.Equal(_entryDamascusId, damascus.PerEntry[0].EntryId);
    }

    [Fact]
    public async Task Stats_AmountBreakdown_GroupedPerCurrency_NoCrossCurrencySum()
    {
        await SeedDocAsync("ليرة 1", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);
        await SeedDocAsync("ليرة 2", null, null, _entryDamascusId, ExecutionStatusCatalog.Deferred, false, "ليرة سورية", 250);
        await SeedDocAsync("دولار 1", null, null, _entryDamascusId, null, false, "دولار أمريكي", 90);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        Assert.NotNull(stats.AmountTotals);
        var lira = stats.AmountTotals!.First(c => c.Currency == "ليرة سورية");
        Assert.Equal(2, lira.Files);
        Assert.Equal(350m, lira.TotalAmount);
        var dollar = stats.AmountTotals!.First(c => c.Currency == "دولار أمريكي");
        Assert.Equal(1, dollar.Files);
        Assert.Equal(90m, dollar.TotalAmount);

        Assert.NotNull(stats.AmountByStatus);
        var deferred = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.Deferred);
        Assert.Equal(1, deferred.Files);
        Assert.Equal(250m, deferred.Totals.Single().TotalAmount);
        var circulating = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.StateCirculating);
        Assert.Equal(2, circulating.Files);
    }

    [Fact]
    public async Task Stats_BranchOutsideScope_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _portal.GetStatsAsync(_delegateGroupId, default, 999999));
    }

    [Fact]
    public async Task DraftWithStatus_ClassifiedByStatus_NotAsDraft_FilterMatchesCounters()
    {
        // F1: المسودة ذات الحالة تُحتسب في سلّة حالتها (مطابقة قوائم المحامين والمدير)،
        // وفلتر «تحت رفع» يشترط فراغ الحالة — فلا ازدواج بين الفلترين.
        await SeedDocAsync("مسودة متريثة", null, null, _entryDamascusId, ExecutionStatusCatalog.Deferred, true, "ليرة سورية", 100);
        await SeedDocAsync("مسودة خالصة", null, null, _entryDamascusId, null, true, "ليرة سورية", 50);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(1, stats.DeferredFiles);
        Assert.Equal(1, stats.DraftFiles);

        var drafts = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.DraftFilter, 1, 20);
        Assert.Equal(1, drafts.TotalCount);
        Assert.Equal("مسودة خالصة", drafts.Items[0].BorrowerName);

        var deferred = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.Deferred, 1, 20);
        Assert.Equal(1, deferred.TotalCount);
    }

    [Fact]
    public async Task UnknownStatusFilter_Rejected_NotSilentlyIgnored()
    {
        // M3: قيمة غريبة تُرفض (400) كقوائم المحامين — لا سقوط صامت بلا فلتر.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _portal.ListFilesAsync(_delegateGroupId, null, "حالة غريبة", 1, 20));
    }

    [Fact]
    public async Task LegacyUnknownStatus_CountedAsCirculating_BucketsReconcile()
    {
        // H2: صف إرثي بحالة غير مصنّفة يُعامل «متداولًا» — ومجموع السلّات يساوي الإجمالي دائمًا.
        await SeedDocAsync("إرثي", null, null, _entryDamascusId, "حالة إرثية قديمة", false, "ليرة سورية", 70);
        await SeedDocAsync("متداول", null, null, _entryDamascusId, null, false, "ليرة سورية", 30);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(2, stats.TotalFiles);
        Assert.Equal(2, stats.CirculatingFiles);
        Assert.Equal(stats.TotalFiles,
            stats.CirculatingFiles + stats.DeferredFiles + stats.ExecutedFiles
            + stats.ReferredToStartFiles + stats.DraftFiles);
    }

    /// <summary>
    /// ينشئ نسخة إنابة حقيقية: ملف منيب + ملف مناب مربوط بسجل إنابة فعلي
    /// (`SourceDelegationId` مفتاح أجنبي — لا قيمة وهمية).
    /// </summary>
    private async Task<int> SeedDelegationCopyAsync(
        string borrower, string? execStatus, bool isDraft, string currency, decimal amount)
    {
        // المنيب بلا أي رابط جهة — خارج كل النطاقات عمدًا حتى لا يلوّث العدّ.
        var source = new Document
        {
            CreatedById = 1, BorrowerName = "منيب " + borrower, IsDraft = false,
            ExecStatus = string.Empty, AmountNumeric = amount, Currency = currency,
            GeneralEntitySide = "applicant", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            SearchText = string.Empty,
        };
        _db.Documents.Add(source);
        await _db.SaveChangesAsync();
        var copyId = await SeedDocAsync(borrower, null, null,
            _entryDamascusId, execStatus, isDraft, currency, amount);
        var delegation = new DocumentDelegation { SourceDocumentId = source.Id, CreatedById = 1 };
        _db.DocumentDelegations.Add(delegation);
        await _db.SaveChangesAsync();
        var copy = await _db.Documents.FindAsync(copyId);
        copy!.SourceDelegationId = delegation.Id;
        await _db.SaveChangesAsync();
        return copyId;
    }

    [Fact]
    public async Task DelegationFiles_CountedNotSummed_PerB6()
    {
        // F4 (مرآة ب6): نسخ الإنابة والحالات الانتهائية تُحتسب عددًا دون مبالغ.
        await SeedDocAsync("منيب منفذ", null, null, _entryDamascusId, ExecutionStatusCatalog.ExecutedBySettlement, false, "ليرة سورية", 1000);
        await SeedDocAsync("منفذ إنابة", null, null, _entryDamascusId, ExecutionStatusCatalog.DelegationExecuted, false, "ليرة سورية", 1000);
        await SeedDocAsync("مسترد", null, null, _entryDamascusId, ExecutionStatusCatalog.Recovered, false, "ليرة سورية", 1000);
        await SeedDelegationCopyAsync("مسوّاة منابة", ExecutionStatusCatalog.ExecutedBySettlement, false, "ليرة سورية", 1000);
        await SeedDelegationCopyAsync("منابة متداولة", null, false, "ليرة سورية", 1000);
        await SeedDocAsync("متداول أصيل", null, null, _entryDamascusId, null, false, "ليرة سورية", 500);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);

        // العدّ كامل: المنفذة 4 (منيب + إنابة + مسترد + مسوّاة منابة)، والمتداولة 2.
        Assert.Equal(6, stats.TotalFiles);
        Assert.Equal(4, stats.ExecutedFiles);
        Assert.Equal(2, stats.CirculatingFiles);

        // المجاميع فوق الحاملات فقط: 1000 (منيب) + 500 (متداول أصيل).
        var lira = stats.AmountTotals!.First(c => c.Currency == "ليرة سورية");
        Assert.Equal(2, lira.Files);
        Assert.Equal(1500m, lira.TotalAmount);

        // صف «منفذ»: ملفاته = العدّاد الكامل، ومجاميعه = الحاملات فقط (المنيب وحده).
        var executed = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.ExecutedFilter);
        Assert.Equal(4, executed.Files);
        Assert.Equal(1000m, executed.Totals.Single().TotalAmount);

        // صف «متداول»: ملفاه، ومبلغ الأصيل وحده (المنابة المتداولة مستثناة).
        var circulating = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.StateCirculating);
        Assert.Equal(2, circulating.Files);
        Assert.Equal(500m, circulating.Totals.Single().TotalAmount);
    }

    [Fact]
    public async Task FilterCounts_MatchStatsBuckets_PerStatus()
    {
        // لازمة التطابق: رقم الفلتر يساوي رقم البطاقة لكل حالة.
        await SeedDocAsync("متداول", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);
        await SeedDocAsync("تريث", null, null, _entryDamascusId, ExecutionStatusCatalog.Deferred, false, "ليرة سورية", 100);
        await SeedDocAsync("منفذ", null, null, _entryDamascusId, ExecutionStatusCatalog.ExecutedBySettlement, false, "ليرة سورية", 100);
        await SeedDocAsync("محال", null, null, _entryDamascusId, ExecutionStatusCatalog.ReferredToStart, false, "ليرة سورية", 100);
        await SeedDocAsync("مسودة", null, null, _entryDamascusId, null, true, "ليرة سورية", 100);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        var cases = new[]
        {
            (ExecutionStatusCatalog.StateCirculating, stats.CirculatingFiles),
            (ExecutionStatusCatalog.Deferred, stats.DeferredFiles),
            (ExecutionStatusCatalog.ExecutedFilter, stats.ExecutedFiles),
            (ExecutionStatusCatalog.ReferredToStart, stats.ReferredToStartFiles),
            (ExecutionStatusCatalog.DraftFilter, stats.DraftFiles),
        };
        foreach (var (status, expected) in cases)
        {
            var page = await _portal.ListFilesAsync(_delegateGroupId, null, status, 1, 20);
            Assert.Equal(expected, page.TotalCount);
        }
    }

    [Fact]
    public async Task Export_WithBranchFilter_Succeeds_AndOutsideScope_ThrowsUnauthorized()
    {
        await SeedDocAsync("دمشق", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);

        var bytes = await _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "tester", default, _entryDamascusId);
        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _portal.ExportWorkbookAsync(_delegateGroupId, null, null, "tester", default, 999999));
    }

    [Fact]
    public async Task LatestBaseNumber_Pinned_RegardlessOfYear()
    {
        // N1: انحراف موثّق — الأحدث دائمًا حتى بسنة مستقبلية (بخلاف قوائم المحامين).
        var id = await SeedDocAsync("مستقبلي", null, null, _entryDamascusId, null, false,
            "ليرة سورية", 100, fileNumber: "99", fileYear: "2026");
        var futureYear = DateTime.UtcNow.Year + 2;
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = id, Year = futureYear, BaseNumber = "7777", CreatedById = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        var item = page.Items.Single(i => i.Id == id);
        Assert.Equal("7777", item.DisplayBaseNumber);
        Assert.Equal(futureYear.ToString(), item.DisplayBaseYear);
    }

    [Fact]
    public async Task PartiallyExecuted_IsAlwaysCirculating_CounterMatchesFilter()
    {
        // قرار المالك: «منفذ جبريا + منفذ جزئيا» متداول دائمًا (بوابة ومحامين ومدير).
        // وصف `ExecSubStatus` الفارغ (صفوف قديمة) يُعامل منفذًا كاملًا — مطابقة C# لا SQL الثلاثي.
        await SeedDocAsync("جزئي", null, null, _entryDamascusId,
            ExecutionStatusCatalog.ExecutedForcibly, false, "ليرة سورية", 1000,
            execSubStatus: ExecutionStatusCatalog.SubPartiallyExecuted);
        await SeedDocAsync("كامل", null, null, _entryDamascusId,
            ExecutionStatusCatalog.ExecutedForcibly, false, "ليرة سورية", 2000,
            execSubStatus: ExecutionStatusCatalog.SubFullyExecuted);
        await SeedDocAsync("قديم بلا فرعي", null, null, _entryDamascusId,
            ExecutionStatusCatalog.ExecutedForcibly, false, "ليرة سورية", 3000,
            execSubStatus: null);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(3, stats.TotalFiles);
        Assert.Equal(1, stats.CirculatingFiles);
        Assert.Equal(2, stats.ExecutedFiles);

        var executed = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.ExecutedFilter, 1, 20);
        Assert.Equal(stats.ExecutedFiles, executed.TotalCount);
        var circulating = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.StateCirculating, 1, 20);
        Assert.Equal(stats.CirculatingFiles, circulating.TotalCount);
        Assert.Equal("جزئي", circulating.Items[0].BorrowerName);

        // مبلغ الجزئي في سلّة المتداول لا المنفذ.
        var circulatingRow = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.StateCirculating);
        Assert.Equal(1000m, circulatingRow.Totals.Single().TotalAmount);
        var executedRow = stats.AmountByStatus!.First(s => s.Status == ExecutionStatusCatalog.ExecutedFilter);
        Assert.Equal(5000m, executedRow.Totals.Single().TotalAmount);
    }

    [Fact]
    public async Task LegacyUnknownStatus_FoldedIntoCirculating_FilterMatchesCounter()
    {
        // قرار المالك: الإرثي يُطوى «متداولًا» في العدّاد والفلتر معًا — لا عدّاد بلا فلتر.
        await SeedDocAsync("إرثي", null, null, _entryDamascusId, "قيد المعالجة", false, "ليرة سورية", 100);
        await SeedDocAsync("مسودة إرثية", null, null, _entryDamascusId, "قيد المعالجة", true, "ليرة سورية", 50);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(2, stats.TotalFiles);
        Assert.Equal(2, stats.CirculatingFiles);
        Assert.Equal(0, stats.DraftFiles);

        // لازمة التطابق الدقيقة التي فاتت المراجعة السابقة: الفلتر == العدّاد حتى مع الإرثي.
        var circulating = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.StateCirculating, 1, 20);
        Assert.Equal(stats.CirculatingFiles, circulating.TotalCount);
        var drafts = await _portal.ListFilesAsync(_delegateGroupId, null, ExecutionStatusCatalog.DraftFilter, 1, 20);
        Assert.Equal(0, drafts.TotalCount);
    }

    [Fact]
    public async Task SixPairs_GroupedByCurrency_ZeroSkipped()
    {
        // قرار المالك: الأزواج الستة (المبلغ×3 + الإدراج×3) حسب عملة كلٍّ منها.
        // الصفري يُسقط فلا ملف وهمي في العملة الافتراضية — والملف الصفري كليًا
        // يُحتسب عددًا في سلّته دون أي مجموع.
        await SeedDocAsync("متعدد", null, null, _entryDamascusId, null, false,
            "ليرة سورية", 1000, currency2: "دولار أمريكي", amount2: 50,
            inclusionCurrency: "ليرة سورية", inclusionAmount: 200);
        await SeedDocAsync("صفري", null, null, _entryDamascusId, null, false, "ليرة سورية", 0);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(2, stats.TotalFiles);
        Assert.Equal(2, stats.CirculatingFiles);

        var lira = stats.AmountTotals!.Single(c => c.Currency == "ليرة سورية");
        Assert.Equal(1200m, lira.TotalAmount);
        Assert.Equal(1, lira.Files);
        var dollar = stats.AmountTotals!.Single(c => c.Currency == "دولار أمريكي");
        Assert.Equal(50m, dollar.TotalAmount);
        Assert.Equal(1, dollar.Files);
        Assert.Equal(2, stats.AmountTotals!.Count);
    }

    [Fact]
    public async Task DetailResponse_ScrubsUnconsumedInternalFields_KeepsConsumedOnes()
    {
        // ق7 سلكيًا: كل حقل لا تستهلكه واجهة التفاصيل يُحجب — والحارس يفشل عند أي تسرب لاحق.
        var id = await SeedDocAsync("محجوب", "أب", "عائلة", _entryDamascusId, null, false,
            "ليرة سورية", 100, fileType: "سند مصارف", court: "دائرة تنفيذ دمشق");
        var branch = new Branch { Name = "فرع الإدارة" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        var doc = await _db.Documents.FindAsync(id);
        doc!.ViewCount = 7;
        doc.PrintCount = 3;
        doc.Lawyer = "محامٍ داخلي";
        doc.Branch = branch;
        await _db.SaveChangesAsync();

        var response = await _portal.GetFileAsync(_delegateGroupId, id, "tester");
        Assert.NotNull(response);
        Assert.Null(response.Notes);
        Assert.Null(response.ImmediateActions);
        Assert.Equal(0, response.ViewCount);
        Assert.Equal(0, response.PrintCount);
        Assert.Null(response.AdministrativeBranchName);
        Assert.Null(response.BranchId);
        Assert.Equal(0, response.CreatedById);
        Assert.Null(response.CreatedByName);
        Assert.Null(response.Lawyer);
        Assert.False(response.NeedsRotation);
        Assert.False(response.HasAppeals);
        Assert.Null(response.MatchedAppealId);
        Assert.Null(response.SourceDelegationId);
        Assert.NotNull(response.Assignments);

        // لا إفراط في الحجب: المستهلك يبقى.
        Assert.Equal("محجوب", response.BorrowerName);
        Assert.Equal("سند مصارف", response.FileType);
        Assert.Equal("دائرة تنفيذ دمشق", response.Court);
    }

    [Fact]
    public async Task AppealsBreakdown_ExplicitSets_UnknownIsPending()
    {
        // المغلق = محسوم/مشطوب حصرًا؛ أي حالة مستقبلية معلّقة ظاهرة لا مدفونة.
        var inScope = await SeedDocAsync("داخل", null, null, _entryDamascusId, null, false, "ليرة سورية", 10);
        var outScope = new Document
        {
            CreatedById = 1, BorrowerName = "خارج", IsDraft = false, ExecStatus = string.Empty,
            AmountNumeric = 10, GeneralEntitySide = "applicant", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            SearchText = "خارج",
        };
        _db.Documents.Add(outScope);
        await _db.SaveChangesAsync();
        _db.DocumentAppeals.AddRange(
            new DocumentAppeal { DocumentId = inScope, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Pending, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = inScope, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Decided, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = inScope, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.StruckOff, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = inScope, Direction = AppealDirectionCatalog.Appellants, Status = "حالة مستقبلية", AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 },
            new DocumentAppeal { DocumentId = outScope.Id, Direction = AppealDirectionCatalog.Appellants, Status = AppealStatusCatalog.Pending, AppellantsJson = "[]", AppelleesJson = "[]", CreatedById = 1 });
        await _db.SaveChangesAsync();

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(2, stats.PendingAppeals);
        Assert.Equal(2, stats.ClosedAppeals);
    }

    [Fact]
    public async Task MonthlySeries_AnchoredToInjectedClock()
    {
        // السلسلة حتى الشهر الحالي بتوقيت النظام المحقون — لا `DateTime.UtcNow` مباشرة.
        var fakeClock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var portal = PortalServiceFactory.Create(_db, _audit, clock: fakeClock);

        await SeedDocAsync("يونيو", null, null, _entryDamascusId, null, false, "ليرة سورية", 10,
            createdAt: new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc));
        await SeedDocAsync("أبريل", null, null, _entryDamascusId, null, false, "ليرة سورية", 10,
            createdAt: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));

        var stats = await portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(12, stats.Monthly.Count);
        Assert.Equal((2025, 7), (stats.Monthly[0].Year, stats.Monthly[0].Month));
        Assert.Equal((2026, 6), (stats.Monthly[11].Year, stats.Monthly[11].Month));
        Assert.Equal(1, stats.Monthly[11].Files);
        Assert.Equal(1, stats.Monthly[9].Files);
        Assert.Equal(0, stats.Monthly[10].Files);
    }

    [Fact]
    public async Task ListItem_DisplayStatus_FromSingleSource()
    {
        // الشارة من `DocumentStatusResolver` — لا تصنيف في الواجهة.
        await SeedDocAsync("جبري", null, null, _entryDamascusId,
            ExecutionStatusCatalog.ExecutedForcibly, false, "ليرة سورية", 10);
        await SeedDocAsync("جزئي", null, null, _entryDamascusId,
            ExecutionStatusCatalog.ExecutedForcibly, false, "ليرة سورية", 10,
            execSubStatus: ExecutionStatusCatalog.SubPartiallyExecuted);
        await SeedDocAsync("مسترد", null, null, _entryDamascusId,
            ExecutionStatusCatalog.Recovered, false, "ليرة سورية", 10);

        var page = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        var byName = page.Items.ToDictionary(i => i.BorrowerName!);
        Assert.Equal("منفذ", byName["جبري"].DisplayStatus);
        Assert.Equal("متداول / منفذ جزئيا", byName["جزئي"].DisplayStatus);
        Assert.Equal("مسترد", byName["مسترد"].DisplayStatus);
    }

    [Fact]
    public async Task EntryDelegate_SeesOnlyOwnEntry_AndRejectsOtherEntry()
    {
        await SeedDocAsync("دمشق", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);
        await SeedDocAsync("لاذقية", null, null, _entryAleppoId, null, false, "ليرة سورية", 200);

        var stats = await _portal.GetStatsAsync(_delegateEntryId);
        Assert.Equal(1, stats.TotalFiles);

        var files = await _portal.ListFilesAsync(_delegateEntryId, null, null, 1, 20);
        Assert.Equal(1, files.TotalCount);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _portal.GetStatsAsync(_delegateEntryId, default, _entryAleppoId));
    }

    [Fact]
    public async Task H1_StruckOffExecutedSide_ExcludedFromListStatsAndCirculating()
    {
        // عائلة «منفذ عليه» المشطوبة تسجّل شطبها في ExecutedStatus لا ExecStatus —
        // يجب استبعادها من القائمة والعدّادات والتصدير (كقائمة المحامين)، لا احتسابها «متداولًا».
        var struckId = await SeedDocAsync("مشطوب", null, null, _entryDamascusId, null, false,
            "ليرة سورية", 100, generalEntitySide: "executed",
            executedStatus: ExecutedStatusCatalog.StruckOff);
        AttachExecutedPartyOnly(struckId, _entryDamascusId);
        var liveId = await SeedDocAsync("حي", null, null, _entryDamascusId, null, false,
            "ليرة سورية", 100, generalEntitySide: "executed",
            executedStatus: ExecutedStatusCatalog.Executed);
        AttachExecutedPartyOnly(liveId, _entryDamascusId);

        var list = await _portal.ListFilesAsync(_delegateGroupId, null, null, 1, 20);
        Assert.Contains(list.Items, f => f.Id == liveId); // شاهد: الرؤية عبر فرع المنفذ تعمل
        Assert.DoesNotContain(list.Items, f => f.Id == struckId);

        var circulating = await _portal.ListFilesAsync(
            _delegateGroupId, null, ExecutionStatusCatalog.StateCirculating, 1, 20);
        Assert.DoesNotContain(circulating.Items, f => f.Id == struckId);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(1, stats.TotalFiles);
        Assert.Equal(1, stats.CirculatingFiles);
    }

    /// <summary>
    /// يجرّد الملف من فرع الطالب ويعلّقه بفرع المنفذ وحده — فتأتي رؤيته في البوابة
    /// من فرع «منفذ عليه» حصرًا (أمانة الاختبار تقتضي عدم الالتفاف عبر فرع الطالب).
    /// </summary>
    private void AttachExecutedPartyOnly(int docId, int registryId)
    {
        var registry = _db.PublicEntities.Find(registryId)!;
        var doc = _db.Documents.Find(docId)!;
        doc.ApplicantPublicEntities.Clear();
        doc.ApplicantRegistryId = null;
        doc.ExecutedPublicEntities.Add(new ExecutedPublicEntity
        {
            EntityName = "جهة منفذ عليها",
            Governorate = "دمشق",
            RegistryId = registryId,
            Registry = registry,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task H2_DetailResponse_ScrubsRemainingUnconsumedFields()
    {
        // SoldAssetIds/ForcedExecutionDate/GeneralEntitySideLabel: لا تستهلكها أي بطاقة
        // بوابة (مُدقَّقة ضد PortalFileDetail وكل المشتركات المعروضة فيه) — فتُحجب سلكيًا.
        var id = await SeedDocAsync("متبق", null, null, _entryDamascusId, null, false, "ليرة سورية", 100);
        var doc = await _db.Documents.FindAsync(id);
        doc!.SoldAssetIds = "[7]";
        doc.ForcedExecutionDate = "1/1/2024";
        await _db.SaveChangesAsync();

        var response = await _portal.GetFileAsync(_delegateGroupId, id, "tester");
        Assert.NotNull(response);
        Assert.Empty(response.SoldAssetIds);
        Assert.Null(response.ForcedExecutionDate);
        Assert.Null(response.GeneralEntitySideLabel);
        Assert.Null(response.DeletedAt);
        Assert.Null(response.BranchName);
    }

    [Fact]
    public async Task SixPairs_AllSixPairsGroupedPerCurrency_ZeroSkipped()
    {
        // قرار المالك: الأزواج الستة كلها — هذا الاختبار يلمسها جميعًا (السابق غطّى ثلاثة فقط).
        await SeedDocAsync("ستة", null, null, _entryDamascusId, null, false,
            "ليرة", 100, currency2: "دولار", amount2: 50,
            inclusionCurrency: "ليرة", inclusionAmount: 200,
            currency3: "يورو", amount3: 30,
            inclusionCurrency2: "دولار", inclusionAmount2: 5,
            inclusionCurrency3: "دينار", inclusionAmount3: 7);

        var stats = await _portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(4, stats.AmountTotals!.Count);
        Assert.Equal(300m, stats.AmountTotals.Single(c => c.Currency == "ليرة").TotalAmount);
        Assert.Equal(55m, stats.AmountTotals.Single(c => c.Currency == "دولار").TotalAmount);
        Assert.Equal(30m, stats.AmountTotals.Single(c => c.Currency == "يورو").TotalAmount);
        Assert.Equal(7m, stats.AmountTotals.Single(c => c.Currency == "دينار").TotalAmount);
        Assert.All(stats.AmountTotals, c => Assert.Equal(1, c.Files));
    }

    [Fact]
    public async Task MonthlySeries_MonthBoundaryRespectsLocalZone()
    {
        // 2026-06-30T22:00Z هو حزيران بتوقيت UTC لكن تموز محليًا (+3):
        // يُحتسب في تموز — الحالة التي وُجد التحويل المحلي لأجلها.
        var fakeClock = new FakeTimeProvider(new DateTimeOffset(2026, 7, 1, 0, 30, 0, TimeSpan.Zero));
        var portal = PortalServiceFactory.Create(_db, _audit, clock: fakeClock);
        await SeedDocAsync("حدّ", null, null, _entryDamascusId, null, false, "ليرة سورية", 10,
            createdAt: new DateTime(2026, 6, 30, 22, 0, 0, DateTimeKind.Utc));

        var stats = await portal.GetStatsAsync(_delegateGroupId);
        Assert.Equal(1, stats.Monthly.Single(m => m.Year == 2026 && m.Month == 7).Files);
        Assert.Equal(0, stats.Monthly.Single(m => m.Year == 2026 && m.Month == 6).Files);
    }

    [Fact]
    public void PortalDetailResponse_EveryPropertyHasExplicitDisposition()
    {
        // الحارس الحقيقي (H2): أي حقل جديد في `DocumentResponse` بلا تصريف صريح
        // يُفشل هذا الاختبار — فيُجبَر المطوّر على قرار «يُحجب أم يُعرض» بدل التسرب الصامت.
        // - محجوب: تُصفّره `PortalService.GetFileAsync` (ويغطيه اختبار التنقية سلوكيًا).
        // - مستهلك: تثبت قراءته في `PortalFileDetail` أو مشترك معروض فيه (دُقِّق حقلًا بحقل).
        // - مقبول: غير مستهلك لكنه بلا حساسية وموثّق السبب هنا حصرًا.
        var scrubbed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Notes", "ImmediateActions", "ViewCount", "PrintCount",
            "AdministrativeBranchName", "BranchId", "CreatedById", "CreatedByName",
            "Lawyer", "NeedsRotation", "HasAppeals", "MatchedAppealId",
            "SourceDelegationId", "SoldAssetIds", "ForcedExecutionDate",
            "GeneralEntitySideLabel", "DeletedAt", "BranchName",
        };
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Id", "CreatedAt", "UpdatedAt", "DocumentType", "IsDraft",
            "BorrowerName", "BorrowerFather", "BorrowerFamily", "BorrowerMother",
            "BorrowerBirth", "BorrowerRegister", "BorrowerNationalId",
            "BorrowerAddress", "BorrowerAddressType",
            "BorrowerRepresentativeName", "BorrowerRepresentativeFather",
            "BorrowerRepresentativeFamily", "BorrowerRepresentativeCapacity",
            "BorrowerRepresentativeAddressType", "BorrowerRepresentativeAddress",
            "BorrowerNature", "BorrowerRegistrationNumber", "BorrowerRepresentedBy",
            "ContractType", "ContractTypeSelector", "ContractNumber", "ContractDate",
            "AnnexType", "AnnexNumber", "AnnexDate", "InclusionText",
            "AmountNumeric", "AmountWords", "Currency",
            "Amount2Numeric", "Amount2Words", "Currency2",
            "Amount3Numeric", "Amount3Words", "Currency3",
            "InclusionAmountNumeric", "InclusionAmountWords", "InclusionCurrency",
            "InclusionAmount2Numeric", "InclusionAmount2Words", "InclusionCurrency2",
            "InclusionAmount3Numeric", "InclusionAmount3Words", "InclusionCurrency3",
            "Court", "Applicant",
            "ReferredFromLawyer", "ReferredAt",
            "FileNumber", "DisplayFileNumber", "DisplayFileYear",
            "FileType", "FileYear", "FileIncoming", "FileIncomingDate",
            "UnderFilingNumber", "FileRegistrationDate",
            "DisplayStatus", "ExecStatus", "ExecSubStatus",
            "CollectedAmount", "CollectedAmount2", "CollectedAmount3",
            "CollectedCurrency", "CollectedCurrency2", "CollectedCurrency3",
            "GeneralEntitySide",
            "ExecutedStatus", "ExecutedDescription",
            "FileReceiptDate", "FileReceiptNumber",
            "ExecutedRequiredAmount", "ExecutedRequiredCurrency",
            "ExecutedRequiredAmount2", "ExecutedRequiredCurrency2",
            "ExecutedRequiredAmount3", "ExecutedRequiredCurrency3",
            "ExecutedPaidAmount", "ExecutedPaidCurrency",
            "ExecutedPaidAmount2", "ExecutedPaidCurrency2",
            "ExecutedPaidAmount3", "ExecutedPaidCurrency3",
            "ExecutedDepositDate", "ExecutedExecutionDate",
            "ForcibleTransferDate", "ForcibleTransferNoticeNumber",
            "StruckOffDate",
            "RenewalFileReceiptNumber", "RenewalFileReceiptDate",
            "RenewalFileNumber", "RenewalFileType", "RenewalDate",
            "NoFundsDemandNumber", "NoFundsDemandDate",
            "StartReferralNumber", "StartReferralDate",
            "BaraetNumber", "BaraetDate", "BaraetRegNumber", "BaraetRegDate",
            "TarithNumber", "TarithDate", "TarithRegNumber", "TarithRegDate",
            "SayerNumber", "SayerDate", "SayerRegNumber", "SayerRegDate",
            "SeizureDate",
            "Guarantors", "Assets", "BorrowerHeirs", "ExecutionActions",
            "ExecutionApplicants", "ExecutedPublicEntities", "ExecutedNaturalPersons",
            "ApplicantPublicEntities", "Assignments",
            "FileArrivalNumber", "FileArrivalDate",
            "Occurrences",
        };
        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            // مفتاح تسريع خلفي مشتق (لا يقرؤه العرض): معرّفات القيود مكشوفة أصلًا
            // عبر النطاق والقيود المطابقة — فلا حساسية إضافية.
            "ApplicantRegistryId",
        };
        var undisposed = typeof(DocumentResponse).GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !scrubbed.Contains(n) && !consumed.Contains(n) && !accepted.Contains(n))
            .ToList();
        Assert.Empty(undisposed);
    }

    [Fact]
    public void PortalListItem_EveryPropertyHasExplicitDisposition()
    {
        // G1: عقد القائمة (`PortalFileListItemDto`) مسار مستقل عن التفاصيل —
        // أي حقل جديد فيه بلا تصريف صريح يُفشل هذا الاختبار. دُقّق الاستهلاك
        // حقلًا بحقل ضد `PortalFileCard` (المستهلك الإنتاجي الوحيد):
        // - مستهلك: يُقرأ في البطاقة (الاسم/الشارة/الأساس/النوع/الدائرة/القيود).
        // - مقبول: غير مستهلك لكنه بيانات القضية نفسها الظاهرة في التفاصيل أيضًا
        //   (القائمة عرض جزئي لملفات النطاق نفسها) — فلا حساسية إضافية، ويُوثَّق هنا حصرًا.
        var consumed = new HashSet<string>(StringComparer.Ordinal)
        {
            "Id", "DocumentType", "IsDraft",
            "BorrowerName", "BorrowerFather", "BorrowerFamily",
            "ExecStatus", "DisplayStatus",
            "FileType", "Court", "DisplayBaseNumber", "DisplayBaseYear",
            "MatchedEntries",
        };
        var accepted = new HashSet<string>(StringComparer.Ordinal)
        {
            // طالب التنفيذ وملخص المنفذ عليه والمبلغ وعملته وتاريخا القيد —
            // كلها ظاهرة في صفحة التفاصيل للملف نفسه.
            "Applicant", "ExecutedEntitiesSummary",
            "AmountNumeric", "Currency",
            "CreatedAt", "UpdatedAt",
        };
        var undisposed = typeof(PortalFileListItemDto).GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => !consumed.Contains(n) && !accepted.Contains(n))
            .ToList();
        Assert.Empty(undisposed);

        // القيود المطابقة عقد متداخل: أي حقل جديد فيه يستوجب مراجعة عرض صريحة.
        var entryProps = typeof(PortalScopeEntryDto).GetProperties(
                BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(entryProps.SetEquals(
            new HashSet<string>(StringComparer.Ordinal) { "Id", "Governorate", "BranchName", "IsActive" }),
            "حقل جديد في PortalScopeEntryDto — راجع عرضه في PortalFileCard أولًا");
    }
}
