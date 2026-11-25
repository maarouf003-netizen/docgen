using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>جسر اختباري يحافظ على توقيع قراءة مندوبي الجهات القديم فوق معالج الاستعلام.</summary>
public static class EntityDelegateReadQueryExtensions
{
    public static Task<List<DelegateDto>> ListAsync(
        this IQueryHandler<ListEntityDelegatesQuery, List<DelegateDto>> handler,
        CancellationToken ct = default,
        EntityRegistryActor? actor = null)
        => handler.HandleAsync(new ListEntityDelegatesQuery(actor), ct);
}
