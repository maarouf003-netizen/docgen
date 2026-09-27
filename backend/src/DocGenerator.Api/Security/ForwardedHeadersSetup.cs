using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace DocGenerator.Api.Security;

/// <summary>
/// إعداد <c>ForwardedHeaders</c> الموثوق: يعالج <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>
/// فقط إذا جاء الطلب من وكيل معروف صراحةً في الإعدادات (<c>Security:KnownProxies</c>، عنوان
/// <c>IP</c> أو نطاق <c>CIDR</c>). بلا أي وكيل معروف يبقى النظام مغلقًا ضد التزوير: أي ترويسة
/// <c>X-Forwarded-For</c> يرسلها عميل مباشر تُتجاهَل.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options, string[]? knownProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

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
                    && int.TryParse(parts[1], out var prefix))
                {
                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefix));
                    continue;
                }
            }
            if (IPAddress.TryParse(item, out var ip))
                options.KnownProxies.Add(ip);
        }
    }
}
