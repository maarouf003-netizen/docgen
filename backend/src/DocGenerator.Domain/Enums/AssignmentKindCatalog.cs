namespace DocGenerator.Domain.Enums;

/// <summary>
/// المصدر الوحيد لأنواع سجل التعاقب على الملف (DocumentAssignment).
/// </summary>
public static class AssignmentKindCatalog
{
    /// <summary>منشئ الملف: أول سجل يُنشأ مع إنشاء الملف باسم منشئه.</summary>
    public const string Create = "create";

    /// <summary>إحالة الملف إلى محامٍ آخر (نقل/تحويل ملكية الملف).</summary>
    public const string Transfer = "transfer";
}
