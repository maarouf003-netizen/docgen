namespace DocGenerator.Application.Common;

/// <summary>
/// تطبيع صفحة/حجم صفحة لاستعلامات القوائم الخادمية: سقف صارم للصفحة وللحجم معًا،
/// فلا يلتفّ حساب الإزاحة <c>(page-1)*perPage</c> في <c>int</c> (‏`page=2147483647`
/// كان يعطي إزاحة سالبة ⇒ خطأ <c>PostgreSQL</c> ⇒ 500، أو يلتف إلى 0 بصمت).
/// </summary>
public static class PageRequest
{
    /// <summary>أقصى عدد صفوف في الصفحة (نفس عقد الواجهة والقوائم).</summary>
    public const int MaxPerPage = 100;

    /// <summary>حجم الصفحة الافتراضي عند غياب/صفر القيمة.</summary>
    public const int DefaultPerPage = 20;

    /// <summary>
    /// سقف رقم الصفحة: مع <see cref="MaxPerPage"/> يبقى أكبر إزاحة ممكنة
    /// <c>(1_000_000-1)*100 = 99_999_900</c> داخل <see cref="int"/> بأمان.
    /// </summary>
    public const int MaxPage = 1_000_000;

    /// <summary>يطبّع الصفحة والحجم إلى النطاق الآمن (لا يرمي على القيم الورادة).</summary>
    public static (int Page, int PerPage) Normalize(int page, int perPage)
        => (
            Math.Clamp(page, 1, MaxPage),
            Math.Clamp(perPage <= 0 ? DefaultPerPage : perPage, 1, MaxPerPage));
}
