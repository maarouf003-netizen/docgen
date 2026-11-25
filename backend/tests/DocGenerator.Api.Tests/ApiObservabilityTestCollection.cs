namespace DocGenerator.Api.Tests;

/// <summary>
/// `P5-01`: اختبارات الرصد لا تعمل بالتوازي مع بقية المجموعات — `ActivityListener`
/// عالمي، ونريد أن يكون «بلا نشاط/بلا مستمع» حقيقيًا لا مشروطًا بجدولة الاختبارات.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiObservabilityTestCollection
{
    public const string Name = "api-observability";
}
