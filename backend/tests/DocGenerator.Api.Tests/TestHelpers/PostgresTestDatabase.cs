using Npgsql;

namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// قاعدة <c>PostgreSQL</c> معزولة لاختبار حيّ واحد: إنشاء باسم عشوائي ثم إسقاط.
///
/// <para><c>Pooling=false</c> مقصود: إسقاط القاعدة في النهاية يتطلب ألا تبقى
/// اتصالات خاملة مفتوحة، وبديله <c>NpgsqlConnection.ClearAllPools()</c> يمسّ
/// مجمّعات <b>كل</b> الاختبارات المتوازية — أثر عام غير مقبول.</para>
/// </summary>
internal static class PostgresTestDatabase
{
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

    public static async Task DropAsync(string baseConnectionString, string databaseName)
    {
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }
}
