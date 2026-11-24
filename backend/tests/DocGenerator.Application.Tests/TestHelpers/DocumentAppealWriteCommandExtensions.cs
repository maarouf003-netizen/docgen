using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Appeals;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>CreateAsync</c>/<c>UpdateAsync</c> القديمين
/// فوق معالجَي الاستئناف — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentAppealWriteCommandExtensions
{
    public static Task<AppealDto> CreateAsync(
        this ICommandHandler<CreateDocumentAppealCommand, AppealDto> handler,
        int documentId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new CreateDocumentAppealCommand(documentId, request, userId, actorName), ct);

    public static Task<AppealDto?> UpdateAsync(
        this ICommandHandler<UpdateDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> UpdateRegistrationAsync(
        this ICommandHandler<UpdateAppealRegistrationCommand, AppealDto?> handler,
        int appealId,
        UpdateAppealRegistrationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateAppealRegistrationCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> AssignAsync(
        this ICommandHandler<AssignDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        AssignAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new AssignDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> TransferAsync(
        this ICommandHandler<TransferDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        TransferAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new TransferDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<int> TransferAllAsync(
        this ICommandHandler<TransferAllDocumentAppealsCommand, int> handler,
        TransferAllAppealsRequest request,
        int? headBranchId,
        int? ownerSectionId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new TransferAllDocumentAppealsCommand(request, headBranchId, ownerSectionId, actorName), ct);

    public static Task<AppealDto?> DecideAsync(
        this ICommandHandler<DecideDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        DecideAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new DecideDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> StrikeAsync(
        this ICommandHandler<StrikeDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        StrikeAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new StrikeDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> ForwardAsync(
        this ICommandHandler<ForwardDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        ForwardAppealRequest request,
        int callerUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new ForwardDocumentAppealCommand(appealId, request, callerUserId, actorName), ct);

    public static Task<AppealDto?> RecallForwardAsync(
        this ICommandHandler<RecallAppealForwardCommand, AppealDto?> handler,
        int appealId,
        int callerUserId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new RecallAppealForwardCommand(appealId, callerUserId, actorName), ct);

    public static Task<AppealActionDto> AddActionAsync(
        this ICommandHandler<AddAppealActionCommand, AppealActionDto> handler,
        int appealId, AddAppealActionRequest request, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new AddAppealActionCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealActionDto?> UpdateActionAsync(
        this ICommandHandler<UpdateAppealActionCommand, AppealActionDto?> handler,
        int appealId, int actionId, UpdateAppealActionRequest request, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new UpdateAppealActionCommand(appealId, actionId, request, userId, actorName), ct);

    public static Task<bool> DeleteActionAsync(
        this ICommandHandler<DeleteAppealActionCommand, bool> handler,
        int appealId, int actionId, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new DeleteAppealActionCommand(appealId, actionId, userId, actorName), ct);

    public static Task<AppealActionDto> SetReminderAsync(
        this ICommandHandler<SetAppealReminderCommand, AppealActionDto> handler,
        int appealId, int actionId, SetAppealReminderRequest request, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new SetAppealReminderCommand(appealId, actionId, request, userId, actorName), ct);

    public static Task<bool> ClearReminderAsync(
        this ICommandHandler<ClearAppealReminderCommand, bool> handler,
        int appealId, int actionId, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new ClearAppealReminderCommand(appealId, actionId, userId, actorName), ct);

    public static Task<AppealActionDto> RescheduleAppealReminderAsync(
        this ICommandHandler<RescheduleAppealReminderCommand, AppealActionDto> handler,
        int appealId, int actionId, string? newDueDate, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new RescheduleAppealReminderCommand(appealId, actionId, newDueDate, userId, actorName), ct);

    public static Task<bool> SaveBaseNumbersAsync(
        this ICommandHandler<SaveAppealBaseNumbersCommand, bool> handler,
        int appealId, SaveAppealBaseNumbersRequest request, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new SaveAppealBaseNumbersCommand(appealId, request, userId, actorName), ct);

    public static Task<bool> CorrectBaseNumberAsync(
        this ICommandHandler<CorrectAppealBaseNumberCommand, bool> handler,
        int appealId, int year, CorrectAppealBaseNumberRequest request, int userId, string? actorName, CancellationToken ct = default)
        => handler.HandleAsync(new CorrectAppealBaseNumberCommand(appealId, year, request, userId, actorName), ct);
}
