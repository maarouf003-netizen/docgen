using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>
/// مراسلات ملف محدد: لمالك الملف ورئيس نطاقه (قسمه لدوائر القسم وبلا دائرة،
/// وشعبته لدوائر شعبته) والمدير/المشرف ومتابعيه (إحالة/إنابة/استئناف)،
/// ولمندوب الجهة المراسلات التي هو طرف فيها على هذا الملف.
/// </summary>
public sealed record ListCorrespondencesByDocumentQuery(
    int DocumentId,
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : IQuery<List<CorrespondenceListItemDto>>;

internal sealed class ListCorrespondencesByDocumentQueryHandler
    : QueryHandlerBase<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>>
{
    private readonly ICorrespondenceRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly IPortalRepository _portal;
    private readonly IBranchRepository _branches;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ListCorrespondencesByDocumentQueryHandler(
        ICorrespondenceRepository letters,
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        IPortalRepository portal,
        IBranchRepository branches,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ListCorrespondencesByDocumentQuery, List<CorrespondenceListItemDto>>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _documents = documents;
        _circuits = circuits;
        _appeals = appeals;
        _delegations = delegations;
        _portal = portal;
        _branches = branches;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<CorrespondenceListItemDto>> HandleCoreAsync(
        ListCorrespondencesByDocumentQuery query, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(query.DocumentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        if (!await MayListDocumentAsync(document, query.ActorUserId, query.Role,
                query.ActorBranchId, query.ActorSectionId, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على مراسلات هذا الملف");

        var letters = await _letters.ListByDocumentAsync(query.DocumentId, ct);

        // مندوب الجهة يرى من مراسلات الملف ما هو طرف فيه فقط — حتى داخل النطاق.
        if (query.Role == UserRole.EntityManager)
            letters = letters
                .Where(l => l.CreatedById == query.ActorUserId || l.TargetUserId == query.ActorUserId)
                .ToList();

        // رئيس القسم يرى من مراسلات الملف ما يقع في فرعه أو محافظته فقط —
        // يغلق تسريبًا عبر ملف قديم بلا فرع يحمل مراسلة فرع آخر.
        if (query.Role == UserRole.Head && query.ActorBranchId is not null)
        {
            var headGovernorate = await CorrespondenceService.ResolveHeadGovernorateAsync(
                _branches, query.ActorBranchId.Value, ct);
            letters = letters
                .Where(l => l.BranchId == query.ActorBranchId
                    || (l.BranchId is null && l.Governorate == headGovernorate))
                .ToList();
        }

        return letters
            .Select(l => CorrespondenceService.ToListItem(l, query.ActorUserId, _clock, _timeZone))
            .ToList();
    }

    private async Task<bool> MayListDocumentAsync(Document document, int actorUserId,
        UserRole role, int? actorBranchId, int? actorSectionId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Manager or UserRole.Admin:
                return true;
            case UserRole.Head:
                return actorBranchId is not null
                    && (actorBranchId == document.BranchId || document.BranchId is null);
            case UserRole.SubHead:
                // نطاق الشعبة (§10): فرعه ودائرة شعبته — بلا تدهور. حارس null
                // صريح (مرآة نمط Head أعلاه): رئيس مشوَّه بلا فرع مرفوض دائمًا.
                if (actorBranchId is null || actorBranchId != document.BranchId || actorSectionId is null)
                    return false;
                if (document.ExecutionCircuitId is null)
                    return false;
                var circuit = document.ExecutionCircuit
                    ?? await _circuits.GetByIdAsync(document.ExecutionCircuitId.Value, ct);
                return circuit?.SectionId == actorSectionId;
            case UserRole.Lawyer:
                return document.CreatedById == actorUserId
                    || await CorrespondenceService.FollowsDocumentAsync(
                        _appeals, _delegations, document.Id, actorUserId, ct);
            case UserRole.EntityManager:
                return await CorrespondenceService.IsDocumentInDelegateScopeAsync(
                    _portal, document.Id, actorUserId, ct);
            default:
                return false;
        }
    }
}
