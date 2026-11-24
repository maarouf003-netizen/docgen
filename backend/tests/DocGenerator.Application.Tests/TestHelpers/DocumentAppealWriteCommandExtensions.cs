using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Appeals;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيعَي <c>CreateAsync</c>/<c>UpdateAsync</c> القديمين
/// فوق معالجَي الاستئناف — منطق الاختبارات كما هو.
/// </summary>
public static class DocumentAppealWriteCommandExtensions
{
    public static Task<AppealDto> CreateAsync(
        this ICommandHandler<CreateDocumentAppealCommand, AppealDto> handler,
        int documentId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new CreateDocumentAppealCommand(documentId, request, userId, actorName), ct);

    public static Task<AppealDto?> UpdateAsync(
        this ICommandHandler<UpdateDocumentAppealCommand, AppealDto?> handler,
        int appealId,
        UpsertAppealRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateDocumentAppealCommand(appealId, request, userId, actorName), ct);

    public static Task<AppealDto?> UpdateRegistrationAsync(
        this ICommandHandler<UpdateAppealRegistrationCommand, AppealDto?> handler,
        int appealId,
        UpdateAppealRegistrationRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new UpdateAppealRegistrationCommand(appealId, request, userId, actorName), ct);
}
