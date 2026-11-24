using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Correspondences;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعات <c>ICorrespondenceService</c> القديمة في كتابات
/// المراسلات فوق معالجات الأوامر — منطق الاختبارات كما هو (`P4-01bt`).
/// </summary>
public static class CorrespondenceWriteCommandExtensions
{
    public static Task<CorrespondenceDto> CreateAsync(
        this ICommandHandler<CreateCorrespondenceCommand, CorrespondenceDto> handler,
        CreateCorrespondenceRequest request,
        int actorUserId,
        string? actorName,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new CreateCorrespondenceCommand(request, actorUserId, actorName, role, actorBranchId, actorSectionId), ct);

    public static Task<CorrespondenceMessageDto> AddAddendumAsync(
        this ICommandHandler<AddCorrespondenceAddendumCommand, CorrespondenceMessageDto> handler,
        int correspondenceId,
        AddCorrespondenceAddendumRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new AddCorrespondenceAddendumCommand(correspondenceId, request, actorUserId, actorName), ct);

    public static Task<CorrespondenceMessageDto> ReplyAsync(
        this ICommandHandler<ReplyCorrespondenceCommand, CorrespondenceMessageDto> handler,
        int correspondenceId,
        ReplyCorrespondenceRequest request,
        int actorUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new ReplyCorrespondenceCommand(correspondenceId, request, actorUserId, actorName), ct);

    public static Task<CorrespondenceReceiptDto> MarkSeenAsync(
        this ICommandHandler<MarkCorrespondenceSeenCommand, CorrespondenceReceiptDto> handler,
        int correspondenceId,
        int actorUserId,
        string? actorName,
        UserRole role,
        int? actorBranchId,
        CancellationToken ct = default,
        int? actorSectionId = null)
        => handler.HandleAsync(
            new MarkCorrespondenceSeenCommand(correspondenceId, actorUserId, actorName, role, actorBranchId, actorSectionId), ct);
}
