using DocGenerator.Testing;
using Npgsql;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس انحدار لجمود التنظيف (‏`docs/audit/80-…` §3): كان <c>PostgresTestDb.Dispose</c>
/// ينفّذ <c>await</c> داخليًا، فتعود الاستمرارية إلى <c>SynchronizationContext</c> الحالي —
/// وهو محجوب بالخيط الذي ينتظر الإسقاط نفسه — فتتوقف جولة الاختبارات كلها بلا استثناء
/// (رُصد عند ~900 حالة في `Api`، وظهر في لقطات `dotnet-stack` عشرات الخيوط في
/// <c>PostgresHostDatabase.Dispose</c>/<c>ApiFactory.Dispose</c>).
///
/// <para>الاختبار يحاكي السياق المحجوب: سياق لا يضخّ رسائله. أي <c>await</c> يلتقطه
/// يجعل <c>Dispose</c> لا يكتمل، فيفشل الاختبار بمهلة صريحة بدل تعليق صامت.</para>
/// </summary>
public sealed class PostgresTestDbDisposeTests
{
    [PostgresFact]
    public void Dispose_CompletesWithoutPumpingSynchronizationContext()
    {
        var database = PostgresTestDb.Create();
        var databaseName = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            // السياق الحالي هو الذي تلتقطه الاستمراريات داخل Dispose.
            SynchronizationContext.SetSynchronizationContext(new NonPumpingSynchronizationContext());
            try
            {
                database.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        })
        {
            IsBackground = true,
            Name = "postgres-testdb-dispose",
        };

        thread.Start();

        Assert.True(
            thread.Join(TimeSpan.FromSeconds(30)),
            "Dispose لم يكتمل خلال 30s: استمرارية معلّقة على SynchronizationContext غير مضخوط "
            + "(انحدار جمود — استخدم الإسقاط المتزامن داخل Dispose).");
        Assert.Null(failure);

        // الإسقاط حدث فعلًا، لا مجرد خروج من الطريقة.
        Assert.False(DatabaseExists(databaseName), $"القاعدة {databaseName} ما زالت موجودة بعد Dispose.");
    }

    private static bool DatabaseExists(string databaseName)
    {
        var baseConnectionString = PostgresTestEnvironment.RequireOrSkip()!;
        var maintenance = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = "postgres" };
        using var connection = new NpgsqlConnection(maintenance.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "name";
        parameter.Value = databaseName;
        command.Parameters.Add(parameter);
        return command.ExecuteScalar() is not null;
    }

    /// <summary>سياق لا ينفّذ ما يُرسَل إليه — يحاكي خيط xUnit المحجوب بانتظار <c>Task</c>.</summary>
    private sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            // لا ضخّ عن قصد: الاستمرارية المعلّقة هنا هي الفشل المُختبَر.
        }
    }
}
