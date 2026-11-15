namespace DocGenerator.Application.Common;

/// <summary>
/// مصدر العشوائية القابل للحقن (P1-02): كل توليد أرقام/رموز غير حتمي يمر عبر هذا العقد،
/// فيمكن للاختبارات استبداله بمولّد ثابت وتصبح النتائج قابلة لإعادة الإنتاج.
/// </summary>
public interface IEntropySource
{
    /// <summary>عدد صحيح في المجال <c>[minValue, maxValue)</c>.</summary>
    long NextInt64(long minValue, long maxValue);
}

/// <summary>
/// المصدر الافتراضي في التشغيل: <see cref="Random.Shared"/> الآمن للخيوط.
/// لا يُستخدم مباشرة في الخدمات، بل عبر <see cref="IEntropySource"/> فقط.
/// </summary>
public sealed class SystemEntropySource : IEntropySource
{
    public static SystemEntropySource Instance { get; } = new();

    private SystemEntropySource()
    {
    }

    public long NextInt64(long minValue, long maxValue) => Random.Shared.NextInt64(minValue, maxValue);
}
