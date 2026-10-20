namespace DocGenerator.Domain.Enums;

/// <summary>
/// حالة رسالة صندوق الصادر (Outbox) — تُخزَّن كنص صغير (lowercase) لا كعدد رقمي،
/// لتطابق قيود <c>CK_HeadAlertOutbox_Status</c> في القاعدة والاستعلامات التشغيلية.
/// </summary>
public enum HeadAlertOutboxStatus
{
    /// <summary>مُسجَّلة ولم تُلتقط بعد.</summary>
    Pending = 0,

    /// <summary>التقطها مشغّل وتعمل على معالجتها (مع lease قابل للاسترداد).</summary>
    Processing = 1,

    /// <summary>عُولجت بنجاح (أو كانت <c>Unchanged</c>/<c>Stale</c> المعالَجة).</summary>
    Processed = 2,

    /// <summary>تُخطّت بقرار عمل (لا مستلمين، مرجع مفقود، حدث قديم) — بلا إعادة.</summary>
    Skipped = 3,

    /// <summary>فشلت المعالجة بعد استنفاد المحاولات (أو تعارض متكرر) — تبقى للمراجعة اليدوية.</summary>
    Failed = 4
}
