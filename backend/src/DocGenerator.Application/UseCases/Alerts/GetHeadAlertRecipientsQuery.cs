using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// مستلمو التنبيه وحالة قراءتهم — للمنشئ أو المدير/المشرف فقط؛
/// غير ذلك <c>null</c> (يُترجم إلى 404).
/// </summary>
public sealed record GetHeadAlertRecipientsQuery(
    int Id,
    HeadAlertActor Actor) : IQuery<List<HeadAlertRecipientDto>?>;

internal sealed class GetHeadAlertRecipientsQueryHandler
    : QueryHandlerBase<GetHeadAlertRecipientsQuery, List<HeadAlertRecipientDto>?>
{
    private readonly IHeadAlertRepository _alerts;

    public GetHeadAlertRecipientsQueryHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<GetHeadAlertRecipientsQuery, List<HeadAlertRecipientDto>?>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override async Task<List<HeadAlertRecipientDto>?> HandleCoreAsync(
        GetHeadAlertRecipientsQuery request, CancellationToken ct)
    {
        var id = request.Id;
        var actor = request.Actor;

        ArgumentNullException.ThrowIfNull(actor);

        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null)
            return null;

        // منشئ أو مدير/مشرف فقط؛ أي دور آخر 404 بلا تسريب وجود (§9.3/§12).
        if (alert.CreatedById != actor.UserId && !actor.IsManagerOrAdmin)
            return null;

        var recipients = await _alerts.ListRecipientsWithUserAsync(id, ct);
        return recipients.Select(r => new HeadAlertRecipientDto(
            r.Id,
            r.UserId,
            r.User?.FullName ?? string.Empty,
            r.User?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            r.IsRead,
            r.ReadAt)).ToList();
    }
}
