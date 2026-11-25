using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>تذكيرات إجراءات الاستئنافات التي يتابعها المحامي (بطاقة التذكيرات).</summary>
public sealed record GetAppealRemindersQuery(int UserId) : IQuery<List<AppealReminderDto>>;

internal sealed class GetAppealRemindersQueryHandler
    : QueryHandlerBase<GetAppealRemindersQuery, List<AppealReminderDto>>
{
    private readonly IAppealRepository _appeals;

    public GetAppealRemindersQueryHandler(
        IAppealRepository appeals,
        IEnumerable<IPipelineBehavior<GetAppealRemindersQuery, List<AppealReminderDto>>> behaviors)
        : base(behaviors)
        => _appeals = appeals;

    protected override async Task<List<AppealReminderDto>> HandleCoreAsync(
        GetAppealRemindersQuery query, CancellationToken ct)
    {
        var rows = await ListReminderRowsAsync(query.UserId, ct);
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

    /// <summary>عنوان موجز للاستئناف في التذكيرات.</summary>
    private static string AppealTitleOf(DocumentAppeal appeal)
        => $"استئناف قرار رئيس التنفيذ — {AppealDirectionCatalog.ToLabel(appeal.Direction)}";
}
