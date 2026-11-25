using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Alerts;

/// <summary>
/// عدّاد غير المقروء لصفوف الفاعل نفسه (`D6`/`D7`): المحامي بلا قيد فرع،
/// ورئيس/شعبة داخل فرعه. المدير/المشرف يُرفضون <c>403</c>، ورئيس بلا فرع كذلك
/// (`D11`)، وقيمة نطاق غير معروفة <c>400</c>.
/// </summary>
public sealed record CountUnreadHeadAlertsQuery(
    HeadAlertActor Actor,
    AlertUnreadScope Scope) : IQuery<int>;

internal sealed class CountUnreadHeadAlertsQueryHandler
    : QueryHandlerBase<CountUnreadHeadAlertsQuery, int>
{
    private readonly IHeadAlertRepository _alerts;

    public CountUnreadHeadAlertsQueryHandler(
        IHeadAlertRepository alerts,
        IEnumerable<IPipelineBehavior<CountUnreadHeadAlertsQuery, int>> behaviors)
        : base(behaviors)
        => _alerts = alerts;

    protected override async Task<int> HandleCoreAsync(
        CountUnreadHeadAlertsQuery request, CancellationToken ct)
    {
        var actor = request.Actor;
        var scope = request.Scope;

        ArgumentNullException.ThrowIfNull(actor);

        // مفتاح صريح مغلق (مراجعة المرحلة 4 `ح‑22`): قيمة enum غير معروفة من مستدعٍ
        // داخلي تُرفض 400 بدل السقوط الصامت إلى النطاق الشخصي.
        return scope switch
        {
            AlertUnreadScope.User => await HeadAlertReadHelpers.CountUserScopeAsync(_alerts, actor, ct),
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope), scope, "نطاق عدّاد غير معروف — استخدم user"),
        };
    }
}
