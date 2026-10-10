namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات منتدى المحامين (قسم `Forum`): مدة الاحتفاظ بالأشهر للحذف الصلب
/// التلقائي — `0` = معطّل، والسالب يُرفض عند بدء التشغيل (يفشل الإقلاع مبكرًا).
/// </summary>
public sealed class ForumOptions
{
    public int RetentionMonths { get; set; } = 6;
}
