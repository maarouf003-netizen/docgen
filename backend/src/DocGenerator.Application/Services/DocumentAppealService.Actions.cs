using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── الإجراءات والملاحظات المستقلة ─────────────────────────────────────

    public async Task<List<AppealActionDto>> GetActionsAsync(int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        return appeal.Actions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => ToActionDto(a))
            .ToList();
    }

    public async Task<List<AppealReminderDto>> GetRemindersAsync(int userId, CancellationToken ct = default)
    {
        var rows = await ListReminderRowsAsync(userId, ct);
        return rows
            .Select(r =>
            {
                var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
                    r.ReminderBaseDate, r.ActionDate, r.ReminderDuration, r.CreatedAt);
                return new AppealReminderDto(
                    r.ActionId,
                    r.AppealId,
                    r.DocumentId,
                    r.AppealTitle,
                    r.Text,
                    r.ActionDate,
                    r.ReminderDuration,
                    r.ReminderColor,
                    dueDate,
                    suspect,
                    r.DocumentLabel);
            })
            .OrderBy(r => r.DueDate)
            .ThenBy(r => r.AppealId)
            .ToList();
    }

    private async Task<List<(int ActionId, int AppealId, int DocumentId, string AppealTitle,
        string DocumentLabel, string Text, string? ActionDate, string? ReminderDuration,
        string? ReminderColor, DateTime? ReminderBaseDate, DateTime CreatedAt)>>
        ListReminderRowsAsync(int userId, CancellationToken ct)
    {
        var appeals = await _appeals.ListByAssigneeAsync(userId, null, asNoTracking: true, ct);
        var rows = new List<(int, int, int, string, string, string, string?, string?, string?, DateTime?, DateTime)>();
        foreach (var appeal in appeals)
        {
            var documentLabel = DocumentDisplayName.Build(appeal.Document);
            foreach (var action in appeal.Actions.Where(a =>
                         a.ReminderDuration != null || a.ReminderColor != null || a.ReminderBaseDate != null))
            {
                rows.Add((action.Id, appeal.Id, appeal.DocumentId,
                    AppealTitleOf(appeal), documentLabel, action.Text, action.ActionDate,
                    action.ReminderDuration, action.ReminderColor, action.ReminderBaseDate, action.CreatedAt));
            }
        }
        return rows;
    }
}
