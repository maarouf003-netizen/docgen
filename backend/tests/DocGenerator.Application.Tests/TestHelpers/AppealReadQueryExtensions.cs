using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Appeals;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات الاستئنافات (`P4-01cm`): تحافظ على توقيعات
/// الخدمة القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class AppealReadQueryExtensions
{
    public static Task<List<AppealDto>> ListForDocumentAsync(
        this IQueryHandler<ListAppealsForDocumentQuery, List<AppealDto>> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListAppealsForDocumentQuery(documentId), ct);

    public static Task<PagedResult<AppealDto>> SearchAsync(
        this IQueryHandler<SearchAppealsQuery, PagedResult<AppealDto>> handler,
        string? query,
        string? status,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId,
        int page,
        int perPage,
        CancellationToken ct = default)
        => handler.HandleAsync(
            new SearchAppealsQuery(query, status, visibleBranchId, visibleUserId, ownerSectionId, page, perPage),
            ct);

    public static Task<AppealDto?> GetAsync(
        this IQueryHandler<GetAppealQuery, AppealDto?> handler,
        int appealId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetAppealQuery(appealId), ct);

    public static Task<int> CountByAssigneeForHeadAsync(
        this IQueryHandler<CountAssigneeAppealsForHeadQuery, int> handler,
        int assigneeId,
        int? headBranchId,
        int? ownerSectionId,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountAssigneeAppealsForHeadQuery(assigneeId, headBranchId, ownerSectionId), ct);

    public static Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(
        this IQueryHandler<GetAppealBaseNumberHistoryQuery, List<AppealBaseNumberHistoryDto>> handler,
        int appealId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetAppealBaseNumberHistoryQuery(appealId), ct);

    public static Task<List<AppealActionDto>> GetActionsAsync(
        this IQueryHandler<GetAppealActionsQuery, List<AppealActionDto>> handler,
        int appealId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetAppealActionsQuery(appealId), ct);

    public static Task<List<AppealReminderDto>> GetRemindersAsync(
        this IQueryHandler<GetAppealRemindersQuery, List<AppealReminderDto>> handler,
        int userId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetAppealRemindersQuery(userId), ct);
}
