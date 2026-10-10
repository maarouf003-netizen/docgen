using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Security;

/// <summary>
/// سمة قبول واحد: توليد ملف ثقيل واحد جارٍ لكل فاعل — الثاني يُردّ `429`
/// برسالة مهذبة وترويسة `Retry-After` بدل تكديس الذاكرة. تُطبَّق على نقاط
/// تنزيل المصنفات الأربع (مستندات/دوائر/بوابة/سجل تغييرات)؛ توليد `Word`
/// أحادي المستند مستثنى عمدًا (محدود ومخفوض أصلًا بالمحدد العام).
/// تعمل بعد المصادقة (تحتاج هوية المستخدم)، وتُحرَّر الخانة في `finally`
/// حتى عند الإلغاء أو الفشل. الرد المباشر (بلا رمي) عمدًا: الانشغال تدفق
/// تحكم طبيعي لا خطأ — فلا يُلوَّث سجل الأخطاء.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class SingleExportPerUserAttribute : Attribute, IAsyncActionFilter
{
    public const string BusyMessage = "لديك تصدير قيد التنفيذ — انتظر انتهاءه قبل بدء تصدير جديد";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var guard = http.RequestServices.GetRequiredService<ExportConcurrencyGuard>();
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var key = ExportConcurrencyGuard.KeyFor(userId, http.Connection.RemoteIpAddress?.ToString());
        if (!guard.TryEnter(key))
        {
            http.Response.Headers.RetryAfter = "30";
            context.Result = new JsonResult(new { message = BusyMessage })
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
            };
            return;
        }
        try
        {
            await next();
        }
        finally
        {
            guard.Exit(key);
        }
    }
}
