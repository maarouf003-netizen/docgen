using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>DeleteAsync</c> القديم فوق معالج الأمر
/// <see cref="DeleteDocumentDelegationCommand"/> — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentDelegationCommandExtensions
{
    public static Task<bool> DeleteAsync(
        this ICommandHandler<DeleteDocumentDelegationCommand, bool> handler,
        int delegationId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new DeleteDocumentDelegationCommand(delegationId, userId, actorName), ct);
}
