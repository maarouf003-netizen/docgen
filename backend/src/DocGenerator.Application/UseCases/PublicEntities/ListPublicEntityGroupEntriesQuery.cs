using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>قيود مجموعة واحدة — لرئيس القسم (محافظته فقط) ولوحة إدارة الفروع؛ قراءة صرفة.</summary>
public sealed record ListPublicEntityGroupEntriesQuery(int GroupId, EntityRegistryActor Actor)
    : IQuery<IReadOnlyList<PublicEntityEntryDto>>;

internal sealed class ListPublicEntityGroupEntriesQueryHandler
    : QueryHandlerBase<ListPublicEntityGroupEntriesQuery, IReadOnlyList<PublicEntityEntryDto>>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;

    public ListPublicEntityGroupEntriesQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IEnumerable<IPipelineBehavior<ListPublicEntityGroupEntriesQuery, IReadOnlyList<PublicEntityEntryDto>>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
    }

    protected override async Task<IReadOnlyList<PublicEntityEntryDto>> HandleCoreAsync(
        ListPublicEntityGroupEntriesQuery query, CancellationToken ct)
    {
        var groupId = query.GroupId;
        var actor = query.Actor;
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive) throw new ArgumentException("المجموعة غير نشطة");

        var entries = await _entities.ListEntriesByGroupAsync(groupId, ct);
        var filtered = entries.Where(e => e.IsActive).ToList();

        // نطاق الرئيس (قسم/شعبة): محافظته فقط — مع ضمان ظهور قيد «الجهة الأم» دائمًا (F4).
        if (actor.Role is UserRole.Head or UserRole.SubHead && actor.BranchId.HasValue)
        {
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            var gov = PublicEntityService.NormalizeOptional(branch?.Governorate);
            if (gov is not null)
                filtered = filtered.Where(e => e.IsParentEntity || e.Governorate == gov).ToList();
            else
                filtered = new List<PublicEntity>();
        }

        return filtered
            .OrderBy(e => e.Governorate, StringComparer.Ordinal)
            .ThenBy(e => e.BranchName, StringComparer.Ordinal)
            .Select(e => PublicEntityService.ToEntryDto(group, e))
            .ToList();
    }
}
