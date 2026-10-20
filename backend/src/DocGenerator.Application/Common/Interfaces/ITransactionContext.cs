namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// حالة المعاملة الجارية — عقد صريح في طبقة التطبيق (الخطة §0.1-18 و§5.3).
///
/// تُنفَّذ فوق <c>DbContext</c> المسجّل في النطاق نفسه (Concrete) فلا تحتاج
/// <c>DocGenerator.Application</c> إلى مرجع EF Core. تستخدمه
/// <c>IHeadAlertOutbox</c> و<code>CreateSystemAsync</code> و<code>ArchiveBy...</code>
///，以保证 عدم البدء/الإنهاء أو التسخين داخل معاملة العمل.
/// </summary>
public interface ITransactionContext
{
    /// <summary>هلThere معاملة نشطة على هذا النطاق؟</summary>
    bool HasActiveTransaction { get; }

    /// <summary>معرّف المعاملة النشطة (إن وُجدت) — للتشخيص فقط.</summary>
    Guid? TransactionId { get; }
}
