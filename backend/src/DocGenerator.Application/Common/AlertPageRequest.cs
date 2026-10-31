namespace DocGenerator.Application.Common;

/// <summary>فلتر الأرشفة في قوائم التنبيهات (الخطة §8.1/§9.1).</summary>
public enum AlertArchiveFilter
{
    /// <summary>الصفوف النشطة فقط (الافتراضي) — المؤرشف مستبعد من القوائم والعدادات.</summary>
    Active = 0,

    /// <summary>النشطة والمؤرشفة معًا.</summary>
    All = 1,

    /// <summary>المؤرشفة فقط.</summary>
    OnlyArchived = 2
}

/// <summary>
/// طلب صفحات خادمية لقوائم التنبيهات (الخطة §9.1/§13): سقف صارم بلا استعلام بلا حد.
/// </summary>
public sealed record AlertPageRequest
{
    /// <summary>الحد الأقصى لعدد الصفوف في الصفحة.</summary>
    public const int MaxPageSize = 100;

    /// <summary>العدد الافتراضي للصفوف في الصفحة.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>رقم الصفحة (يبدأ من 1).</summary>
    public int Page { get; init; } = 1;

    /// <summary>عدد الصفوف في الصفحة (1..100).</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// يتحقق من صحة القيم: الصفحة ≥ 1، وحجم الصفحة بين 1 و100، والإزاحة الناتجة لا
    /// تتجاوز <see cref="int.MaxValue"/>. أي قيمة خارج ذلك تُرفض بـ<see cref="AlertPagingException"/>
    /// (رسالة عربية نظيفة تُترجم <c>400</c>، بلا سقف صامت ولا التفاف حسابي).
    /// </summary>
    public void Validate()
    {
        if (Page < 1)
            throw new AlertPagingException("رقم الصفحة يبدأ من 1");
        if (PageSize < 1 || PageSize > MaxPageSize)
            throw new AlertPagingException($"حجم الصفحة يجب أن يكون بين 1 و{MaxPageSize}");

        // الطرح في `long` قبل الضرب: `Page - 1` في `int` كان يلتفّ عند أدنى قيمة
        // (أمان بالترتيب لا بالأنواع)، والضرب في `int` كان يمرّر إزاحة موجبة خاطئة
        // (مثلًا (int.MaxValue-1)*3) بلا رفض.
        if (((long)Page - 1) * PageSize > int.MaxValue)
            throw new AlertPagingException(
                "رقم الصفحة كبير جدًا — الإزاحة الناتجة تتجاوز الحد الأقصى المسموح");
    }

    /// <summary>الإزاحة داخل الاستعلام — حساب موسّع ثم تحويل آمن بعد <see cref="Validate"/>.</summary>
    public int Skip => (int)(((long)Page - 1) * PageSize);
}
