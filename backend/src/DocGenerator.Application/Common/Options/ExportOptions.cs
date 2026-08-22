namespace DocGenerator.Application.Common;

/// <summary>
/// حدود التصدير إلى Excel. السقف يحمي الخادم من ذروات الذاكرة غير المحصورة
 /// (جلب كامل النتائج ثم بناء المصنف في الذاكرة) ويظل قابلاً للرفع من الإعدادات.
/// </summary>
public sealed class ExportOptions
{
    public int MaxRows { get; set; } = 10_000;
}
