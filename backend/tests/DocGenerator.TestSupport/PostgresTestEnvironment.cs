namespace DocGenerator.Testing;

/// <summary>
/// بوابة `PostgreSQL` الحقيقي للاختبارات (‏`ADR-0009`): تُقرأ السلسلة من
/// <c>DOCGEN_TEST_POSTGRES</c>، وفي <c>CI</c> تُفرض <c>DOCGEN_REQUIRE_POSTGRES=1</c>
/// كي يكون غياب القاعدة فشلًا صريحًا لا تخطيًا صامتًا.
/// </summary>
public static class PostgresTestEnvironment
{
    public const string ConnectionStringVariable = "DOCGEN_TEST_POSTGRES";
    public const string RequireVariable = "DOCGEN_REQUIRE_POSTGRES";

    /// <summary>هل يوجد خادم اختبار حيّ مُعرَّف في البيئة؟</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringVariable));

    /// <summary>هل يجب فشل الاختبار عند غياب الخادم (وضع <c>CI</c>)؟</summary>
    public static bool IsRequired =>
        string.Equals(
            Environment.GetEnvironmentVariable(RequireVariable),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// يُرجع سلسلة الاتصال إن وُجدت، وإلا <c>null</c> (تخطٍّ صريح). ومع
    /// <c>DOCGEN_REQUIRE_POSTGRES=1</c> يرمي استثناءً واضحًا بدل العودة صامتًا.
    /// </summary>
    public static string? RequireOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        if (IsRequired)
        {
            throw new InvalidOperationException(
                $"DOCGEN_REQUIRE_POSTGRES=1 يتطلب {ConnectionStringVariable} لقاعدة اختبار حيّة — "
                + "لا تُشغَّل هذه المجموعة بلا Postgres حقيقي في CI.");
        }

        return null;
    }
}
