namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// نتيجة بدء مفتاح عدم تكرار (RF-011): إما حجز جديد (`Ticket` بلا رد)، أو رد
/// مخزن يُعاد، أو عملية قيد المعالجة (يُعاد الانتظار والمحاولة).
/// </summary>
/// <param name="Ticket">الحجز المحتفظ به لإتمامه/تحريره لاحقًا — null عند الرد المخزن أو الانشغال.</param>
/// <param name="ReplayBody">لقطة الاستجابة المخزنة — تُعاد حرفيًا عند التكرار.</param>
/// <param name="Busy">صحيح عندما يوجد حجز قيد المعالجة بنفس البصمة.</param>
public sealed record IdempotencyBegin(
    IdempotencyTicket? Ticket,
    string? ReplayBody,
    bool Busy);

/// <summary>حجز مفتاح محتفظ به — يُتمَّم بالنتيجة أو يُحرَّر عند الفشل.</summary>
public sealed record IdempotencyTicket(string Key, string Operation, int UserId);

/// <summary>
/// مخزن مفاتيح عدم التكرار (RF-011): حجز/إتمام/تحرير + كنس انتهازي للمنتهية.
/// الحجز التزام مستقل خارج معاملة العملية (فلا تعشيش معاملات).
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// بدء المفتاح: مخزن مكتمل وطازج ← ردّه؛ قيد المعالجة بنفس البصمة ← انشغال؛
    /// بصمة مختلفة أو منتهٍ/غائب ← حجز جديد (يُستبدَل المنتهي). السباق المتزامن
    /// على الحجز يُحسَم بالقيد الفريد (الخاسر يقرأ الفائز: رد أو انشغال).
    /// </summary>
    Task<IdempotencyBegin> BeginAsync(
        string key, string operation, int userId, string fingerprint, DateTime expiresAt,
        CancellationToken ct = default);

    /// <summary>إتمام الحجز بلقطة الاستجابة (تُعاد حرفيًا عند التكرار).</summary>
    Task CompleteAsync(IdempotencyTicket ticket, string responseBody, CancellationToken ct = default);

    /// <summary>تحرير الحجز عند فشل العملية (لتُعاد المحاولة طازجة) — متسامح.</summary>
    Task ReleaseAsync(IdempotencyTicket ticket, CancellationToken ct = default);
}
