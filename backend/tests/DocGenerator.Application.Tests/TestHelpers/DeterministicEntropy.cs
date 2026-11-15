using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// مصدر عشوائية حتمي للاختبارات (P1-02): يعيد القيم بالترتيب ثم يثبّت على الأخيرة.
/// </summary>
public sealed class DeterministicEntropy(params long[] values) : IEntropySource
{
    private int _index;

    public long NextInt64(long minValue, long maxValue)
    {
        var value = values[Math.Min(_index, values.Length - 1)];
        _index++;
        if (value < minValue || value >= maxValue)
            throw new InvalidOperationException($"entropy value {value} outside [{minValue}, {maxValue})");
        return value;
    }
}
