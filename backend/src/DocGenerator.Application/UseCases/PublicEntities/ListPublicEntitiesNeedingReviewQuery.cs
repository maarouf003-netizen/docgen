using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قيود بانتظار مراجعة رئيس القسم ضمن نطاقه (المدير/المشرف يرىان الكل) — قراءة صرفة.</summary>
public sealed record ListPublicEntitiesNeedingReviewQuery(EntityRegistryActor Actor)
    : IQuery<List<PublicEntityEntryDto>>;

internal sealed class ListPublicEntitiesNeedingReviewQueryHandler
    : QueryHandlerBase<ListPublicEntitiesNeedingReviewQuery, List<PublicEntityEntryDto>>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;

    public ListPublicEntitiesNeedingReviewQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IEnumerable<IPipelineBehavior<ListPublicEntitiesNeedingReviewQuery, List<PublicEntityEntryDto>>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
    }

    /// <summary>
    /// قائمة «بحاجة مراجعة»: الرئيس (قسم/شعبة) يرى ما أدخله محامو فرعه (بغض النظر عن محافظة
    /// الجهة نفسها — قد يُقيم محامٍ ملفًا تنفيذيًا على جهة تتبع محافظة أخرى)، والمدير/
    /// المشرف يرىان كل السجل. رئيس بلا فرع مضبوط تعني قائمة فارغة.
    /// </summary>
    protected override async Task<List<PublicEntityEntryDto>> HandleCoreAsync(
        ListPublicEntitiesNeedingReviewQuery query, CancellationToken ct)
    {
        var actor = query.Actor;
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return new List<PublicEntityEntryDto>();
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        return groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => x.Entry.NeedsReview)
            .Where(x => headBranchId is null
                || (x.Entry.CreatedBy != null && x.Entry.CreatedBy.BranchId == headBranchId))
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Select(x => PublicEntityService.ToEntryDto(x.Group, x.Entry))
            .ToList();
    }
}
