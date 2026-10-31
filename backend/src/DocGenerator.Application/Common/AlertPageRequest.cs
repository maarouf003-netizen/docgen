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
    /// تتجاوز <see cref="int.MaxValue"/>. أي قيمة خارج ذلك تُرفض برسالة صريحة
    /// (لا سقف صامت ولا تسريب صفحة ضخمة، ولا التفاف حسابي في <see cref="Skip"/>).
    /// </summary>
    public void Validate()
    {
        if (Page < 1)
            throw new ArgumentOutOfRangeException(nameof(Page), "رقم الصفحة يبدأ من 1");
        if (PageSize < 1 || PageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(
                nameof(PageSize),
                $"حجم الصفحة يجب أن يكون بين 1 و{MaxPageSize}");

        // الحساب في `long` قبل المقارنة: ضرب `int` هنا كان يلتفّ (مثلًا
        // (int.MaxValue-1)*3 يصبح موجبًا خاطئًا) فيمرّر صفحة غير المطلوبة بصمت.
        if ((long)(Page - 1) * PageSize > int.MaxValue)
            throw new ArgumentOutOfRangeException(
                nameof(Page),
                "رقم الصفحة كبير جدًا — الإزاحة الناتجة تتجاوز الحد الأقصى المسموح");
    }

    /// <summary>الإزاحة داخل الاستعلام — حساب موسّع ثم تحويل آمن بعد <see cref="Validate"/>.</summary>
    public int Skip => (int)((long)(Page - 1) * PageSize);
}
