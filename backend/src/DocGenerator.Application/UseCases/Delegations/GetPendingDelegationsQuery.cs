using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Delegations;

/// <summary>
/// طلبات الإنابة المعلّقة لنطاق الرئيس (§7.1): رئيس القسم (دوائر القسم وبلا
/// دائرة + الخارجية غير الموجَّهة في فرعه)، ورئيس الشعبة (دوائر شعبته +
/// الخارجية الموجَّهة لشعبته). `RejectedOnly` لفلتر «مرفوض بانتظار التصحيح» (§7.4).
/// </summary>
public sealed record GetPendingDelegationsQuery(
    int BranchId,
    int? OwnerSectionId = null,
    bool RejectedOnly = false) : IQuery<List<DelegationDto>>;

internal sealed class GetPendingDelegationsQueryHandler
    : QueryHandlerBase<GetPendingDelegationsQuery, List<DelegationDto>>
{
    private readonly IDelegationRepository _delegations;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetPendingDelegationsQueryHandler(
        IDelegationRepository delegations,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetPendingDelegationsQuery, List<DelegationDto>>> behaviors)
        : base(behaviors)
    {
        _delegations = delegations;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<DelegationDto>> HandleCoreAsync(
        GetPendingDelegationsQuery query, CancellationToken ct)
    {
        var delegations = await _delegations.ListPendingByBranchAsync(query.BranchId, query.OwnerSectionId, query.RejectedOnly, ct);
        return delegations.Select(d => DocumentDelegationService.ToDto(d, d.SourceDocument, ServerClock.CurrentYear(_clock, _timeZone))).ToList();
    }
}
