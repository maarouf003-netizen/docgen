namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// تصنيف أخطاء قاعدة البيانات دون تسرّب تفاصيل المزود إلى طبقة التطبيق (B3):
/// يميّز تعارض القيد الفريد (يُترجم إلى خطأ مجال 400) عن باقي الأعطال (تبقى 500).
/// يستقبل <see cref="Exception"/> العام عمدًا — لا مرجع لحزم المزودين هنا.
/// </summary>
public interface IDbExceptionClassifier
{
    /// <summary>هل هذا الاستثناء (أو سببه الداخلي) تعارض قيد فريد؟</summary>
    bool IsUniqueViolation(Exception ex);
}
