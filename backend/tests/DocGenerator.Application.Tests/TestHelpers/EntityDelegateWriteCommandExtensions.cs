using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعات <c>IEntityDelegateService</c> القديمة في كتابات
/// مندوبي الجهات فوق معالجات الأوامر — منطق الاختبارات كما هو (`P4-01bf`).
/// </summary>
public static class EntityDelegateWriteCommandExtensions
{
    public static Task<DelegateDto> CreateAsync(
        this ICommandHandler<CreateEntityDelegateCommand, DelegateDto> handler,
        CreateDelegateRequest request,
        string? actorName,
        CancellationToken ct = default,
        EntityRegistryActor? actor = null)
        => handler.HandleAsync(new CreateEntityDelegateCommand(request, actorName, actor), ct);

    public static Task<DelegateDto?> UpdateAsync(
        this ICommandHandler<UpdateEntityDelegateCommand, DelegateDto?> handler,
        int delegateUserId,
        UpdateDelegateRequest request,
        string? actorName,
        CancellationToken ct = default,
        EntityRegistryActor? actor = null)
        => handler.HandleAsync(new UpdateEntityDelegateCommand(delegateUserId, request, actorName, actor), ct);
}
