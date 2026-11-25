using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Portal;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات البوابة (`P4-01co`): تحافظ على توقيعات الخدمة
/// القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class PortalReadQueryExtensions
{
    public static Task<PortalScopeDto?> GetMyScopeAsync(
        this IQueryHandler<GetPortalScopeQuery, PortalScopeDto?> handler,
        int userId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetPortalScopeQuery(userId), ct);

    public static Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        this IQueryHandler<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>> handler,
        int userId,
        string? query,
        string? status,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? entryId = null)
        => handler.HandleAsync(
            new ListPortalFilesQuery(userId, query, status, page, perPage, entryId), ct);

    public static Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(
        this IQueryHandler<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?> handler,
        int userId,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPortalAppealsQuery(userId, documentId), ct);

    public static Task<IReadOnlyList<PortalExecutionActionDto>?> ListExecutionActionsAsync(
        this IQueryHandler<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?> handler,
        int userId,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPortalExecutionActionsQuery(userId, documentId), ct);

    public static Task<IReadOnlyList<DelegationDto>?> ListDelegationsAsync(
        this IQueryHandler<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?> handler,
        int userId,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPortalDelegationsQuery(userId, documentId), ct);

    public static Task<IReadOnlyList<AppealDto>?> ListAppealDetailsAsync(
        this IQueryHandler<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?> handler,
        int userId,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPortalAppealDetailsQuery(userId, documentId), ct);

    public static Task<IReadOnlyList<BaseNumberHistoryDto>?> ListBaseNumbersAsync(
        this IQueryHandler<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?> handler,
        int userId,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new ListPortalBaseNumbersQuery(userId, documentId), ct);

    public static Task<PortalStatsDto> GetStatsAsync(
        this IQueryHandler<GetPortalStatsQuery, PortalStatsDto> handler,
        int userId,
        CancellationToken ct = default,
        int? entryId = null)
        => handler.HandleAsync(new GetPortalStatsQuery(userId, entryId), ct);
}
