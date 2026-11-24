using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق لتوقيعات `IDocumentDelegationService` القديمة في مسارات
/// التوجيه/التراجع/الرفض — منطق الاختبارات كما هو (`P4-01bx`).
/// </summary>
public static class DocumentDelegationApprovalCommandExtensions
{
    public static Task<DelegationDto?> AssignAsync(
        this ICommandHandler<AssignDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        AssignDelegationRequest request,
        int userId,
        int? headBranchId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new AssignDocumentDelegationCommand(delegationId, request, userId, headBranchId, actorName), ct);

    public static Task<DelegationDto?> RedirectToSectionAsync(
        this ICommandHandler<RedirectDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        RedirectDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RedirectDocumentDelegationCommand(delegationId, request, userId, actorName), ct);

    public static Task<DelegationDto?> RecallRedirectAsync(
        this ICommandHandler<RecallDelegationRedirectCommand, DelegationDto?> handler,
        int delegationId,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RecallDelegationRedirectCommand(delegationId, userId, actorName), ct);

    public static Task<DelegationDto?> RejectAsync(
        this ICommandHandler<RejectDocumentDelegationCommand, DelegationDto?> handler,
        int delegationId,
        RejectDelegationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RejectDocumentDelegationCommand(delegationId, request, userId, actorName), ct);
}
