namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// بوابة اختبارات <c>PostgreSQL</c> الحيّة: تُشغَّل عند تعريف
/// <c>DOCGEN_TEST_POSTGRES</c>، وتُتخطّى محليًا عند غيابه.
///
/// <para>في <c>CI</c> تُضبط <c>DOCGEN_REQUIRE_POSTGRES=1</c> مع سلسلة الاتصال، فأي
/// غياب لها = <b>فشل صريح</b> لا نجاح صامت — لأن «خضرة» خطوة لم تنفّذ اختبارًا
/// واحدًا أسوأ من فشل ظاهر (الخطة §4.1 البند 14).</para>
/// </summary>
internal static class PostgresTestEnvironment
{
    public const string ConnectionStringVariable = "DOCGEN_TEST_POSTGRES";
    public const string RequireVariable = "DOCGEN_REQUIRE_POSTGRES";

    /// <summary>هل سلسلة قاعدة الاختبار معرّفة وغير فارغة؟</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    /// <summary>هل فُرض وجود Postgres (وضع <c>CI</c>) فلا يُسمح بالتخطي؟</summary>
    public static bool IsRequired =>
        string.Equals(
            Environment.GetEnvironmentVariable(RequireVariable),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// سلسلة اتصال قاعدة الاختبار، أو <c>null</c> للتخطّي المحلي. عند
    /// <c>DOCGEN_REQUIRE_POSTGRES=1</c> يرمي استثناءً صريحًا بدل التخطّي.
    /// </summary>
    public static string? RequireOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (IsRequired)
        {
            throw new InvalidOperationException(
                $"DOCGEN_REQUIRE_POSTGRES=1 لكن {ConnectionStringVariable} غير معرّف — "
                + "لا يُسمح بتخطّي اختبارات Postgres الحيّة في CI.");
        }

        return null;
    }
}
