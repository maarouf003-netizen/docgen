namespace DocGenerator.Application.Tests;

/// <summary>
/// `P5-01`: `ActivityListener` عالمي على مستوى العملية، فتُعزل اختبارات الرصد في
/// مجموعة لا تعمل بالتوازي مع غيرها — وإلا رأى اختبار «بلا مستمع» مستمعًا من اختبار آخر.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ObservabilityTestCollection
{
    public const string Name = "application-observability";
}
