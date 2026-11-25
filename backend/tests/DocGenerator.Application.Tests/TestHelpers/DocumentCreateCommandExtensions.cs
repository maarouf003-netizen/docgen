using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>CreateAsync</c> القديم فوق معالج الأمر
/// <see cref="CreateDocumentCommand"/> — فيبقى منطق الاختبارات كما هو بعد نقل الملكية
/// (نفس المعاملات ونفس الترتيب)، بلا أي سلوك إضافي.
/// </summary>
public static class DocumentCreateCommandExtensions
{
    public static Task<DocumentResponse> CreateAsync(
        this ICommandHandler<CreateDocumentCommand, DocumentResponse> handler,
        DocumentUpsertRequest request,
        int userId,
        string? actorName,
        int? branchId,
        CancellationToken ct = default,
        string? idempotencyKey = null)
        => handler.HandleAsync(
            new CreateDocumentCommand(request, userId, actorName, branchId, idempotencyKey), ct);
}
