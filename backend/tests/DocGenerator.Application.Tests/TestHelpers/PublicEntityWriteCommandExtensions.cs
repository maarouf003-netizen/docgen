using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.PublicEntities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعات خدمة السجل القديمة في كتابات
/// سجل الجهات فوق معالجات الأوامر — منطق الاختبارات كما هو (`P4-01be`).
/// </summary>
public static class PublicEntityWriteCommandExtensions
{
    public static Task<PublicEntityEntryDto> CreateAsync(
        this ICommandHandler<CreatePublicEntityCommand, PublicEntityEntryDto> handler,
        CreatePublicEntityRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new CreatePublicEntityCommand(request, actor), ct);

    public static Task<PublicEntityEntryDto?> UpdateAsync(
        this ICommandHandler<UpdatePublicEntityCommand, PublicEntityEntryDto?> handler,
        int entryId,
        UpdatePublicEntityRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdatePublicEntityCommand(entryId, request, actor), ct);

    public static Task<PublicEntityEntryDto?> AddAliasAsync(
        this ICommandHandler<AddPublicEntityAliasCommand, PublicEntityEntryDto?> handler,
        int entryId,
        AddPublicEntityAliasRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new AddPublicEntityAliasCommand(entryId, request, actor), ct);

    public static Task<PublicEntityEntryDto?> ApproveReviewAsync(
        this ICommandHandler<ApprovePublicEntityReviewCommand, PublicEntityEntryDto?> handler,
        int entryId,
        EntityRegistryActor actor,
        CancellationToken ct = default)
        => handler.HandleAsync(new ApprovePublicEntityReviewCommand(entryId, actor), ct);
}
