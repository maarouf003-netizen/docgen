using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class StatisticsRepository
{
    public async Task<ManagerStatsDto> GetManagerStatsAsync(StatsPeriod period, int? branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var (utcStart, utcEnd) = ToUtcWindow(window);
        var rows = await InOwnerScope(_db.Documents.AsNoTracking(), ownerSectionId, fullAccess)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                SourceDelegationId = d.SourceDelegationId,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                WasDepositExecuted = d.WasDepositExecuted,
                ContractTypeSelector = d.ContractTypeSelector,
                AmountNumeric = d.AmountNumeric,
                Currency = d.Currency,
                Amount2Numeric = d.Amount2Numeric,
                Currency2 = d.Currency2,
                Amount3Numeric = d.Amount3Numeric,
                Currency3 = d.Currency3,
                InclusionAmountNumeric = d.InclusionAmountNumeric,
                InclusionCurrency = d.InclusionCurrency,
                InclusionAmount2Numeric = d.InclusionAmount2Numeric,
                InclusionCurrency2 = d.InclusionCurrency2,
                InclusionAmount3Numeric = d.InclusionAmount3Numeric,
                InclusionCurrency3 = d.InclusionCurrency3,
                CollectedAmount = d.CollectedAmount,
                CollectedAmount2 = d.CollectedAmount2,
                CollectedAmount3 = d.CollectedAmount3,
                CollectedCurrency = d.CollectedCurrency,
                CollectedCurrency2 = d.CollectedCurrency2,
                CollectedCurrency3 = d.CollectedCurrency3,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedRequiredCurrency = d.ExecutedRequiredCurrency,
                ExecutedRequiredAmount2 = d.ExecutedRequiredAmount2,
                ExecutedRequiredCurrency2 = d.ExecutedRequiredCurrency2,
                ExecutedRequiredAmount3 = d.ExecutedRequiredAmount3,
                ExecutedRequiredCurrency3 = d.ExecutedRequiredCurrency3,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                ExecutedPaidCurrency = d.ExecutedPaidCurrency,
                ExecutedPaidAmount2 = d.ExecutedPaidAmount2,
                ExecutedPaidCurrency2 = d.ExecutedPaidCurrency2,
                ExecutedPaidAmount3 = d.ExecutedPaidAmount3,
                ExecutedPaidCurrency3 = d.ExecutedPaidCurrency3,
                DelegationSalesAmount = d.Delegations
                    .Where(dl => dl.Status == DelegationStatusCatalog.Executed)
                    .SelectMany(dl => dl.Assets)
                    .Sum(a => (decimal?)a.SalePrice),
                PeriodDate = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate ?? d.CreatedAt
                        : d.RegistrationDate!.DateParsed ?? d.CreatedAt,
                // وسم المصدر بذات شرط الاحتياط أعلاه: جهات «منفذ عليها/إيداع» تسقط إلى
                // الإدخال عند غياب ورود الإخطار، وغيرها عند غياب/تعذّر تحليل تاريخ القيد.
                // السبب مفصول في علم مستقل (`PeriodDateReceiptMissing`) لأن تاريخ القيد ليس
                // التاريخ المعتمد لهاتين الجهتين أصلًا — فلا يُنسب احتياطهما إلى «تعذّر تحليله».
                PeriodDateFromCreatedAt = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate == null
                        : d.RegistrationDate == null || d.RegistrationDate.DateParsed == null,
                PeriodDateReceiptMissing = (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.FileReceiptDate == null,
            })
            .Where(r => r.PeriodDateFromCreatedAt
                ? r.PeriodDate >= utcStart && r.PeriodDate < utcEnd
                : r.PeriodDate >= window.Start && r.PeriodDate < window.End)
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window);
    }

    public async Task<ManagerStatsDto> GetPersonalStatsAsync(StatsPeriod period, int userId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var (utcStart, utcEnd) = ToUtcWindow(window);
        var rows = await _db.Documents.AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new ManagerStatRow
            {
                IsDraft = d.IsDraft,
                SourceDelegationId = d.SourceDelegationId,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                WasDepositExecuted = d.WasDepositExecuted,
                ContractTypeSelector = d.ContractTypeSelector,
                AmountNumeric = d.AmountNumeric,
                Currency = d.Currency,
                Amount2Numeric = d.Amount2Numeric,
                Currency2 = d.Currency2,
                Amount3Numeric = d.Amount3Numeric,
                Currency3 = d.Currency3,
                InclusionAmountNumeric = d.InclusionAmountNumeric,
                InclusionCurrency = d.InclusionCurrency,
                InclusionAmount2Numeric = d.InclusionAmount2Numeric,
                InclusionCurrency2 = d.InclusionCurrency2,
                InclusionAmount3Numeric = d.InclusionAmount3Numeric,
                InclusionCurrency3 = d.InclusionCurrency3,
                CollectedAmount = d.CollectedAmount,
                CollectedAmount2 = d.CollectedAmount2,
                CollectedAmount3 = d.CollectedAmount3,
                CollectedCurrency = d.CollectedCurrency,
                CollectedCurrency2 = d.CollectedCurrency2,
                CollectedCurrency3 = d.CollectedCurrency3,
                ExecutedRequiredAmount = d.ExecutedRequiredAmount,
                ExecutedRequiredCurrency = d.ExecutedRequiredCurrency,
                ExecutedRequiredAmount2 = d.ExecutedRequiredAmount2,
                ExecutedRequiredCurrency2 = d.ExecutedRequiredCurrency2,
                ExecutedRequiredAmount3 = d.ExecutedRequiredAmount3,
                ExecutedRequiredCurrency3 = d.ExecutedRequiredCurrency3,
                ExecutedPaidAmount = d.ExecutedPaidAmount,
                ExecutedPaidCurrency = d.ExecutedPaidCurrency,
                ExecutedPaidAmount2 = d.ExecutedPaidAmount2,
                ExecutedPaidCurrency2 = d.ExecutedPaidCurrency2,
                ExecutedPaidAmount3 = d.ExecutedPaidAmount3,
                ExecutedPaidCurrency3 = d.ExecutedPaidCurrency3,
                DelegationSalesAmount = d.Delegations
                    .Where(dl => dl.Status == DelegationStatusCatalog.Executed)
                    .SelectMany(dl => dl.Assets)
                    .Sum(a => (decimal?)a.SalePrice),
                PeriodDate = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate ?? d.CreatedAt
                        : d.RegistrationDate!.DateParsed ?? d.CreatedAt,
                // وسم المصدر بذات شرط الاحتياط أعلاه (راجع مسار المدير — مع فصل سبب الإخطار).
                PeriodDateFromCreatedAt = d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit
                        ? d.FileReceiptDate == null
                        : d.RegistrationDate == null || d.RegistrationDate.DateParsed == null,
                PeriodDateReceiptMissing = (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.FileReceiptDate == null,
            })
            .Where(r => r.PeriodDateFromCreatedAt
                ? r.PeriodDate >= utcStart && r.PeriodDate < utcEnd
                : r.PeriodDate >= window.Start && r.PeriodDate < window.End)
            .ToListAsync(ct);

        return AggregateManagerStats(rows, period, window)
            with
        { Appeals = await GetPersonalAppealsStatsAsync(userId, window.Start, window.End, ct) };
    }

    /// <summary>
    /// عدادات استئنافات المحامي في نطاق الفترة (المسندة إليه للمتابعة):
    /// المنظورة بحسب لحظة التسطير، والمحسومة للصالح/للضد بحسب تاريخ قرار الحسم
    /// (وبديلٍ لحظة التسطير إن غاب). null عند غياب أي استئناف.
    /// </summary>
    private async Task<AppealsStatsDto?> GetPersonalAppealsStatsAsync(
        int userId, DateTime start, DateTime end, CancellationToken ct)
    {
        var rows = await _db.DocumentAppeals.AsNoTracking()
            .Where(a => a.AssignedLawyerId == userId)
            .Select(a => new
            {
                a.Status,
                a.Outcome,
                a.CreatedAt,
                a.DecisionDate,
            })
            .ToListAsync(ct);
        if (rows.Count == 0)
            return null;

        var (utcStart, utcEnd) = ToUtcWindow((start, end));
        var pending = rows.Count(r =>
            r.Status == AppealStatusCatalog.Pending
            && r.CreatedAt >= utcStart && r.CreatedAt < utcEnd);
        var inFavor = rows.Count(r =>
            r.Status == AppealStatusCatalog.Decided && r.Outcome == AppealOutcomeCatalog.InFavor
            && InLocalPeriod(r.DecisionDate, r.CreatedAt, start, end));
        var against = rows.Count(r =>
            r.Status == AppealStatusCatalog.Decided && r.Outcome == AppealOutcomeCatalog.Against
            && InLocalPeriod(r.DecisionDate, r.CreatedAt, start, end));

        return new AppealsStatsDto(pending, inFavor, against);
    }

    /// <summary>
    /// تقع اللحظة في الفترة المحلية؟ تاريخ القرار (يومي) يُقارن بالحدود المحلية،
    /// ولحظة الإنشاء (UTC) تُحوَّل إلى توقيت دمشق قبل المقارنة.
    /// </summary>
    private bool InLocalPeriod(DateTime? decisionDate, DateTime createdAtUtc, DateTime start, DateTime end)
    {
        if (decisionDate is { } decided)
            return decided.Date >= start && decided.Date < end;

        var local = TimeZoneInfo.ConvertTimeFromUtc(AsUtc(createdAtUtc), _timeZone);
        return local >= start && local < end;
    }

    public async Task<List<ManagerLawyerStatDto>> GetManagerLawyerStatsAsync(StatsPeriod period, int branchId,
        int? year = null, int? month = null, int? quarter = null, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
    {
        var window = GetPeriodWindow(period, year, month, quarter);
        var rows = await InOwnerScope(_db.Documents.AsNoTracking(), ownerSectionId, fullAccess)
            .Where(d => d.BranchId == branchId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                d.CreatedById,
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
                CreatedAt = d.CreatedAt,
            })
            .ToListAsync(ct);

        var lawyers = await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Lawyer && u.BranchId == branchId && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.Username })
            .ToListAsync(ct);

        var counts = new Dictionary<int, Dictionary<(int Year, int Month), (int Count, int Fallback)>>();
        foreach (var r in rows)
        {
            // فترة الملف: تاريخ قيده، وإن لم يُقيد بعد (تحت رفع) فشهر إدخاله —
            // مع وسم المصدر لكل شهر (راجع `MonthlyStatDto`).
            var periodDate = r.RegDateParsed ?? LocalDateOf(r.CreatedAt);
            if (periodDate < window.Start || periodDate >= window.End)
                continue;

            var key = (periodDate.Year, periodDate.Month);
            if (!counts.TryGetValue(r.CreatedById, out var months))
            {
                months = new Dictionary<(int, int), (int, int)>();
                counts[r.CreatedById] = months;
            }
            var (count, fallback) = months.TryGetValue(key, out var e) ? e : (0, 0);
            months[key] = (count + 1, fallback + (r.RegDateParsed is null ? 1 : 0));
        }

        var points = new List<(int Year, int Month)>();
        for (var cursor = window.Start; cursor < window.End; cursor = cursor.AddMonths(1))
            points.Add((cursor.Year, cursor.Month));

        return lawyers
            .Select(l =>
            {
                var months = counts.TryGetValue(l.Id, out var m) ? m : new Dictionary<(int, int), (int, int)>();
                var lawyerPoints = points
                    .Select(p =>
                    {
                        var (count, fallback) = months.TryGetValue((p.Year, p.Month), out var e) ? e : (0, 0);
                        return new ManagerPeriodPointDto(p.Year, p.Month, count, fallback);
                    })
                    .ToList();
                return new ManagerLawyerStatDto(
                    l.Id,
                    string.IsNullOrWhiteSpace(l.FullName) ? l.Username : l.FullName,
                    lawyerPoints.Sum(p => p.Count),
                    lawyerPoints);
            })
            .OrderByDescending(l => l.TotalCount)
            .ThenBy(l => l.LawyerName)
            .ToList();
    }
}
