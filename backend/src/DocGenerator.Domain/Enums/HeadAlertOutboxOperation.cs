namespace DocGenerator.Domain.Enums;

/// <summary>
/// نوع operation رسالة صندوق الصادر (Outbox) — يُخزَّن كنص صغير (lowercase) لا كعدد رقمي،
/// وتقيّده <c>CK_HeadAlertOutbox_Operation</c> في القاعدة.
///
/// **مهم:** اسم عضو التعداد هو نفسه القيمة المخزَّنة (بشرطة سفلية) — فـ<code>Enum.ToString()</code>
/// يُسقط الشرطة السفلية، و<code>circuit_renamed</code> لا <code>circuitrenamed</code>.
/// </summary>
public enum HeadAlertOutboxOperation
{
    /// <summary>إنشاء/تحديث تنبيه نظامي (upsert) — يستلزم <c>Kind</c> غير فارغ.</summary>
    Upsert = 0,

    /// <summary>إعادة تسمية دائرة تنفيذ — تحديث التنبيهات النشطة المرتبطة بها (بلا <c>Kind</c>).</summary>
    Circuit_Renamed = 1
}
