namespace DocGenerator.Application.Common;

/// <summary>
/// أنواع المراجع التي يعتمد عليها التنبيه النظامي — تُفحص قبل الإدراج وقت المعالجة
/// (الخطة §8.1: «تُفحص المراجع قبل الإدراج»).
/// </summary>
public enum HeadAlertReferenceKind
{
    /// <summary>الفرع (إلزامي دائمًا).</summary>
    Branch = 0,

    /// <summary>الملف المرتبط.</summary>
    Document = 1,

    /// <summary>الإنابة المرتبطة.</summary>
    Delegation = 2,

    /// <summary>الاستئناف المرتبط.</summary>
    Appeal = 3,

    /// <summary>كتاب المطالعة المرتبط.</summary>
    ReviewLetter = 4,

    /// <summary>الجهة العامة المرتبطة.</summary>
    PublicEntity = 5,

    /// <summary>دائرة التنفيذ المرتبطة.</summary>
    ExecutionCircuit = 6,
}
