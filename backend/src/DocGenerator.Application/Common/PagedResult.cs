namespace DocGenerator.Application.Common;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PerPage { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => PerPage <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PerPage);
    /// <summary>مؤشر الصفحة التالية للقوائم الداعمة (keyset) — `null` في آخر صفحة أو للقوائم الإدارية الصغيرة.</summary>
    public string? NextCursor { get; set; }
}
