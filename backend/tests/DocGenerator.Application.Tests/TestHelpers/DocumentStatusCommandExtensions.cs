using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعات <c>IDocumentStatusService</c> القديمة في
/// انتقالات الحالة فوق معالجات الأوامر — منطق الاختبارات كما هو (`P4-01bd`).
/// </summary>
public static class DocumentStatusCommandExtensions
{
    public static Task<bool> UpdateStatusAsync(
        this ICommandHandler<UpdateDocumentStatusCommand, bool> handler,
        int documentId,
        string status,
        Dictionary<string, string?> fields,
        string? actorName,
        CancellationToken ct = default,
        long? version = null)
        => handler.HandleAsync(new UpdateDocumentStatusCommand(documentId, status, fields, actorName, version), ct);

    public static Task<bool> RevertStatusAsync(
        this ICommandHandler<RevertDocumentStatusCommand, bool> handler,
        int documentId,
        Dictionary<string, string?> fields,
        string? actorName,
        CancellationToken ct = default,
        long? version = null)
        => handler.HandleAsync(new RevertDocumentStatusCommand(documentId, fields, actorName, version), ct);

    public static Task<bool> ReturnFromReferredToStartAsync(
        this ICommandHandler<ReturnFromReferredToStartCommand, bool> handler,
        int documentId,
        ReturnReferredToStartRequest request,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new ReturnFromReferredToStartCommand(documentId, request, actorName), ct);

    public static Task<bool> ConsiderExecutedByDelegationAsync(
        this ICommandHandler<ConsiderExecutedByDelegationCommand, bool> handler,
        int documentId,
        Dictionary<string, string?> fields,
        string? actorName,
        CancellationToken ct = default,
        long? version = null)
        => handler.HandleAsync(new ConsiderExecutedByDelegationCommand(documentId, fields, actorName, version), ct);

    public static Task<bool> UpdateExecutedStatusAsync(
        this ICommandHandler<UpdateExecutedStatusCommand, bool> handler,
        int documentId,
        string status,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateExecutedStatusCommand(documentId, status, null, actorName), ct);

    public static Task<bool> UpdateExecutedStatusAsync(
        this ICommandHandler<UpdateExecutedStatusCommand, bool> handler,
        int documentId,
        string status,
        ExecutedStatusRequest? request,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateExecutedStatusCommand(documentId, status, request, actorName), ct);

    public static Task<bool> RestoreStruckOffAsync(
        this ICommandHandler<RestoreStruckOffDocumentCommand, bool> handler,
        int documentId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RestoreStruckOffDocumentCommand(documentId, new RenewalRequest(), actorName), ct);

    public static Task<bool> RestoreStruckOffAsync(
        this ICommandHandler<RestoreStruckOffDocumentCommand, bool> handler,
        int documentId,
        RenewalRequest renewal,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RestoreStruckOffDocumentCommand(documentId, renewal, actorName), ct);
}
