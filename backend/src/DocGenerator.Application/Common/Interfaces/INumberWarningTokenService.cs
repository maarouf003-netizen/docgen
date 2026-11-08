namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// رمز تحذير قصير العمر يربط تأكيد إعادة استخدام الرقم بالمستخدم والملف وبصمة
/// الطلب وصفوف التعارض نفسها — بدل <c>confirmReuse</c> المنطقي الأعمى (R7).
/// </summary>
public interface INumberWarningTokenService
{
    string Issue(int userId, int documentId, string fingerprint, IEnumerable<int> conflictingRowIds);

    bool Validate(string? token, int userId, int documentId, string fingerprint, IEnumerable<int> conflictingRowIds);
}
