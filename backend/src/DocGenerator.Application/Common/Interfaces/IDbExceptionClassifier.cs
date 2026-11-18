namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// تصنيف أخطاء قاعدة البيانات دون تسرّب تفاصيل المزود إلى طبقة التطبيق (B3):
/// يميّز تعارض القيد الفريد (يُترجم إلى تعارض بيانات <c>409</c>) عن باقي الأعطال (تبقى 500).
/// يستقبل <see cref="Exception"/> العام عمدًا — لا مرجع لحزم المزودين هنا.
/// </summary>
public interface IDbExceptionClassifier
{
    /// <summary>هل هذا الاستثناء (أو سببه الداخلي) تعارض قيد فريد؟</summary>
    bool IsUniqueViolation(Exception ex);

    /// <summary>هل هذا الاستثناء (أو سببه الداخلي) تعارض تزامن متفائل؟</summary>
    bool IsConcurrencyViolation(Exception ex);

    /// <summary>
    /// هل هذا الاستثناء (أو سببه الداخلي) خرق مفتاح أجنبي — أي مرجع محذوف/غير موجود؟
    /// يُستخدم لتحويل رسالة صندوق الصادر إلى <c>skipped: missing_reference</c> بدل
    /// إعادة لا نهائية (الخطة §8.1).
    /// </summary>
    bool IsForeignKeyViolation(Exception ex);

    /// <summary>
    /// هل هذا الاستثناء (أو سببه الداخلي) انتهاك قيد
    /// <c>CK_Documents_FileNumberRequiresCircuit</c> — رقم ملف بلا دائرة تنفيذ؟
    /// يُترجم إلى <c>400</c> لا <c>500</c> (القيد حارس أخير، والخدمة لا تعرف المزوّد).
    /// </summary>
    bool IsMissingDocumentCircuitViolation(Exception ex);
}
