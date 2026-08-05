namespace DocGenerator.Application.Common;

/// <summary>
/// يُرمى عند تعارض التفاؤلية أثناء النقل المتزامن: تغيّر المحامي المختص للملف
/// بين قراءته والتحديث، فيُعاد استجابة 409 Conflict للواجهة بدل التجاوز الصامت.
/// </summary>
public sealed class DocumentConflictException : Exception
{
    public DocumentConflictException(string message) : base(message) { }
}
