namespace DocGenerator.Api.Tests.TestHelpers;

/// <summary>
/// اختبار حقيقي على <c>PostgreSQL</c>: يُتخطّى محليًا عند غياب
/// <c>DOCGEN_TEST_POSTGRES</c> وسلسلة فرض الوجود غير مفعّلة.
///
/// <para>لا تُرمى استثناءات هنا: الاستثناء أثناء الاكتشاف يجعل xUnit يُسقط الاختبار
/// من القائمة ويعيد نجاحًا صامتًا. في وضع <c>CI</c>
/// (<c>DOCGEN_REQUIRE_POSTGRES=1</c>) نترك الاختبار مُكتشَفًا كي يفشل عبر
/// <see cref="PostgresTestEnvironment.RequireOrSkip"/> في المصنع/الجسم برسالة صريحة.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresTestEnvironment.IsConfigured && !PostgresTestEnvironment.IsRequired)
            Skip = SkipReason;
    }

    internal const string SkipReason =
        "PostgreSQL غير متاح: عيّن DOCGEN_TEST_POSTGRES لقاعدة اختبار حيّة "
        + "(Docker أو PostgreSQL محمول).";
}

/// <summary>نظير <see cref="PostgresFactAttribute"/> لاختبارات <c>Theory</c>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (!PostgresTestEnvironment.IsConfigured && !PostgresTestEnvironment.IsRequired)
            Skip = PostgresFactAttribute.SkipReason;
    }
}
