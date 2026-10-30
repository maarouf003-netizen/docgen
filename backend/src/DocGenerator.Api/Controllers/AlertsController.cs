using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Controllers;

[ApiController]
[Route("api/alerts")]
// قرار المالك 2026-10-16: قراءة إشرافية للمدير/المشرف (القوائم والتفاصيل والمستلمون) —
// الكتابة (إصدار) والعدّادات والقراءة-تعليم تبقى مستلم-محددة.
//
// قرار `D8` (مراجعة PR-4 `ح‑1`): هذا المتحكم يترجم HTTP فقط — لا فحص دور/فرع/طرف هنا
// إطلاقًا، فالقرار كله في `HeadAlertService`: `ArgumentException` ⇒ 400،
// `UnauthorizedAccessException` ⇒ 403. تكرار الفحص في الطبقتين هو ما جعل
// `ListByDelegationAsync` آمنًا بحرس خارجي لا بعقده.
[Authorize(Roles = "lawyer,head,subhead,manager,admin")]
public class AlertsController : ControllerBase
{
    private readonly IHeadAlertService _alerts;

    public AlertsController(IHeadAlertService alerts) => _alerts = alerts;

    private string? ActorName => User.Identity?.Name;

    /// <summary>سياق الفاعل من التوكن — تمرير بيانات فقط، لا قرار صلاحية.</summary>
    private HeadAlertActor Actor => new(User.GetUserId(), User.GetRoleEnum(), User.GetBranchId());

    /// <summary>
    /// قائمة التنبيهات (§8: قراءة بالمستلم): المحامي تنبيهاته، ورئيس القسم/الشعبة
    /// ما استلمه في فرعه فقط، والمدير/المشرف قائمة الإشراف عبر الفروع (قراءة فقط).
    /// ملخصات مرقّمة بلا نص كامل — النص الكامل عبر <c>GET /{id}</c> وحده.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? archived,
        [FromQuery] bool unreadOnly,
        [FromQuery] int? delegationId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        try
        {
            var query = new HeadAlertListQuery
            {
                Archive = ParseArchiveFilter(archived),
                UnreadOnly = unreadOnly,
                DelegationId = delegationId,
                Page = BuildPageRequest(page, pageSize),
            };
            return Ok(await _alerts.ListForActorAsync(Actor, query, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// عدّاد غير المقروء للفاعل نفسه — المستلمون فقط (محامٍ أو رئيس نطاق)؛
    /// المدير/المشرف ورئيس بلا فرع يُرفضون في الخدمة (<c>403</c>).
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        try
        {
            return Ok(new { count = await _alerts.CountUnreadAsync(Actor, ct) });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// ما أصدره الرئيس في فرعه (تبويب «أرسلتها» — قراءة فقط): رئيس القسم
    /// والشعبة بفرع — بلا زر تعليم مقروء (المُرسِل ليس مستلمًا).
    /// </summary>
    [HttpGet("sent")]
    public async Task<IActionResult> Sent(
        [FromQuery] string? archived,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _alerts.ListSentAsync(
                Actor, ParseArchiveFilter(archived), BuildPageRequest(page, pageSize), ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>إصدار تنبيه — رئيس القسم/الشعبة لفرعه فقط. يعيد <c>201</c> بملخص الصف (D3).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateHeadAlertRequest request, CancellationToken ct)
    {
        try
        {
            var alert = await _alerts.CreateAsync(request, Actor, ActorName, ct);
            return CreatedAtAction(nameof(Get), new { id = alert.Id }, alert);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// تفاصيل كاملة للنص — المستلم بأولوية مطلقة (بلا شرط فرع)، ثم المنشئ، ثم
    /// المدير/المشرف؛ غير ذلك <c>404</c> بلا تسريب وجود (D2/§12).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var alert = await _alerts.GetDetailsAsync(id, Actor, ct);
        return alert is null ? NotFound() : Ok(alert);
    }

    /// <summary>
    /// مستلمو التنبيه وحالة قراءتهم — للمنشئ أو المدير/المشرف فقط؛ غير ذلك
    /// <c>404</c> بلا تسريب وجود (§9.3/§12).
    /// </summary>
    [HttpGet("{id:int}/recipients")]
    public async Task<IActionResult> Recipients(int id, CancellationToken ct)
    {
        var recipients = await _alerts.GetRecipientsAsync(id, Actor, ct);
        return recipients is null ? NotFound() : Ok(recipients);
    }

    /// <summary>
    /// تنبيهات الإنابة المدموجة (مرآة منيب ↔ مناب): تُعرض في بطاقتي «معلومات الملف المنيب»
    /// و«تشعبات الملف» بعلامة قراءةٍ بحسب المستخدم. محامي طرفٌ في الإنابة فقط.
    /// </summary>
    [HttpGet("by-delegation/{delegationId:int}")]
    public async Task<IActionResult> ByDelegation(
        int delegationId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        try
        {
            // قرار D1: الافتراضي للشريط صفحة واحدة بحد أقصى 100 — والترقيم صارم.
            // التحقق نفسه في الخدمة (بعد فحص الدور) كي لا يسبق 400 رفضَ الصلاحية.
            var request = BuildPageRequest(page, pageSize, AlertPageRequest.MaxPageSize);
            return Ok(await _alerts.ListByDelegationAsync(delegationId, Actor, request, ct));
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { message = e.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// تعليم التنبيه كمقروء — المستلم فقط (محامٍ أو رئيس نطاق)؛ يُبقي سجل
    /// المقروء تاريخًا لا يُرحَّل عند التعاقب. غير المستلم يُرفض (404).
    /// </summary>
    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _alerts.MarkReadAsync(id, Actor, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>يقرأ فلتر الأرشفة من الاستعلام: <c>active|all|only</c> (الفارغ = active).</summary>
    private static AlertArchiveFilter ParseArchiveFilter(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        null or "" or "active" => AlertArchiveFilter.Active,
        "all" => AlertArchiveFilter.All,
        "only" => AlertArchiveFilter.OnlyArchived,
        _ => throw new ArgumentException("قيمة archived غير صالحة — استخدم active أو all أو only"),
    };

    /// <summary>
    /// يبني طلب الصفحة بالافتراضيات المعلنة (page=1، pageSize=50، أو
    /// <paramref name="defaultPageSize"/> لمسار الشريط) — والتحقق النهائي
    /// (1..100) في <see cref="AlertPageRequest.Validate"/>، فلا يُستبدل 0 صامتًا.
    /// </summary>
    private static AlertPageRequest BuildPageRequest(
        int? page, int? pageSize, int defaultPageSize = AlertPageRequest.DefaultPageSize) => new()
        {
            Page = page ?? 1,
            PageSize = pageSize ?? defaultPageSize,
        };
}
