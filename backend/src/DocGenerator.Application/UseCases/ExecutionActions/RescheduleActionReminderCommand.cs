using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionActions;

/// <summary>
/// نقل تذكير إجراء إلى يوم استحقاق جديد من نافذة اليوم: المدخل (`NewDueDate`) هو يوم
/// الاستحقاق نفسه لا تاريخ الإجراء؛ وتُكتب `ReminderBaseDate = newDueDate − DurationDays`
/// مع بقاء المدة وبقية الحقول دون مساس. **تاريخ الإجراء لا يُكتب ولا يُعاد تنسيقه** (I1/I7).
/// </summary>
public sealed record RescheduleActionReminderCommand(
    int DocumentId,
    int ActionId,
    string? NewDueDate,
    string? ActorName,
    long? Version = null) : ICommand<ExecutionActionDto>, ITransactionalRequest;

public sealed class RescheduleActionReminderCommandHandler
    : CommandHandlerBase<RescheduleActionReminderCommand, ExecutionActionDto>
{
    private readonly IRepository<ExecutionAction> _actions;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public RescheduleActionReminderCommandHandler(
        IRepository<ExecutionAction> actions,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<RescheduleActionReminderCommand, ExecutionActionDto>> behaviors)
        : base(behaviors)
    {
        _actions = actions;
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<ExecutionActionDto> HandleCoreAsync(
        RescheduleActionReminderCommand command, CancellationToken ct)
    {
        var action = await _actions.GetByIdAsync(command.ActionId, ct);
        if (action is null || action.DocumentId != command.DocumentId)
            throw new KeyNotFoundException();

        OptimisticConcurrencyGuard.EnsureCurrent(action, command.Version, "الإجراء");

        var doc = await _documents.GetByIdAsync(command.DocumentId, ct);
        if (doc is null)
            throw new KeyNotFoundException();

        if (string.IsNullOrWhiteSpace(command.NewDueDate))
            throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        var due = DocumentValidator.ParseDateTime(command.NewDueDate.Trim(), "اليوم الجديد")
            ?? throw new ArgumentException("اليوم الجديد مطلوب — استخدم مثال: 1/8/2026");

        if (string.IsNullOrWhiteSpace(action.ReminderDuration)
            && string.IsNullOrWhiteSpace(action.ReminderColor)
            && action.ReminderBaseDate is null)
            throw new ArgumentException("لا يوجد تذكير لإعادة جدولته");

        // حارس المدة الملتبسة (بيانات قديمة): مدة غير فارغة خارج المدد الأربع كانت
        // تُحسب صفرًا بصمت فيُسكت وسم `dueDateSuspect` — هنا تُرفض برسالة توجه
        // لنافذة الإجراءات بدل إصلاح صامت.
        var trimmedDuration = action.ReminderDuration?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDuration) && !ActionReminderCalculator.ValidDurations.Contains(trimmedDuration))
            throw new ArgumentException("مدة التذكير غير معروفة — عدّل الإجراء من نافذة الإجراءات");

        // الإسناد إلى تاريخ أساس التذكير وحده (`DateTime` خالص): تاريخ الإجراء واقعة
        // قانونية لا تُعاد كتابتها، ولا يُعاد تنسيق نصها — I1/I7.
        var oldBaseDate = action.ReminderBaseDate;
        action.ReminderBaseDate = due.AddDays(-ActionReminderCalculator.DurationDays(trimmedDuration));

        _actions.Update(action);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "action", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor(
                $"نقل التذكير إلى {due:yyyy-MM-dd} (تاريخ الأساس: {oldBaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—"} ← {action.ReminderBaseDate:yyyy-MM-dd}) — تاريخ الإجراء لم يتغير",
                doc), ct);
        return ExecutionActionService.ToDto(action, command.ActorName);
    }
}
