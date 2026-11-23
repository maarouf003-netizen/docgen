using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>DeleteAsync</c> القديم فوق معالج الأمر
/// <see cref="DeleteDocumentCommand"/> — منطق الاختبارات كما هو بلا أي سلوك إضافي.
/// </summary>
public static class DocumentDeleteCommandExtensions
{
    public static Task<bool> DeleteAsync(
        this ICommandHandler<DeleteDocumentCommand, bool> handler,
        int documentId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new DeleteDocumentCommand(documentId, actorName), ct);
}
