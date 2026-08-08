namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// يحوّل سلسلة اتصال Postgres بصيغة URI (postgres:// أو postgresql:// الشائعة في Render وHeroku
/// وNeon) إلى الصيغة الكلامية التي يتطلبها Npgsql (Host=...;Port=...;Database=...;Username=...;Password=...).
/// السلاسل الكلامية تُمرَّر كما هي دون أي تعديل، والقيم الفارغة تبقى فارغة.
/// </summary>
public static class PostgresConnectionString
{
    private static readonly Dictionary<string, string> QueryParamAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sslmode"] = "SSL Mode",
        ["connect_timeout"] = "Timeout",
        ["application_name"] = "Application Name",
        ["options"] = "Options",
        ["search_path"] = "Search Path",
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);

        var parts = new List<string>
        {
            $"Host={Quote(uri.Host)}",
            $"Port={(uri.Port > 0 ? uri.Port : 5432)}",
            $"Database={Quote(Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')))}",
        };

        var userInfo = uri.UserInfo;
        if (userInfo.Length > 0)
        {
            var separator = userInfo.IndexOf(':');
            var username = separator >= 0 ? userInfo[..separator] : userInfo;
            parts.Add($"Username={Quote(Uri.UnescapeDataString(username))}");
            if (separator >= 0 && separator < userInfo.Length - 1)
                parts.Add($"Password={Quote(Uri.UnescapeDataString(userInfo[(separator + 1)..]))}");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                var rawValue = kv.Length == 2 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
                if (QueryParamAliases.TryGetValue(kv[0], out var canonical))
                    parts.Add($"{canonical}={Quote(rawValue)}");
            }
        }

        return string.Join(";", parts) + ";";
    }

    private static string Quote(string value)
    {
        if (value.Length == 0 || value.IndexOfAny([';', '=', '"']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
