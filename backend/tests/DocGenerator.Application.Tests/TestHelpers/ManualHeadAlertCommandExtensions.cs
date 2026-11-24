using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Alerts;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسر اختباري ضيق: يحافظ على توقيع <c>IHeadAlertService.CreateAsync</c> القديم فوق
/// معالج الأمر — منطق الاختبارات كما هو (`P4-01bi`).
/// </summary>
public static class ManualHeadAlertCommandExtensions
{
    public static Task<HeadAlertSummaryDto> CreateAsync(
        this ICommandHandler<CreateManualHeadAlertCommand, HeadAlertSummaryDto> handler,
        CreateHeadAlertRequest request,
        HeadAlertActor actor,
        string? actorName,
        CancellationToken ct = default)
        => handler.HandleAsync(new CreateManualHeadAlertCommand(request, actor, actorName), ct);
}
