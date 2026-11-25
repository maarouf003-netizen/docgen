using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>عدد قيود «بانتظار المراجعة» ضمن نطاق الفاعل — شارة خفيفة دون تحميل القائمة.</summary>
public sealed record CountPublicEntitiesNeedingReviewQuery(EntityRegistryActor Actor) : IQuery<int>;

internal sealed class CountPublicEntitiesNeedingReviewQueryHandler
    : QueryHandlerBase<CountPublicEntitiesNeedingReviewQuery, int>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;

    public CountPublicEntitiesNeedingReviewQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IEnumerable<IPipelineBehavior<CountPublicEntitiesNeedingReviewQuery, int>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
    }

    protected override async Task<int> HandleCoreAsync(CountPublicEntitiesNeedingReviewQuery query, CancellationToken ct)
    {
        var actor = query.Actor;
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return 0;
        }

        return await _entities.CountNeedsReviewAsync(headBranchId, ct);
    }
}
