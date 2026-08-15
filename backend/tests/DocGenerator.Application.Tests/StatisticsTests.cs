using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class StatisticsRepositoryTests : IDisposable
{
    /// <summary>تاريخ إدخال قديم ثابت للوثائق الأولية بلا تاريخ قيد، كي لا تلوّث نوافذ الفترات الحالية.</summary>
    private static readonly DateTime LegacyDate = new(2020, 1, 15);

    private readonly DocGeneratorDbContext _db;
    private readonly IStatisticsRepository _stats;

    public StatisticsRepositoryTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _db.Users.Add(new User { Username = "u1", FullName = "مستخدم 1", Role = UserRole.Lawyer, BranchId = branch.Id });
        _db.SaveChanges();
        _db.Documents.AddRange(
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = true, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 0, ExecStatus = string.Empty, CreatedAt = LegacyDate },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 500, ExecStatus = "منفذ بالتسوية", CollectedAmount = 200, CreatedAt = LegacyDate },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "سامر", BorrowerFamily = "حسن", AmountNumeric = 700, ExecStatus = "تريث", CreatedAt = LegacyDate },
            new Document { BranchId = null, CreatedById = 1, IsDraft = false, BorrowerName = "بلا فرع", BorrowerFamily = "س", AmountNumeric = 100, ExecStatus = "منفذ جبريا", CollectedAmount = 100, CreatedAt = LegacyDate });
        _db.SaveChanges();
        _stats = new StatisticsRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Dashboard_WithoutBranchFilter_CountsAll()
    {
        var s = await _stats.GetDashboardStatsAsync(null);

        Assert.Equal(4, s.TotalDocuments);
        Assert.Equal(1, s.TotalDrafts);
        Assert.Equal(2, s.TotalExecuted);
        Assert.Equal(1, s.TotalDeferred);
        Assert.Equal(0, s.TotalActive);
        Assert.Equal(3, s.TotalBorrowers);
        Assert.Equal(1300, s.TotalAmount);
        Assert.Equal(300, s.TotalCollectedAmount);
    }

    [Fact]
    public async Task Dashboard_WithBranchFilter_LimitsToBranch()
    {
        var branch = await _db.Branches.FirstAsync();
        var s = await _stats.GetDashboardStatsAsync(branch.Id);

        Assert.Equal(3, s.TotalDocuments);
        Assert.Equal(1, s.TotalDrafts);
        Assert.Equal(1, s.TotalExecuted);
        Assert.Equal(1, s.TotalDeferred);
        Assert.Equal(0, s.TotalActive);
        Assert.Equal(1200, s.TotalAmount);
        Assert.Equal(200, s.TotalCollectedAmount);
    }

    [Fact]
    public async Task Dashboard_CountsActiveDirectly()
    {
        _db.Documents.AddRange(
            new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "م", BorrowerFamily = "م", AmountNumeric = 100, ExecStatus = string.Empty },
            new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "ن", BorrowerFamily = "ن", AmountNumeric = 200, ExecStatus = "منفذ جبريا", ExecSubStatus = "منفذ جزئيا" },
            new Document { BranchId = 1, CreatedById = 1, IsDraft = true, BorrowerName = "ح", BorrowerFamily = "ح", AmountNumeric = 0, ExecStatus = "تريث" });
        _db.SaveChanges();

        var s = await _stats.GetDashboardStatsAsync(1);

        Assert.Equal(6, s.TotalDocuments);
        Assert.Equal(1, s.TotalDrafts);
        Assert.Equal(2, s.TotalActive);
        Assert.Equal(1, s.TotalExecuted);
        Assert.Equal(2, s.TotalDeferred);
    }

    [Fact]
    public async Task Dashboard_ExcludesStruckOffExecutedFiles()
    {
        // الملفات المشطوبة (وضع «منفذ عليه») مستثناة من عدادات اللوحة مثل القوائم والتصدير:
        // سجلها الوحيد هو صفحة «الملفات المشطوبة».
        var today = DateTime.Today;
        _db.Documents.Add(new Document
        {
            BranchId = 1,
            CreatedById = 1,
            IsDraft = false,
            BorrowerName = "مشطوب",
            BorrowerFamily = "مشطوب",
            AmountNumeric = 500,
            ExecStatus = string.Empty,
            GeneralEntitySide = GeneralEntitySideCatalog.Executed,
            ExecutedStatus = ExecutedStatusCatalog.StruckOff,
            StruckOffDate = DateTime.UtcNow,
            CreatedAt = new DateTime(today.Year, today.Month, 1),
        });
        _db.SaveChanges();

        var s = await _stats.GetDashboardStatsAsync(1);

        // التعداد يبقى كما كان قبل إضافة الملف المشطوب (3 ملفات في الفرع بمجموع 1200، بلا نشط).
        Assert.Equal(3, s.TotalDocuments);
        Assert.Equal(0, s.TotalActive);
        Assert.Equal(1200, s.TotalAmount);
    }

    [Fact]
    public async Task Dashboard_SumsMoneyInDecimalWithoutPrecisionLoss()
    {
        using var db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        db.Branches.Add(branch);
        db.SaveChanges();
        db.Users.Add(new User { Username = "u1", FullName = "مستخدم 1", Role = UserRole.Lawyer, BranchId = branch.Id });
        db.SaveChanges();
        db.Documents.AddRange(
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "أ", BorrowerFamily = "أ", AmountNumeric = 0.1m, ExecStatus = string.Empty, CollectedAmount = 0.1m },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "ب", BorrowerFamily = "ب", AmountNumeric = 0.2m, ExecStatus = string.Empty, CollectedAmount = 0.2m },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "ج", BorrowerFamily = "ج", AmountNumeric = 0.3m, ExecStatus = string.Empty, CollectedAmount = 0.3m });
        db.SaveChanges();

        var s = await new StatisticsRepository(db).GetDashboardStatsAsync(branch.Id);

        Assert.Equal(0.6m, s.TotalAmount);
        Assert.Equal(0.6m, s.TotalCollectedAmount);
    }

    [Fact]
    public async Task Reminders_ReturnsOnlyActionsWithRemindersOrderedByDueDate()
    {
        var doc = new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "أحمد", BorrowerFather = "خالد", BorrowerFamily = "العلي", AmountNumeric = 100, ExecStatus = string.Empty, DocumentType = "متداول - أحمد خالد العلي" };
        _db.Documents.Add(doc);
        _db.SaveChanges();

        _db.ExecutionActions.AddRange(
            new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "action", Text = "إجراء شهر", ActionDate = "1/9/2026", ReminderDuration = "شهر", ReminderColor = "أحمر", CreatedAt = new DateTime(2026, 8, 1) },
            new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "note", Text = "ملاحظة أسبوع", ActionDate = "1/8/2026", ReminderDuration = "أسبوع", ReminderColor = "أصفر", CreatedAt = new DateTime(2026, 8, 2) },
            new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "action", Text = "بدون تذكير", ActionDate = "1/7/2026", CreatedAt = new DateTime(2026, 8, 3) });
        _db.SaveChanges();

        var reminders = await _stats.GetRemindersAsync(1);

        Assert.Equal(2, reminders.Count);
        Assert.DoesNotContain(reminders, r => r.ActionText == "بدون تذكير");
        Assert.Equal("ملاحظة أسبوع", reminders[0].ActionText);
        Assert.Equal("إجراء شهر", reminders[1].ActionText);
        Assert.Equal(new DateTime(2026, 8, 8), reminders[0].DueDate);
        Assert.Equal(new DateTime(2026, 10, 1), reminders[1].DueDate);
        Assert.Equal(doc.Id, reminders[0].DocumentId);
        Assert.Equal("أحمد", reminders[0].BorrowerName);
        Assert.Equal("خالد", reminders[0].BorrowerFather);
        Assert.Equal("العلي", reminders[0].BorrowerFamily);
        Assert.True(reminders[0].ActionId > 0);
    }

    [Fact]
    public async Task Reminders_FiltersByUser()
    {
        var doc = new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 100, ExecStatus = string.Empty };
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "action", Text = "تذكير أ", ActionDate = "1/8/2026", ReminderDuration = "3 أيام", ReminderColor = "أحمر", CreatedAt = new DateTime(2026, 8, 1) });
        _db.SaveChanges();

        Assert.Single(await _stats.GetRemindersAsync(1));
        Assert.Empty(await _stats.GetRemindersAsync(999));
    }

    [Fact]
    public async Task Reminders_UsesCreatedAtWhenActionDateMissing()
    {
        var doc = new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "أ", BorrowerFamily = "ب", AmountNumeric = 100, ExecStatus = string.Empty };
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "note", Text = "ملاحظة بلا تاريخ", ActionDate = null, ReminderDuration = "أسبوعين", ReminderColor = "بنفسجي", CreatedAt = new DateTime(2026, 8, 1) });
        _db.SaveChanges();

        var reminders = await _stats.GetRemindersAsync(1);

        var reminder = Assert.Single(reminders);
        Assert.Equal(new DateTime(2026, 8, 15), reminder.DueDate);
    }

    [Fact]
    public async Task BranchesSummary_GroupsPerBranch()
    {
        var summary = await _stats.GetBranchesSummaryAsync();

        var dam = Assert.Single(summary);
        Assert.Equal("دمشق", dam.BranchName);
        Assert.Equal(3, dam.TotalDocuments);
        Assert.Equal(1, dam.TotalDrafts);
        Assert.Equal(1200, dam.TotalAmount);
    }

    [Fact]
    public async Task UserActivity_ListsUsersWithCounts()
    {
        var activity = await _stats.GetUserActivityAsync();

        var user = Assert.Single(activity);
        Assert.Equal("u1", user.Username);
        Assert.Equal(4, user.DocumentCount);
    }

    private static string D(int year, int month, int day) => $"{day}/{month}/{year}";

    /// <summary>مبلغ سلة عملة محددة من قائمة المبالغ المجمّعة (صفر عند غيابها).</summary>
    private static decimal AmountOf(IEnumerable<CurrencyAmountDto> amounts, string currency) =>
        amounts.FirstOrDefault(a => a.Currency == currency)?.Amount ?? 0m;

    private static Document RegisteredDoc(int branchId, bool isDraft, string? execStatus, string date,
        string? execSubStatus = null, decimal? collected = null, decimal amount = 0, decimal amount2 = 0) =>
        new()
        {
            BranchId = branchId,
            CreatedById = 1,
            IsDraft = isDraft,
            ExecStatus = execStatus ?? string.Empty,
            ExecSubStatus = execSubStatus,
            CollectedAmount = collected,
            AmountNumeric = amount,
            Amount2Numeric = amount2,
            RegistrationDate = new DocumentRegistrationDate { Date = date, DateParsed = ActionDateParser.TryParse(date) },
        };

    [Fact]
    public async Task ManagerStats_Monthly_CountsRegisteredFilesInCurrentMonth()
    {
        var today = DateTime.Today;
        var first = new DateTime(today.Year, today.Month, 1);
        var outside = first.AddMonths(-1);

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(first.Year, first.Month, first.Day)),
            RegisteredDoc(1, false, "تريث", D(today.Year, today.Month, 2)),
            RegisteredDoc(1, false, "منفذ بالتسوية", D(today.Year, today.Month, 3), collected: 300),
            RegisteredDoc(1, false, "منفذ جبريا", D(today.Year, today.Month, 4), collected: 700),
            RegisteredDoc(1, false, "منفذ جبريا", D(today.Year, today.Month, 5), execSubStatus: "منفذ جزئيا"),
            RegisteredDoc(1, true, null, D(today.Year, today.Month, 6)),
            RegisteredDoc(1, false, "منفذ بالتسوية", D(outside.Year, outside.Month, outside.Day), collected: 500),
            new Document { BranchId = 1, CreatedById = 1, IsDraft = true, ExecStatus = string.Empty });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        // متداول + منفذ جزئيا = active (2)؛ مسودتان (بإحداهما تاريخ قيد) = draft (2)؛ تريث = deferred
        // المسودة الأخيرة بلا تاريخ قيد تُحسب في شهر إدخالها (الحالي). TotalFiles = active + drafts + deferred
        Assert.Equal(5, s.TotalFiles);
        Assert.Equal(2, s.Active);
        Assert.Equal(2, s.Drafts);
        Assert.Equal(1, s.Deferred);
        Assert.Equal(1, s.SettledCount);
        Assert.Equal(300m, s.SettledCollected);
        Assert.Equal(1, s.ForcibleCount);
        Assert.Equal(700m, s.ForcibleCollected);
        Assert.Equal(today.Year, s.PeriodYear);
        Assert.Equal(today.Month, s.PeriodMonth);
        Assert.Null(s.PeriodQuarter);
    }

    [Fact]
    public async Task ManagerStats_WithBranchFilter_LimitsToBranch()
    {
        var today = DateTime.Today;
        var other = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.Add(other);
        _db.SaveChanges();

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(today.Year, today.Month, 5)),
            RegisteredDoc(other.Id, false, "تريث", D(today.Year, today.Month, 6)));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(1, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(0, s.Deferred);
    }

    [Fact]
    public async Task ManagerStats_CountsExecutedSideFilesInTheirOwnCards()
    {
        // ملفات وضع «الجهة العامة منفذ عليها» تُحتسب في بطاقتي «متداول للضد»/«منفذ للضد»
        // حسب حالة الوضع، معزولة تمامًا عن عدادتي «منفذ بالتسوية»/«منفذ جبريا» الخاصتين
        // بنظام «طالبة تنفيذ»، والمشطوب مستبعد من البطاقتين.
        var today = DateTime.Today;
        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(today.Year, today.Month, 2)),
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1000,
                FileReceiptDate = new DateTime(today.Year, today.Month, 3),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 500,
                FileReceiptDate = new DateTime(today.Year, today.Month, 4),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.StruckOff,
                ExecutedPaidAmount = 2000,
                ExecutedRequiredAmount = 900,
                StruckOffDate = DateTime.UtcNow,
                FileReceiptDate = new DateTime(today.Year, today.Month, 5),
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        // ملف واحد من «طالبة تنفيذ» (متداول) + ملفان من وضع «منفذ عليها» (منفذ/متداول)
        // والمشطوب مستبعد: «منفذ للضد» يحمل المبلغ المدفوع، «متداول للضد» يحمل المبلغ المطلوب.
        Assert.Equal(1, s.Active);
        Assert.Equal(0, s.SettledCount);
        Assert.Equal(0, s.ForcibleCount);
        Assert.Equal(1, s.ExecutedAgainstCount);
        Assert.Equal(1000m, s.ExecutedAgainstAmount);
        Assert.Equal(1, s.TradingAgainstCount);
        Assert.Equal(500m, AmountOf(s.TradingAgainstAmounts, "ليرة سورية"));
    }

    [Fact]
    public async Task ManagerStats_ExecutedSideUsesFileReceiptDateAsPeriod()
    {
        // فترة ملف «منفذ عليها» من تاريخ ورود الاخطار لا من تاريخ قيده (المقيد من الخصم)،
        // فملف ورد في شهر مضى لا يُحتسب في نافذة الشهر الحالي مهما كان تاريخ إدخاله.
        var today = DateTime.Today;
        var lastMonth = today.AddMonths(-1);
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 700,
                FileReceiptDate = new DateTime(lastMonth.Year, lastMonth.Month, lastMonth.Day),
                CreatedAt = new DateTime(today.Year, today.Month, 1, 10, 0, 0),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 300,
                FileReceiptDate = new DateTime(today.Year, today.Month, 6),
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(1, s.TradingAgainstCount);
        Assert.Equal(300m, AmountOf(s.TradingAgainstAmounts, "ليرة سورية"));
    }

    [Fact]
    public async Task ManagerStats_CountsDepositSideAsBeneficiaryRows()
    {
        // ملفات «عرض وايداع» تُحتسب «للصالح» كأسطر فرعية داخل بطاقتي متداول/منفذ:
        // المتداول بعدده فقط، والمنفذ بعدده ومجموع المبالغ المودعة، والمشطوب مستبعد،
        // ولا تماس لها ببطاقتي «منفذ للصالح» (بالتسوية/جبريا) ولا بطاقتي «للضد».
        var today = DateTime.Today;
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 900,
                FileReceiptDate = new DateTime(today.Year, today.Month, 2),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
                ExecutedStatus = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 750,
                ExecutedDepositDate = new DateTime(today.Year, today.Month, 3),
                FileReceiptDate = new DateTime(today.Year, today.Month, 3),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
                ExecutedStatus = ExecutedStatusCatalog.StruckOff,
                ExecutedPaidAmount = 3000,
                StruckOffDate = DateTime.UtcNow,
                FileReceiptDate = new DateTime(today.Year, today.Month, 4),
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(1, s.DepositTradingCount);
        Assert.Equal(1, s.DepositExecutedCount);
        Assert.Equal(750m, s.DepositExecutedAmount);
        // المشطوب مستبعد والمبالغ المطلوبة لا تُجمع للضضد في صفة العرض (متداول بالعدد فقط).
        Assert.Equal(0, s.TradingAgainstCount);
        Assert.Equal(0, s.ExecutedAgainstCount);
        Assert.Equal(0m, s.ExecutedAgainstAmount);
        Assert.Empty(s.TradingAgainstAmounts);
    }

    [Fact]
    public async Task ManagerStats_DepositSideUsesFileReceiptDateAsPeriod()
    {
        // فترة ملف «عرض وايداع» من تاريخ ورود الاخطار لا من تاريخ قيده (مثل وضع منفذ عليها)،
        // فملف ورد في شهر مضى لا يُحتسب في نافذة الشهر الحالي.
        var today = DateTime.Today;
        var lastMonth = today.AddMonths(-1);
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
                ExecutedStatus = ExecutedStatusCatalog.None,
                FileReceiptDate = new DateTime(lastMonth.Year, lastMonth.Month, lastMonth.Day),
                CreatedAt = new DateTime(today.Year, today.Month, 1, 10, 0, 0),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
                ExecutedStatus = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 200,
                FileReceiptDate = new DateTime(today.Year, today.Month, 6),
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        // الملف المتداول ورده الشهر الماضي رغم إنشائه هذا الشهر — لا يُحتسب في نافذة الشهر الحالي.
        Assert.Equal(0, s.DepositTradingCount);
        Assert.Equal(1, s.DepositExecutedCount);
        Assert.Equal(200m, s.DepositExecutedAmount);
    }

    [Fact]
    public async Task ManagerStats_Quarterly_CountsCurrentQuarterOnly()
    {
        var today = DateTime.Today;
        var quarterStart = new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1);
        var inQuarter = quarterStart.AddMonths(1);
        var prevQuarter = quarterStart.AddMonths(-1);
        var nextQuarter = quarterStart.AddMonths(3);

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(inQuarter.Year, inQuarter.Month, 1)),
            RegisteredDoc(1, false, null, D(prevQuarter.Year, prevQuarter.Month, 1)),
            RegisteredDoc(1, false, null, D(nextQuarter.Year, nextQuarter.Month, 1)));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Quarterly, 1);

        Assert.Equal(1, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(today.Year, s.PeriodYear);
        Assert.Equal((today.Month - 1) / 3 + 1, s.PeriodQuarter);
        Assert.Null(s.PeriodMonth);
    }

    [Fact]
    public async Task ManagerStats_Yearly_CountsCurrentYearOnly()
    {
        var today = DateTime.Today;
        var inYear = today.Month == 1 ? today : new DateTime(today.Year, 1, 1);
        var prevYear = new DateTime(today.Year - 1, 12, 1);
        var nextYear = new DateTime(today.Year + 1, 1, 1);

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(inYear.Year, inYear.Month, inYear.Day)),
            RegisteredDoc(1, false, null, D(prevYear.Year, prevYear.Month, prevYear.Day)),
            RegisteredDoc(1, false, null, D(nextYear.Year, nextYear.Month, nextYear.Day)));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Yearly, 1);

        Assert.Equal(1, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(today.Year, s.PeriodYear);
        Assert.Null(s.PeriodQuarter);
        Assert.Null(s.PeriodMonth);
    }

    [Fact]
    public async Task ManagerStats_TotalExcludesExecuted()
    {
        var today = DateTime.Today;

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(today.Year, today.Month, 5)),
            RegisteredDoc(1, false, "تريث", D(today.Year, today.Month, 6)),
            RegisteredDoc(1, false, "منفذ بالتسوية", D(today.Year, today.Month, 7), collected: 100),
            RegisteredDoc(1, false, "منفذ جبريا", D(today.Year, today.Month, 8), collected: 200));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(2, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(1, s.Deferred);
        Assert.Equal(1, s.SettledCount);
        Assert.Equal(1, s.ForcibleCount);
        Assert.Equal(300m, s.SettledCollected + s.ForcibleCollected);
    }

    [Fact]
    public async Task ManagerStats_AmountsPerStatus_SumAmountNumeric()
    {
        var today = DateTime.Today;

        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, D(today.Year, today.Month, 5), amount: 100, amount2: 3000),
            RegisteredDoc(1, false, "منفذ جبريا", D(today.Year, today.Month, 6),
                execSubStatus: "منفذ جزئيا", amount: 200, amount2: 2000),
            RegisteredDoc(1, true, null, D(today.Year, today.Month, 7), amount: 300),
            RegisteredDoc(1, false, "تريث", D(today.Year, today.Month, 8), amount: 400),
            RegisteredDoc(1, false, "منفذ بالتسوية", D(today.Year, today.Month, 9),
                collected: 50, amount: 500));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        // متداول (2 ملفات مصرفي) = 100 + 200، تحت رفع = 300، تريث = 400 — كلها بعملة ل.س.
        Assert.Equal(2, s.Active);
        Assert.Equal(2, s.ActiveSplit.BankingCount);
        Assert.Equal(0, s.ActiveSplit.OrdinaryCount);
        Assert.Equal(300m, AmountOf(s.ActiveSplit.BankingAmounts, "ليرة سورية"));
        Assert.Equal(1, s.Drafts);
        Assert.Equal(1, s.DraftsSplit.BankingCount);
        Assert.Equal(300m, AmountOf(s.DraftsSplit.BankingAmounts, "ليرة سورية"));
        Assert.Equal(1, s.Deferred);
        Assert.Equal(1, s.DeferredSplit.BankingCount);
        Assert.Equal(400m, AmountOf(s.DeferredSplit.BankingAmounts, "ليرة سورية"));
        // الإجمالي (دون المنفذ) بالعملات: ل.س = 300 + 300 + 400، والدولار من المتداول فقط.
        Assert.Equal(1000m, AmountOf(s.TotalAmounts, "ليرة سورية"));
        Assert.Equal(5000m, AmountOf(s.TotalAmounts, "دولار أمريكي"));
        Assert.DoesNotContain(s.TotalAmounts, a => a.Currency == "يورو");
        // مبالغ المنفذ لا تدخل في الإجمالي (دون المنفذ).
        Assert.Equal(1, s.SettledCount);
        Assert.Equal(50m, s.SettledCollected);
    }

    [Fact]
    public async Task ManagerStats_ActiveAndDeferred_SplitByContractTypeWithCurrencies()
    {
        // مبالغ «متداول للصالح» و«التريث» تُفصَّل مصرفي/عادي ويُجمَّع كل مبلغ في سلة عملته الفعلية
        // (ليرة سورية/دولار أمريكي/يورو) بدل تسميات ل.س/دولار الثابتة.
        var today = DateTime.Today;
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                ExecStatus = string.Empty,
                ContractTypeSelector = "مصرفي",
                AmountNumeric = 100,
                Currency = "دولار أمريكي",
                Amount2Numeric = 50,
                Currency2 = "يورو",
                RegistrationDate = new DocumentRegistrationDate
                    { Date = D(today.Year, today.Month, 2), DateParsed = new DateTime(today.Year, today.Month, 2) },
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                ExecStatus = string.Empty,
                ContractTypeSelector = "عادي",
                InclusionAmountNumeric = 200,
                InclusionCurrency = "ليرة سورية",
                RegistrationDate = new DocumentRegistrationDate
                    { Date = D(today.Year, today.Month, 3), DateParsed = new DateTime(today.Year, today.Month, 3) },
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                ExecStatus = "تريث",
                ContractTypeSelector = "عادي",
                InclusionAmountNumeric = 300,
                InclusionCurrency = "يورو",
                RegistrationDate = new DocumentRegistrationDate
                    { Date = D(today.Year, today.Month, 4), DateParsed = new DateTime(today.Year, today.Month, 4) },
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        // متداول للصالح: مصرفي واحد وعادي واحد، ومبالغ كلٍّ بعملته الفعلية.
        Assert.Equal(2, s.Active);
        Assert.Equal(1, s.ActiveSplit.BankingCount);
        Assert.Equal(1, s.ActiveSplit.OrdinaryCount);
        Assert.Equal(100m, AmountOf(s.ActiveSplit.BankingAmounts, "دولار أمريكي"));
        Assert.Equal(50m, AmountOf(s.ActiveSplit.BankingAmounts, "يورو"));
        Assert.Equal(200m, AmountOf(s.ActiveSplit.OrdinaryAmounts, "ليرة سورية"));
        // التريث: عادي واحد بمبلغ يورو.
        Assert.Equal(1, s.Deferred);
        Assert.Equal(0, s.DeferredSplit.BankingCount);
        Assert.Equal(1, s.DeferredSplit.OrdinaryCount);
        Assert.Equal(300m, AmountOf(s.DeferredSplit.OrdinaryAmounts, "يورو"));
        // الإجمالي (دون المنفذ) بالعملات: ل.س 200 + دولار 100 + يورو 350.
        Assert.Equal(200m, AmountOf(s.TotalAmounts, "ليرة سورية"));
        Assert.Equal(100m, AmountOf(s.TotalAmounts, "دولار أمريكي"));
        Assert.Equal(350m, AmountOf(s.TotalAmounts, "يورو"));
    }

    [Fact]
    public async Task ManagerStats_TradingAgainst_AmountsEachWithOwnCurrency()
    {
        // المبالغ المطلوبة الثلاثة في «متداول للضد» تُعرض كلٌّ بعملتها الفعلية،
        // ويُجمع كل مبلغ في سلة عملته دون دمجه مع عملة أخرى.
        var today = DateTime.Today;
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 500,
                ExecutedRequiredCurrency = "ليرة سورية",
                ExecutedRequiredAmount2 = 100,
                ExecutedRequiredCurrency2 = "دولار أمريكي",
                ExecutedRequiredAmount3 = 40,
                ExecutedRequiredCurrency3 = "يورو",
                FileReceiptDate = new DateTime(today.Year, today.Month, 2),
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                GeneralEntitySide = GeneralEntitySideCatalog.Executed,
                ExecutedStatus = ExecutedStatusCatalog.None,
                ExecutedRequiredAmount = 300,
                ExecutedRequiredCurrency = "ليرة سورية",
                FileReceiptDate = new DateTime(today.Year, today.Month, 3),
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(2, s.TradingAgainstCount);
        Assert.Equal(800m, AmountOf(s.TradingAgainstAmounts, "ليرة سورية"));
        Assert.Equal(100m, AmountOf(s.TradingAgainstAmounts, "دولار أمريكي"));
        Assert.Equal(40m, AmountOf(s.TradingAgainstAmounts, "يورو"));
        Assert.DoesNotContain(s.TradingAgainstAmounts, a => a.Currency == "ليرة سورية" && a.Amount != 800m);
    }

    [Fact]
    public async Task ManagerStats_Ordinary_ThreeAmountsEachWithOwnCurrency()
    {
        // المبالغ الثلاثة للملف العادي (المتضمن) تُجمع كلٌّ في سلة عملتها الفعلية ضمن سلة
        // المبالغ العادية، على غرار المبالغ الثلاثة للمصرفي وللضد.
        var today = DateTime.Today;
        _db.Documents.Add(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                ExecStatus = string.Empty,
                ContractTypeSelector = "عادي",
                InclusionAmountNumeric = 200,
                InclusionCurrency = "ليرة سورية",
                InclusionAmount2Numeric = 100,
                InclusionCurrency2 = "دولار أمريكي",
                InclusionAmount3Numeric = 40,
                InclusionCurrency3 = "يورو",
                RegistrationDate = new DocumentRegistrationDate
                    { Date = D(today.Year, today.Month, 2), DateParsed = new DateTime(today.Year, today.Month, 2) },
            });
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1);

        Assert.Equal(1, s.ActiveSplit.OrdinaryCount);
        Assert.Equal(0, s.ActiveSplit.BankingCount);
        Assert.Equal(200m, AmountOf(s.ActiveSplit.OrdinaryAmounts, "ليرة سورية"));
        Assert.Equal(100m, AmountOf(s.ActiveSplit.OrdinaryAmounts, "دولار أمريكي"));
        Assert.Equal(40m, AmountOf(s.ActiveSplit.OrdinaryAmounts, "يورو"));
        Assert.DoesNotContain(s.ActiveSplit.BankingAmounts, a => a.Currency == "ليرة سورية");
        // الإجمالي (دون المنفذ) يجمع المبالغ العادية الثلاثة بعملاتها.
        Assert.Equal(200m, AmountOf(s.TotalAmounts, "ليرة سورية"));
        Assert.Equal(100m, AmountOf(s.TotalAmounts, "دولار أمريكي"));
        Assert.Equal(40m, AmountOf(s.TotalAmounts, "يورو"));
    }

    [Fact]
    public async Task DashboardAndBranchSummary_IncludeOrdinaryInclusionAmounts()
    {
        _db.Documents.AddRange(
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                BorrowerName = "م",
                BorrowerFamily = "م",
                ContractTypeSelector = "عادي",
                AmountNumeric = 0,
                InclusionAmountNumeric = 250,
                ExecStatus = string.Empty,
            },
            new Document
            {
                BranchId = 1,
                CreatedById = 1,
                IsDraft = false,
                BorrowerName = "ن",
                BorrowerFamily = "ن",
                ContractTypeSelector = "مصرفي",
                AmountNumeric = 150,
                ExecStatus = string.Empty,
            });
        _db.SaveChanges();

        // أساس فرع 1 قبل الإضافة: 500 + 700 = 1200.
        var s = await _stats.GetDashboardStatsAsync(1);
        Assert.Equal(1600m, s.TotalAmount);

        var branches = await _stats.GetBranchesSummaryAsync();
        Assert.Equal(1600m, branches.Single(b => b.BranchId == 1).TotalAmount);
    }

    [Fact]
    public async Task ManagerLawyerStats_ListsBranchLawyersWithMonthlyPoints()
    {
        var today = DateTime.Today;
        var branch = await _db.Branches.FirstAsync();
        var lawyer2 = new User { Username = "u2", FullName = "مستخدم 2", Role = UserRole.Lawyer, BranchId = branch.Id };
        _db.Users.Add(lawyer2);
        _db.SaveChanges();

        _db.Documents.AddRange(
            RegisteredDoc(branch.Id, false, null, D(today.Year, today.Month, 5)),
            RegisteredDoc(branch.Id, false, null, D(today.Year, today.Month, 6)),
            RegisteredDoc(branch.Id, false, null, D(today.Year, today.Month, 7)));
        _db.SaveChanges();
        var second = await _db.Documents.OrderBy(d => d.Id).LastAsync();
        second.CreatedById = lawyer2.Id;
        _db.SaveChanges();

        var stats = await _stats.GetManagerLawyerStatsAsync(StatsPeriod.Monthly, branch.Id);

        Assert.Equal(2, stats.Count);
        Assert.Equal(2, stats[0].TotalCount);
        Assert.Single(stats[0].Points);
        Assert.Equal(today.Month, stats[0].Points[0].Month);
        Assert.Equal(2, stats[0].Points[0].Count);
        Assert.Equal(1, stats[1].TotalCount);
    }

    [Fact]
    public async Task ManagerLawyerStats_IncludesLawyersWithNoFiles()
    {
        var branch = await _db.Branches.FirstAsync();
        var lawyer2 = new User { Username = "u3", FullName = "مستخدم 3", Role = UserRole.Lawyer, BranchId = branch.Id };
        _db.Users.Add(lawyer2);
        _db.SaveChanges();

        var stats = await _stats.GetManagerLawyerStatsAsync(StatsPeriod.Monthly, branch.Id);

        Assert.Equal(2, stats.Count);
        Assert.All(stats, s => Assert.Equal(0, s.TotalCount));
    }

    [Fact]
    public async Task ManagerStats_ExplicitMonth_CountsOnlyThatMonth()
    {
        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, "5/5/2026"),
            RegisteredDoc(1, false, "تريث", "6/5/2026"),
            RegisteredDoc(1, false, "تريث", "7/6/2026"));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Monthly, 1, year: 2026, month: 5);

        Assert.Equal(2, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(1, s.Deferred);
        Assert.Equal(2026, s.PeriodYear);
        Assert.Equal(5, s.PeriodMonth);
        Assert.Null(s.PeriodQuarter);
    }

    [Fact]
    public async Task ManagerStats_ExplicitQuarter_CountsOnlyThatQuarter()
    {
        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, "10/4/2026"),
            RegisteredDoc(1, false, null, "20/5/2026"),
            RegisteredDoc(1, false, "تريث", "5/7/2026"));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Quarterly, 1, year: 2026, quarter: 2);

        Assert.Equal(2, s.TotalFiles);
        Assert.Equal(2, s.Active);
        Assert.Equal(0, s.Deferred);
        Assert.Equal(2026, s.PeriodYear);
        Assert.Equal(2, s.PeriodQuarter);
        Assert.Null(s.PeriodMonth);
    }

    [Fact]
    public async Task ManagerStats_ExplicitYear_CountsOnlyThatYear()
    {
        _db.Documents.AddRange(
            RegisteredDoc(1, false, null, "10/1/2025"),
            RegisteredDoc(1, false, null, "20/12/2026"),
            RegisteredDoc(1, false, "تريث", "5/3/2027"));
        _db.SaveChanges();

        var s = await _stats.GetManagerStatsAsync(StatsPeriod.Yearly, 1, year: 2026);

        Assert.Equal(1, s.TotalFiles);
        Assert.Equal(1, s.Active);
        Assert.Equal(0, s.Deferred);
        Assert.Equal(2026, s.PeriodYear);
        Assert.Null(s.PeriodQuarter);
        Assert.Null(s.PeriodMonth);
    }

    [Fact]
    public async Task PersonalStats_FiltersByUser()
    {
        var branch = await _db.Branches.FirstAsync();
        var user2 = new User { Username = "u2", FullName = "مستخدم 2", Role = UserRole.Lawyer, BranchId = branch.Id };
        _db.Users.Add(user2);
        _db.SaveChanges();

        _db.Documents.AddRange(
            RegisteredDoc(branch.Id, false, null, "5/5/2026"),
            RegisteredDoc(branch.Id, false, "تريث", "6/5/2026"));
        _db.SaveChanges();

        var last = await _db.Documents.SingleAsync(d => d.RegistrationDate != null && d.RegistrationDate.Date == "5/5/2026");
        last.CreatedById = user2.Id;
        _db.SaveChanges();

        var mine = await _stats.GetPersonalStatsAsync(StatsPeriod.Monthly, user2.Id, year: 2026, month: 5);
        var other = await _stats.GetPersonalStatsAsync(StatsPeriod.Monthly, 1, year: 2026, month: 5);

        Assert.Equal(1, mine.TotalFiles);
        Assert.Equal(0, mine.Deferred);
        Assert.Equal(1, other.TotalFiles);
        Assert.Equal(1, other.Deferred);
    }

    [Fact]
    public async Task AvailablePeriods_ListsOnlyRegisteredMonthsWithCounts()
    {
        var branch = await _db.Branches.FirstAsync();
        _db.Documents.AddRange(
            RegisteredDoc(branch.Id, false, null, "5/5/2026"),
            RegisteredDoc(branch.Id, false, null, "6/5/2026"),
            RegisteredDoc(branch.Id, false, null, "7/6/2026"));
        _db.SaveChanges();

        var all = await _stats.GetAvailablePeriodsAsync(null, null);

        Assert.Contains(all, p => p.Year == 2026 && p.Month == 5 && p.Count == 2);
        Assert.Contains(all, p => p.Year == 2026 && p.Month == 6 && p.Count == 1);
        Assert.All(all, p => Assert.True(p.Count >= 1));
    }

    [Fact]
    public async Task AvailablePeriods_ScopesByBranch()
    {
        var branch = await _db.Branches.FirstAsync();
        var other = new Branch { Name = "حلب", Code = "ALP" };
        _db.Branches.Add(other);
        _db.SaveChanges();

        _db.Documents.AddRange(
            RegisteredDoc(branch.Id, false, null, "5/5/2026"),
            RegisteredDoc(other.Id, false, null, "6/6/2026"));
        _db.SaveChanges();

        var branchOnly = await _stats.GetAvailablePeriodsAsync(branch.Id, null);

        Assert.DoesNotContain(branchOnly, p => p.Year == 2026 && p.Month == 6);
        Assert.Contains(branchOnly, p => p.Year == 2026 && p.Month == 5);
    }

    [Fact]
    public async Task AvailablePeriods_IncludesCreationMonthForDraftsWithoutRegistrationDate()
    {
        var branch = await _db.Branches.FirstAsync();
        _db.Documents.Add(new Document
        {
            BranchId = branch.Id,
            CreatedById = 1,
            IsDraft = true,
            ExecStatus = string.Empty,
            CreatedAt = new DateTime(2026, 7, 31),
        });
        _db.SaveChanges();

        var all = await _stats.GetAvailablePeriodsAsync(branch.Id, null);

        Assert.Contains(all, p => p.Year == 2026 && p.Month == 7);
    }

    [Fact]
    public async Task PersonalStats_CountsDraftWithoutRegistrationDateInItsCreationMonth()
    {
        var branch = await _db.Branches.FirstAsync();
        _db.Documents.Add(new Document
        {
            BranchId = branch.Id,
            CreatedById = 1,
            IsDraft = true,
            ExecStatus = string.Empty,
            CreatedAt = new DateTime(2026, 7, 31),
        });
        _db.SaveChanges();

        var july = await _stats.GetPersonalStatsAsync(StatsPeriod.Monthly, 1, year: 2026, month: 7);
        var august = await _stats.GetPersonalStatsAsync(StatsPeriod.Monthly, 1, year: 2026, month: 8);

        Assert.Equal(1, july.TotalFiles);
        Assert.Equal(1, july.Drafts);
        Assert.Equal(0, august.TotalFiles);
    }
}