using Npgsql;

namespace DocGenerator.Testing;

/// <summary>
/// قاعدة <c>PostgreSQL</c> معزولة لاختبار حيّ واحد: إنشاء باسم عشوائي ثم إسقاط.
///
/// <para><c>Pooling=false</c> مقصود: إسقاط القاعدة في النهاية يتطلب ألا تبقى
/// اتصالات خاملة مفتوحة.</para>
/// </summary>
public static class PostgresTestDatabase
{
    /// <summary>ينشئ قاعدة فارغة باسم معطى (متزامن: مسار بناء قاعدة الاختبار).</summary>
    public static string Create(string baseConnectionString, string databaseName, bool pooling = true)
        => CreateCore(baseConnectionString, databaseName, $"CREATE DATABASE \"{databaseName}\"", pooling);

    /// <summary>يستنسخ قاعدة من قالب قائم (متزامن).</summary>
    public static string Clone(string baseConnectionString, string templateDatabase, string databaseName)
        => CreateCore(
            baseConnectionString,
            databaseName,
            $"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{templateDatabase}\"",
            pooling: true);

    /// <summary>يسقط قاعدة (متزامن: تنظيف نهاية التشغيل حيث لا مجمّع خيوط).</summary>
    public static void Drop(string baseConnectionString, string databaseName)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        // FORCE (PostgreSQL 13+): يقطع أي اتصال متبقٍّ بدل فشل التنظيف بعد المضيف.
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        // `DROP DATABASE` يفرض checkpoint فوريًا قبل الإسقاط؛ على أقراص بطيئة (Windows +
        // فحص فيروسات) يتجاوز المهلة الافتراضية 30s فيُلغى الأمر ويُفشل الاختبار رغم
        // نجاحه — القياس في docs/audit/72-p1-01ia-entities-portal-20261119.md.
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    private static string CreateCore(string baseConnectionString, string databaseName, string sql, bool pooling)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            Pooling = pooling,
            // حجم محدود وعمر خمول قصير: أعداد الاتصالات تبقى أقل بكثير من `max_connections`
            // مع عشرات قواعد الأصناف (72 قاعدة في Application وحدها). 2 يكفي لأن حالات
            // الصنف تسلسلية، ويترك هامشًا لتشغيل المشروعين معًا على قاعدة محلية.
            MaxPoolSize = pooling ? 2 : 1,
            ConnectionIdleLifetime = pooling ? 15 : 0,
        };
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            // الاستنساخ ينتظر قفل القالب/checkpoint؛ تحت توازي التشغيل الكامل يتجاوز
            // الافتراضي 30s فيتحول إلى فشل توقيت كاذب (كما في `Drop`).
            command.CommandTimeout = 120;
            command.ExecuteNonQuery();
        }

        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    public static async Task<string> CreateAsync(string baseConnectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Pooling = false };
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    /// <summary>يستنسخ قاعدة من قالب قائم (أسرع من الترحيل/الإنشاء لكل اختبار).</summary>
    public static async Task<string> CloneAsync(
        string baseConnectionString, string templateDatabase, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Pooling = false };
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using (var connection = new NpgsqlConnection(maintenance.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE DATABASE \"{databaseName}\" TEMPLATE \"{templateDatabase}\"";
            await command.ExecuteNonQueryAsync();
        }

        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    public static async Task DropAsync(string baseConnectionString, string databaseName)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        // FORCE (PostgreSQL 13+): يقطع أي اتصال متبقٍّ بدل فشل التنظيف بعد المضيف.
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        // `DROP DATABASE` يفرض checkpoint فوريًا قبل الإسقاط؛ على أقراص بطيئة (Windows +
        // فحص فيروسات) يتجاوز المهلة الافتراضية 30s فيُلغى الأمر ويُفشل الاختبار رغم
        // نجاحه — القياس في docs/audit/72-p1-01ia-entities-portal-20261119.md.
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync();
    }
}
