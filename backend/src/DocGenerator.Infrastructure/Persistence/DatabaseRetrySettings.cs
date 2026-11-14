namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// إعدادات إعادة المحاولة على مستوى القاعدة (P0-03a). تُضبط مرة واحدة عند الإقلاع
/// من <c>Database:RetryMaxCount</c> و<c>Database:RetryMaxDelaySeconds</c>.
/// الافتراضي <c>0</c> = معطّل (السلوك الحالي الآمن)؛ التفعيل يصبح آمنًا بعد
/// <see cref="TransactionAwareExecutionStrategyFactory"/> + نطاق جديد لكل محاولة.
/// </summary>
public static class DatabaseRetrySettings
{
    private static RetrySettings _current = new(0, 30);

    public static RetrySettings Current => Volatile.Read(ref _current);

    public static void Configure(int maxRetryCount, int maxRetryDelaySeconds)
    {
        if (maxRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), "RetryMaxCount لا يكون سالبًا.");
        if (maxRetryDelaySeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRetryDelaySeconds), "RetryMaxDelaySeconds يجب أن يكون موجبًا.");

        Volatile.Write(ref _current, new RetrySettings(maxRetryCount, maxRetryDelaySeconds));
    }

    /// <summary>لقطة إعدادات غير قابلة للتغيير.</summary>
    public sealed record RetrySettings(int MaxRetryCount, int MaxRetryDelaySeconds);
}
