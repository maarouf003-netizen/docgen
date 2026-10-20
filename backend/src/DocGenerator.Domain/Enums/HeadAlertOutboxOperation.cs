namespace DocGenerator.Domain.Enums;

/// <summary>
/// نوع operation رسالة صندوق الصادر (Outbox) — يُخزَّن كنص صغير (lowercase) لا كعدد رقمي،
/// وتقيّده <c>CK_HeadAlertOutbox_Operation</c> في القاعدة.
/// </summary>
public enum HeadAlertOutboxOperation
{
    /// <summary>إنشاء/تحديث تنبيه نظامي (upsert) — يستلزم <c>Kind</c> غير فارغ.</summary>
    Upsert = 0,

    /// <summary>إعادة تسمية دائرة تنفيذ — تحديث التنبيهات النشطة المرتبطة بها (بلا <c>Kind</c>).</summary>
    CircuitRenamed = 1
}
