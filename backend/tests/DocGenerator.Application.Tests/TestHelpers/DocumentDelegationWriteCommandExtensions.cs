using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>CreateAsync</c>/<c>UpdateAsync</c> القديمين
/// فوق معالجَي الأمر — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentDelegationWriteCommandExtensions
{
    public static Task<DelegationDto> CreateAsync(
        this ICommandHandler<CreateDocumentDelegationCommand, DelegationDto> handler,
        int sourceDocumentId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new CreateDocumentDelegationCommand(sourceDocumentId, request, userId, actorName), ct);

    public static Task<DelegationDto?> UpdateAsync(
        this ICommandHandler<UpdateDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        UpsertDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new UpdateDocumentDelegationCommand(delegationId, request, userId, actorName), ct);
}
