namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// يهيّئ قاعدة البيانات عند إقلاع التطبيق: يطبّق المهاجرات، ثم يبذر الفروع والمستخدمين
/// (بذر كامل في بيئة التطوير، أو إنشاء مدير أول فقط في الإنتاج بكلمة تُحقن من الإعدادات).
/// تجريد يمنع طبقة العرض من الاعتماد المباشر على تنفيذات البنية التحتية.
/// </summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(bool development, string? bootstrapAdminPassword, CancellationToken ct = default);
}
