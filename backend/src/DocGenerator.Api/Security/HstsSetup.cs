using Microsoft.AspNetCore.HttpsPolicy;

namespace DocGenerator.Api.Security;

/// <summary>
/// إعداد <c>HSTS</c> من رايات البيئة: المدة و<c>preload</c> مستقلان عن التفعيل نفسه،
/// لأن تفعيل <c>HSTS</c> قرار تشغيلي قابل للتراجع، أما <c>preload</c> فالتزام شبه دائم
/// في قوائم المتصفحات المدمجة — لا يجوز أن يتحركا بمفتاح واحد.
/// </summary>
public static class HstsSetup
{
    /// <summary>الحد الأدنى لمدة <c>HSTS</c> لقبول <c>preload</c>: سنة واحدة بالثواني.</summary>
    public const int PreloadMinimumMaxAgeDays = 365;

    /// <summary>
    /// يتحقق من اتساق الرايات بغضّ النظر عن التفعيل: <c>preload</c> بلا <c>HstsEnabled</c>
    /// كان سيُتجاهَل بصمت (ترويسة لا تُرسَل أصلًا)، والمدة غير الموجبة لا معنى لها.
    /// يُستدعى من <c>Program.cs</c> قبل تسجيل الخدمة حتى يظهر الخطأ في سجل الإقلاع.
    /// </summary>
    public static void Validate(SecurityOptions security)
    {
        ArgumentNullException.ThrowIfNull(security);

        if (security.HstsMaxAgeDays <= 0)
        {
            throw new InvalidOperationException(
                $"Security:HstsMaxAgeDays must be a positive number of days (got {security.HstsMaxAgeDays}).");
        }

        if (security.HstsPreload && !security.HstsEnabled)
        {
            throw new InvalidOperationException(
                "Security:HstsPreload requires Security:HstsEnabled=true; otherwise the header is never sent.");
        }

        if (security.HstsPreload && security.HstsMaxAgeDays < PreloadMinimumMaxAgeDays)
        {
            throw new InvalidOperationException(
                "Security:HstsPreload requires Security:HstsMaxAgeDays >= "
                + $"{PreloadMinimumMaxAgeDays} days (got {security.HstsMaxAgeDays}): browsers ignore "
                + "preload below one year.");
        }
    }

    /// <summary>
    /// ينسخ رايات <see cref="SecurityOptions"/> إلى <see cref="HstsOptions"/> ويرفض القيم
    /// غير الصالحة عند الإقلاع برسالة صريحة بدل ترويسة صامتة لا يعمل بها المتصفح.
    /// </summary>
    public static void Configure(HstsOptions hsts, SecurityOptions security)
    {
        ArgumentNullException.ThrowIfNull(hsts);
        Validate(security);

        hsts.MaxAge = TimeSpan.FromDays(security.HstsMaxAgeDays);
        // preload بلا includeSubDomains لا يُقبل في قائمة المتصفحات، فيُثبَّت معه دائمًا.
        hsts.IncludeSubDomains = true;
        hsts.Preload = security.HstsPreload;
    }
}
