using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class StatisticsRepository
{
    /// <summary>
    /// الأشهر المتاحة (تاريخ القيد، وإن لم يُقيد بعد فشهر الإدخال) ضمن نطاق الفرع/المستخدم،
    /// لتغذية منتقي الفترة المحددة في الواجهة.
    /// </summary>
    public async Task<List<MonthlyStatDto>> GetAvailablePeriodsAsync(int? branchId, int? userId, CancellationToken ct = default,
        int? ownerSectionId = null, bool fullAccess = true)
    {
        var dates = await InOwnerScope(_db.Documents.AsNoTracking(), ownerSectionId, fullAccess)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => userId == null || d.CreatedById == userId)
            .Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Select(d => new
            {
                RegDateParsed = d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null,
                d.CreatedAt,
            })
            .ToListAsync(ct);

        return GroupMonths(dates.Select(x => x.RegDateParsed ?? x.CreatedAt.Date));
    }

    private static List<MonthlyStatDto> GroupMonths(IEnumerable<DateTime> dates)
    {
        var counts = new Dictionary<(int Year, int Month), int>();
        foreach (var date in dates)
        {
            var key = (date.Year, date.Month);
            counts[key] = counts.TryGetValue(key, out var c) ? c + 1 : 1;
        }

        return counts
            .OrderBy(k => k.Key.Year)
            .ThenBy(k => k.Key.Month)
            .Select(k => new MonthlyStatDto(k.Key.Year, k.Key.Month, k.Value))
            .ToList();
    }

    public async Task<List<ReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
    {
        var rows = await _db.ExecutionActions.AsNoTracking()
            .Where(a => a.ReminderDuration != null || a.ReminderColor != null || a.ReminderBaseDate != null)
            .Where(a => a.Document.CreatedById == userId)
            .Select(a => new
            {
                a.Id,
                a.DocumentId,
                a.Document.DocumentType,
                a.Document.BorrowerName,
                a.Document.BorrowerFather,
                a.Document.BorrowerFamily,
                a.Text,
                a.ActionDate,
                a.ReminderDuration,
                a.ReminderColor,
                a.ReminderBaseDate,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        // اسم العرض الموحّد يحتاج أطراف عائلة «منفذ عليها/عرض وايداع» — استعلام ثانٍ
        // محصور بملفات التذكيرات نفسها (لا يشمل كل المستندات) مع SplitQuery.
        var documentIds = rows.Select(r => r.DocumentId).Distinct().ToList();
        var documentLabels = documentIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Documents.AsNoTracking()
                .Where(d => documentIds.Contains(d.Id))
                .Include(d => d.ExecutedNaturalPersons)
                .Include(d => d.ExecutedPublicEntities)
                .Include(d => d.ExecutionApplicants)
                .AsSplitQuery()
                .ToDictionaryAsync(d => d.Id, d => DocumentDisplayName.Build(d), ct);

        return rows
            .Select(r =>
            {
                var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
                    r.ReminderBaseDate, r.ActionDate, r.ReminderDuration, r.CreatedAt);
                return new ReminderDto(
                    r.Id,
                    r.DocumentId,
                    r.DocumentType,
                    r.BorrowerName,
                    r.BorrowerFather,
                    r.BorrowerFamily,
                    r.Text,
                    r.ActionDate,
                    r.ReminderDuration,
                    r.ReminderColor,
                    dueDate,
                    suspect,
                    documentLabels.GetValueOrDefault(r.DocumentId));
            })
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.DocumentId)
            .ToList();
    }
}
