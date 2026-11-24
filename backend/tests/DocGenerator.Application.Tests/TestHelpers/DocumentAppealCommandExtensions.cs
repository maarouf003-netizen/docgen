using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.UseCases.Appeals;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>DeleteAsync</c> القديم فوق معالج الأمر
/// <see cref="DeleteDocumentAppealCommand"/> — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentAppealCommandExtensions
{
    public static Task<bool> DeleteAsync(
        this ICommandHandler<DeleteDocumentAppealCommand, bool> handler,
        int appealId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new DeleteDocumentAppealCommand(appealId, userId, actorName), ct);
}
