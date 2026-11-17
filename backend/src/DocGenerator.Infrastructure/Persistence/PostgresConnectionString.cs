using DocGenerator.Application.Common;
using Npgsql;
using System.Data.Common;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// يحوّل سلسلة اتصال Postgres بصيغة URI (postgres:// أو postgresql:// الشائعة في المضيفين السحابيين
/// الذين يمنحون قواعد مُدارة) إلى الصيغة الكلامية التي يتطلبها Npgsql
/// (Host=...;Port=...;Database=...;Username=...;Password=...).
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

    /// <summary>
    /// يطبّق حدود مجمّع الاتصالات (<see cref="DatabasePoolOptions"/>) **كقيم افتراضية**
    /// على سلسلة اتصال بصيغة كلامية (بعد <see cref="Normalize"/>): المفاتيح المذكورة
    /// صراحةً في السلسلة لا تُلمس، لأن المشغّل قد يضبطها وفق حدود المضيف
    /// (<c>Supabase</c>/<c>Render</c>) أو يستخدم <c>Pooling=false</c> عمدًا.
    ///
    /// <para>
    /// الغرض: منع «السلوك الافتراضي الغامض» تحت الحمل (مهل وحجم مجمّع غير مذكورة)
    /// دون مصادرة قرار صريح. يرفض الإقلاع عند قيم خارج الحدود عبر
    /// <see cref="DatabasePoolOptions.Validate"/>.
    /// </para>
    /// </summary>
    public static string ApplyPoolDefaults(string connectionString, DatabasePoolOptions pool)
    {
        ArgumentNullException.ThrowIfNull(pool);

        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        pool.Validate();

        var specified = new DbConnectionStringBuilder { ConnectionString = connectionString }
            .Keys.Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!specified.Contains("Maximum Pool Size"))
            builder.MaxPoolSize = pool.MaxPoolSize;
        if (!specified.Contains("Minimum Pool Size"))
            builder.MinPoolSize = pool.MinPoolSize;
        if (!specified.Contains("Timeout"))
            builder.Timeout = pool.ConnectionTimeoutSeconds;
        if (!specified.Contains("Command Timeout"))
            builder.CommandTimeout = pool.CommandTimeoutSeconds;
        if (!specified.Contains("Connection Idle Lifetime"))
            builder.ConnectionIdleLifetime = pool.IdleLifetimeSeconds;

        return builder.ConnectionString;
    }
}
