using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Users;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>CreateLawyerAsync</c>/<c>UpdateLawyerAsync</c>
/// القديمين فوق معالجي الأوامر — منطق الاختبارات كما هو (`P4-01bg`).
/// </summary>
public static class LawyerWriteCommandExtensions
{
    public static Task<LawyerListItemDto> CreateLawyerAsync(
        this ICommandHandler<CreateLawyerCommand, LawyerListItemDto> handler,
        int branchId,
        CreateLawyerRequest request,
        string? actorName,
        CancellationToken ct = default,
        int? actorUserId = null)
        => handler.HandleAsync(new CreateLawyerCommand(branchId, request, actorName, actorUserId), ct);

    public static Task<LawyerListItemDto?> UpdateLawyerAsync(
        this ICommandHandler<UpdateLawyerCommand, LawyerListItemDto?> handler,
        int userId,
        UpdateLawyerRequest request,
        int? scopeBranchId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateLawyerCommand(userId, request, scopeBranchId, actorName), ct);
}
