using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IForumRetentionService
{
    /// <summary>
    /// تمريرة تنظيف واحدة قابلة للاختبار المباشر: حذف صلب للرسائل الأقدم من
    /// `UtcNow − RetentionMonths` عدا المثبّتة (مع إيصالاتها) — تُعيد عدد المحذوف.
    /// `RetentionMonths = 0` = معطّل (يُعيد 0 بلا حذف). التسجيل على المضيف لا هنا
    /// (طبقة التطبيق بلا تبعية تسجيل — العرف القائم).
    /// </summary>
    Task<int> ExecuteOnceAsync(CancellationToken ct = default);
}

/// <summary>
/// احتفاظ المنتدى: أول خدمة دورية في المشروع — منطق خالص هنا (يُختبر بساعة
/// مزيفة)، والمضيف (`BackgroundService`) رفيع للجدولة والتسجيل فقط. الحذف
/// `idempotent` فلا مانع من تشغيله على أكثر من نسخة.
/// </summary>
public sealed class ForumRetentionService : IForumRetentionService
{
    private readonly IForumRepository _forum;
    private readonly ForumOptions _options;
    private readonly TimeProvider _clock;

    public ForumRetentionService(
        IForumRepository forum,
        IOptions<ForumOptions> options,
        TimeProvider clock)
    {
        _forum = forum;
        _options = options.Value;
        _clock = clock;
    }

    public Task<int> ExecuteOnceAsync(CancellationToken ct = default)
    {
        var months = _options.RetentionMonths;
        if (months <= 0)
            return Task.FromResult(0);

        // حدّ الأقدمية من ساعة النظام المحقونة — لا `DateTime.UtcNow` مباشرًا.
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddMonths(-months);
        return _forum.DeleteOlderThanAsync(cutoff, ct);
    }
}
