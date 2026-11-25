using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// تفاصيل كاملة (الخطة §9.2): المستلم بأولوية مطلقة بلا شرط فرع، ثم المنشئ،
/// ثم المدير/المشرف؛ غير ذلك <c>null</c> (يُترجم إلى 404 بلا تسريب وجود).
/// </summary>
public sealed record GetHeadAlertDetailsQuery(
    int Id,
    HeadAlertActor Actor) : IQuery<HeadAlertDto?>;

internal sealed class GetHeadAlertDetailsQueryHandler
    : QueryHandlerBase<GetHeadAlertDetailsQuery, HeadAlertDto?>
{
    private readonly IHeadAlertRepository _alerts;

    public GetHeadAlertDetailsQueryHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<GetHeadAlertDetailsQuery, HeadAlertDto?>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override async Task<HeadAlertDto?> HandleCoreAsync(
        GetHeadAlertDetailsQuery request, CancellationToken ct)
    {
        var id = request.Id;
        var actor = request.Actor;

        ArgumentNullException.ThrowIfNull(actor);

        // صف واحد مسقط في SQL — لا «اجلب القائمة وابحث فيها» ولا كيانات محمَّلة (§9.2).
        var row = await _alerts.GetDetailRowAsync(id, actor.UserId, ct);
        if (row is null)
            return null;

        var isCreator = row.CreatedById == actor.UserId;
        var isOversight = actor.IsManagerOrAdmin;

        // المستلم أولًا بأولوية مطلقة (بلا شرط فرع — إنابات خارجية)، ثم المنشئ،
        // ثم الإشراف؛ غير ذلك 404 بلا كشف وجود (D2/§12).
        if (!row.IsRecipient && !isCreator && !isOversight)
            return null;

        return HeadAlertReadHelpers.ToDetailDto(row, canViewRecipients: isOversight || isCreator);
    }
}
