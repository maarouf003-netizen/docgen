namespace DocGenerator.Domain.Enums;

/// <summary>
/// قيم نوع الملف (عمود FileType) ذات المعنى المحدد في النظام.
/// أغلب قيم FileType نصوص حرة يدخلها المستخدم/الاستيراد، والوحيد المعرّف برمجيًا هو ملف الإنابة.
/// </summary>
public static class FileTypeCatalog
{
    /// <summary>الملف المناب الذي يُنشأ تلقائيًا عند اعتماد إنابة تنفيذية.</summary>
    public const string Delegation = "انابة";
}