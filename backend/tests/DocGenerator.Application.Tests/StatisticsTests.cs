using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class StatisticsRepositoryTests : IDisposable
{
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
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = true, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 0, ExecStatus = string.Empty },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 500, ExecStatus = "منفذ بالتسوية", CollectedAmount = 200 },
            new Document { BranchId = branch.Id, CreatedById = 1, IsDraft = false, BorrowerName = "سامر", BorrowerFamily = "حسن", AmountNumeric = 700, ExecStatus = "تريث" },
            new Document { BranchId = null, CreatedById = 1, IsDraft = false, BorrowerName = "بلا فرع", BorrowerFamily = "س", AmountNumeric = 100, ExecStatus = "منفذ جبريا", CollectedAmount = 100 });
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

        var reminders = await _stats.GetRemindersAsync(null, null);

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
    public async Task Reminders_FiltersByBranchAndUser()
    {
        var doc = new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "أحمد", BorrowerFamily = "العلي", AmountNumeric = 100, ExecStatus = string.Empty };
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "action", Text = "تذكير أ", ActionDate = "1/8/2026", ReminderDuration = "3 أيام", ReminderColor = "أحمر", CreatedAt = new DateTime(2026, 8, 1) });
        _db.SaveChanges();

        Assert.Single(await _stats.GetRemindersAsync(1, 1));
        Assert.Empty(await _stats.GetRemindersAsync(999, null));
        Assert.Empty(await _stats.GetRemindersAsync(null, 999));
    }

    [Fact]
    public async Task Reminders_UsesCreatedAtWhenActionDateMissing()
    {
        var doc = new Document { BranchId = 1, CreatedById = 1, IsDraft = false, BorrowerName = "أ", BorrowerFamily = "ب", AmountNumeric = 100, ExecStatus = string.Empty };
        _db.Documents.Add(doc);
        _db.SaveChanges();
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = doc.Id, CreatedById = 1, Type = "note", Text = "ملاحظة بلا تاريخ", ActionDate = null, ReminderDuration = "أسبوعين", ReminderColor = "بنفسجي", CreatedAt = new DateTime(2026, 8, 1) });
        _db.SaveChanges();

        var reminders = await _stats.GetRemindersAsync(1, 1);

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

    private static Document RegisteredDoc(int branchId, bool isDraft, string? execStatus, string date,
        string? execSubStatus = null, decimal? collected = null) =>
        new()
        {
            BranchId = branchId,
            CreatedById = 1,
            IsDraft = isDraft,
            ExecStatus = execStatus ?? string.Empty,
            ExecSubStatus = execSubStatus,
            CollectedAmount = collected,
            RegistrationDate = new DocumentRegistrationDate { Date = date },
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

        // متداول + منفذ جزئيا = active (2)؛ تحت رفع = draft؛ تريث = deferred
        Assert.Equal(4, s.TotalFiles);
        Assert.Equal(2, s.Active);
        Assert.Equal(1, s.Drafts);
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
}