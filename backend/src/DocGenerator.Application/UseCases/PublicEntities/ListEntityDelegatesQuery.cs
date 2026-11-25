using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>
/// قائمة حسابات مندوبي الجهات داخل نطاق الفاعل: رئيس القسم/الشعبة يرى مندوبي
/// محافظة فرعه فقط، والإدارة ترى الكل — قراءة صرفة بلا كتابة.
/// </summary>
public sealed record ListEntityDelegatesQuery(EntityRegistryActor? Actor = null) : IQuery<List<DelegateDto>>;

internal sealed class ListEntityDelegatesQueryHandler
    : QueryHandlerBase<ListEntityDelegatesQuery, List<DelegateDto>>
{
    private readonly IUserRepository _users;
    private readonly IPublicEntityRepository _registry;
    private readonly IRepository<Branch>? _branches;

    public ListEntityDelegatesQueryHandler(
        IUserRepository users,
        IPublicEntityRepository registry,
        IEnumerable<IPipelineBehavior<ListEntityDelegatesQuery, List<DelegateDto>>> behaviors,
        IRepository<Branch>? branches = null)
        : base(behaviors)
    {
        _users = users;
        _registry = registry;
        // مستودع الفروع لنطاق المحافظة اختياري التركيب (اختبارات قديمة بلا فاعل) —
        // الإنتاج يمرره دائمًا عبر `DI`؛ غيابه مع فاعل رئيس = بلا قيد (موثق).
        _branches = branches;
    }

    protected override async Task<List<DelegateDto>> HandleCoreAsync(
        ListEntityDelegatesQuery query, CancellationToken ct)
    {
        var delegates = await _users.ListEntityManagersAsync(ct);
        var governorate = await EntityDelegateProjection.HeadGovernorateAsync(query.Actor, _branches, ct);
        var result = new List<DelegateDto>(delegates.Count);
        foreach (var d in delegates)
        {
            if (governorate is not null
                && !await EntityDelegateProjection.IsScopeInGovernorateAsync(
                    d.PortalGroupId, d.PortalEntryId, governorate, _registry, ct))
                continue;
            result.Add(await EntityDelegateProjection.BuildDtoWithScopeAsync(d, _registry, ct));
        }
        return result;
    }
}
