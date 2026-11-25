using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ExecutionActions;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// سجل مساعدات إجراءات التنفيذ في الاختبارات: الكتابة (`SeedInitialActionsAsync`)
/// والقراءة (`GetExecutionActionsQuery`) — بدل واجهة الخدمة القديمة.
/// </summary>
public sealed record ExecutionActionHandlers(
    DocGenerator.Application.Services.IExecutionActionService Writes,
    IQueryHandler<GetExecutionActionsQuery, List<ExecutionActionDto>> Read);

/// <summary>جسر اختباري يحافظ على توقيع قراءة الإجراءات القديم فوق معالج الاستعلام.</summary>
public static class ExecutionActionReadQueryExtensions
{
    public static Task<List<ExecutionActionDto>> GetExecutionActionsAsync(
        this ExecutionActionHandlers handlers,
        int documentId,
        CancellationToken ct = default)
        => handlers.Read.HandleAsync(new GetExecutionActionsQuery(documentId), ct);
}
