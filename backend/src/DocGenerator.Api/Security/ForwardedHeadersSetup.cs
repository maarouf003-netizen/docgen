using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace DocGenerator.Api.Security;

/// <summary>
/// إعداد <c>ForwardedHeaders</c> الموثوق: تُعالَج <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>
/// فقط إذا جاء الطلب من وكيل معروف صراحةً (<c>Security:KnownProxies</c>: عنوان <c>IP</c> أو نطاق
/// <c>CIDR</c>). بلا وكيل معلَن **يُعطَّل المعالجة كليًا**
/// (<c>ForwardedHeaders.None</c>) وتُصفَّر قوائم الثقة.
///
/// **لماذا التعطيل لا مجرد قائمة فارغة:** <c>ForwardedHeadersMiddleware</c> يعامل «لا وكيل
/// معروف ولا شبكة معروفة» كـ«ثق بكل المصادر» — فقائمة فارغة كانت ستفتح التزوير لكل عميل،
/// وهو أسوأ من افتراضيات الإطار (<c>::1</c> و<c>127.0.0.0/8</c>) التي كانت تثق بـ<c>loopback</c>
/// فقط. التعطيل الصريح هو التعبير الصحيح عن «مغلق افتراضيًا».
/// </summary>
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options, string[]? knownProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // طبقة وكيل واحدة: القيمة هي افتراض الإطار، مثبَّتة صراحةً لأن الاعتماد عليها ضمنيًا
        // يجعل أي تغيير مستقبلي في الإطار تغييرًا صامتًا في نموذج الثقة.
        options.ForwardLimit = 1;

        // تُصفَّر افتراضيات الإطار أولًا (`::1` و`127.0.0.0/8`): القائمة الموثوقة هي ما يُعلَن
        // هنا حصرًا — وإلا بقيت أي عملية محلية قادرة على تزوير `X-Forwarded-For` فتتجاوز مفتاح
        // خنق الدخول (`IP:user`) وتلوّث السجلات، ولو كان وكيل مصرَّحًا به.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();

        var trustedAny = false;

        foreach (var entry in knownProxies ?? [])
        {
            var item = entry.Trim();
            if (item.Length == 0)
                continue;

            if (item.Contains('/'))
            {
                var parts = item.Split('/');
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var networkAddress)
                    && int.TryParse(parts[1], out var prefix)
                    && prefix >= 0
                    && prefix <= networkAddress.GetAddressBytes().Length * 8)
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefix));
                    trustedAny = true;
                    continue;
                }
            }

            if (IPAddress.TryParse(item, out var ip))
            {
                options.KnownProxies.Add(ip);
                trustedAny = true;
            }
        }

        if (!trustedAny)
            options.ForwardedHeaders = ForwardedHeaders.None;
    }
}
