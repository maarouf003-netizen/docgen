using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Appeals;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Application.UseCases.Portal;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// يبني معالجات قراءات البوابة الثمانية (`P4-01co`) على قاعدة اختبارية واحدة وبنفس
/// الساعة/المنطقة المستعملة في اختبارات البوابة، بدل تكرار ربط الكائنات. أما
/// `PortalService` (تفاصيل الملف والتصدير المدقّقان) فيبنيه <see cref="PortalServiceFactory"/>.
/// </summary>
/// <summary>
/// حزمة معالجات قراءات البوابة الثمانية كما بُنيت في <see cref="PortalReadHandlerFactory"/>.
/// </summary>
public sealed record PortalReadHandlers(
    IQueryHandler<GetPortalScopeQuery, PortalScopeDto?> Scope,
    IQueryHandler<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>> Files,
    IQueryHandler<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?> Appeals,
    IQueryHandler<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?> ExecutionActions,
    IQueryHandler<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?> Delegations,
    IQueryHandler<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?> AppealDetails,
    IQueryHandler<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?> BaseNumbers,
    IQueryHandler<GetPortalStatsQuery, PortalStatsDto> Stats);

public static class PortalReadHandlerFactory
{
    public static PortalReadHandlers Create(DocGeneratorDbContext db, TimeProvider? clock = null)
    {
        var documents = new DocumentRepository(db);
        var portal = new PortalRepository(db);
        var appeals = new AppealRepository(db);
        var clockProvider = clock ?? TestClock.Provider;

        var appealReads = new ListAppealsForDocumentQueryHandler(
            documents,
            appeals,
            clockProvider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<ListAppealsForDocumentQuery, List<AppealDto>>>());

        var delegationReads = new GetDocumentDelegationsQueryHandler(
            documents,
            new DelegationRepository(db),
            clockProvider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<GetDocumentDelegationsQuery, List<DelegationDto>>>());

        return new PortalReadHandlers(
            new GetPortalScopeQueryHandler(
                portal,
                Array.Empty<IPipelineBehavior<GetPortalScopeQuery, PortalScopeDto?>>()),
            new ListPortalFilesQueryHandler(
                portal,
                Array.Empty<IPipelineBehavior<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>>>()),
            new ListPortalAppealsQueryHandler(
                portal,
                appeals,
                clockProvider,
                TestClock.TimeZone,
                Array.Empty<IPipelineBehavior<ListPortalAppealsQuery, IReadOnlyList<PortalAppealDto>?>>()),
            new ListPortalExecutionActionsQueryHandler(
                portal,
                documents,
                Array.Empty<IPipelineBehavior<ListPortalExecutionActionsQuery, IReadOnlyList<PortalExecutionActionDto>?>>()),
            new ListPortalDelegationsQueryHandler(
                portal,
                documents,
                delegationReads,
                Array.Empty<IPipelineBehavior<ListPortalDelegationsQuery, IReadOnlyList<DelegationDto>?>>()),
            new ListPortalAppealDetailsQueryHandler(
                portal,
                documents,
                appealReads,
                Array.Empty<IPipelineBehavior<ListPortalAppealDetailsQuery, IReadOnlyList<AppealDto>?>>()),
            new ListPortalBaseNumbersQueryHandler(
                portal,
                documents,
                clockProvider,
                TestClock.TimeZone,
                Array.Empty<IPipelineBehavior<ListPortalBaseNumbersQuery, IReadOnlyList<BaseNumberHistoryDto>?>>()),
            new GetPortalStatsQueryHandler(
                portal,
                clockProvider,
                TestClock.TimeZone,
                Array.Empty<IPipelineBehavior<GetPortalStatsQuery, PortalStatsDto>>()));
    }
}
