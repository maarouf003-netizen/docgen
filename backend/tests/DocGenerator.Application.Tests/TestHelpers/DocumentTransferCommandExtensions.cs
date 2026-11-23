using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>TransferAsync</c> القديم فوق معالج الأمر
/// <see cref="TransferDocumentCommand"/> — منطق الاختبارات كما هو بلا أي سلوك إضافي.
/// </summary>
public static class DocumentTransferCommandExtensions
{
    public static Task<DocumentResponse> TransferAsync(
        this ICommandHandler<TransferDocumentCommand, DocumentResponse> handler,
        int documentId,
        int targetLawyerId,
        string? actorName,
        CancellationToken ct = default,
        int? actorUserId = null)
        => handler.HandleAsync(
            new TransferDocumentCommand(documentId, targetLawyerId, actorName, actorUserId), ct);
}
