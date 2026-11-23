using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>UpdateAsync</c> القديم فوق معالج الأمر
/// <see cref="UpdateDocumentCommand"/> — فيبقى منطق الاختبارات كما هو بعد نقل الملكية
/// (نفس المعاملات ونفس الترتيب)، بلا أي سلوك إضافي.
/// </summary>
public static class DocumentUpdateCommandExtensions
{
    public static Task<DocumentResponse?> UpdateAsync(
        this ICommandHandler<UpdateDocumentCommand, DocumentResponse?> handler,
        int documentId,
        DocumentUpsertRequest request,
        string? actorName,
        int? userId = null,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateDocumentCommand(documentId, request, actorName, userId), ct);
}
