using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري ضيق لتوقيع <c>CommitImportAsync</c> القديم (`P4-01bn`).</summary>
public static class CommitPublicEntityImportCommandExtensions
{
    public static Task<ImportCommitResultDto> CommitImportAsync(
        this ICommandHandler<CommitPublicEntityImportCommand, ImportCommitResultDto> handler,
        ImportCommitRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default,
        string? idempotencyKey = null)
        => handler.HandleAsync(
            new CommitPublicEntityImportCommand(request, actorUserId, actorName, idempotencyKey), ct);
}
