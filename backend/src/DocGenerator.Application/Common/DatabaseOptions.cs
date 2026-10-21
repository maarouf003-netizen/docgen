namespace DocGenerator.Application.Common;

/// <summary>
/// إعدادات قاعدة البيانات (القسم <c>Database</c>) — الخطة §16.1.
/// </summary>
public sealed class DatabaseOptions
{
    /// <summary>اسم القسم في الإعدادات.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// هل يطبّق التطبيق الهجرات تلقائيًا عند الإقلاع؟
    /// <c>true</c> في التطوير والاختبار فقط؛ و<c>false</c> في الإنتاج، حيث تُطبَّق
    /// الهجرات صراحةً قبل تشغيل النسخة (لا يُعتمد على الترحيل التلقائي في الإنتاج).
    /// </summary>
    public bool AutoMigrate { get; set; } = true;
}
