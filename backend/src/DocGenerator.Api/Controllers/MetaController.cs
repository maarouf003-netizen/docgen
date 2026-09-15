using DocGenerator.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

/// <summary>
/// بيانات النظام العامة للواجهة: سنة «قرار السنة» الحالية عند الخادم — الواجهة
/// تُظهر بها سنة التدوير/الإعادة بدل افتراض سنة جهاز العميل (تطابق غير مضمون عبر
/// النطاقات الزمنية). قراءة عامة ([AllowAnonymous]) وأجرى من ساعة النظام ومنطقته.
/// </summary>
[ApiController]
[Route("api/meta")]
public class MetaController : ControllerBase
{
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public MetaController(TimeProvider clock, TimeZoneInfo timeZone)
    {
        _clock = clock;
        _timeZone = timeZone;
    }

    /// <summary>سنة الخادم الحالية في منطقة النظام المقررة.</summary>
    [HttpGet("current-year")]
    [AllowAnonymous]
    public IActionResult CurrentYear()
        => Ok(new { currentYear = ServerClock.CurrentYear(_clock, _timeZone) });
}