using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>TransferAllAsync</c> القديم فوق معالج الأمر
/// <see cref="TransferAllDocumentsCommand"/> — منطق الاختبارات كما هو بلا أي سلوك إضافي.
/// </summary>
public static class DocumentTransferAllCommandExtensions
{
    public static Task<int> TransferAllAsync(
        this ICommandHandler<TransferAllDocumentsCommand, int> handler,
        int sourceLawyerId,
        int targetLawyerId,
        int? scopeBranchId,
        string? actorName,
        CancellationToken ct = default,
        string? idempotencyKey = null,
        int? ownerSectionId = null,
        int? actorUserId = null)
        => handler.HandleAsync(
            new TransferAllDocumentsCommand(
                sourceLawyerId, targetLawyerId, scopeBranchId, actorName,
                idempotencyKey, ownerSectionId, actorUserId), ct);
}
