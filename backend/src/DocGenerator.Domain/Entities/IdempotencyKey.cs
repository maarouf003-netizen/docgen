namespace DocGenerator.Domain.Entities;

/// <summary>
/// مفتاح عدم تكرار لعملية كتابة (RF-011 — INT-007): نفس المفتاح (ضمن العملية
/// والمستخدم) يُرجع النتيجة المخزنة بدل تنفيذ جديد. `ResponseBody` الفارغ يعني
/// «قيد المعالجة». تُكنَس المنتهية انتهازيًا عند كل حجز — بلا مهمة خلفية.
/// </summary>
public class IdempotencyKey
{
    public int Id { get; set; }

    /// <summary>المفتاح المرسَل من العميل (`uuid` — max 100).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>اسم العملية (مثل `documents.create`) — نطاق المفتاح.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>صاحب المفتاح — لا يُعاد استعمال مفتاح لغير صاحبه وعمليته.</summary>
    public int UserId { get; set; }

    /// <summary>بصمة الحمولة (`SHA256` hex) — الحمولة المختلفة نية جديدة تُنفَّذ طازجة.</summary>
    public string Fingerprint { get; set; } = string.Empty;

    /// <summary>لقطة الاستجابة المخزنة — `null` تعني قيد المعالجة.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>رمز حالة HTTP الأصلي (201/204/200…) — يُعاد كما هو عند التكرار.</summary>
    public int StatusCode { get; set; } = 200;

    /// <summary>نوع محتوى الاستجابة الأصلي (JSON عادةً) — يُعاد عند التكرار.</summary>
    public string? ContentType { get; set; }

    /// <summary>ترويسة `Location` للاستجابات 201 إن وُجدت — تُعاد مع التكرار.</summary>
    public string? ResponseLocation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>انتهاء المفتاح (72 ساعة افتراضيًا) — بعده يُعامَل كمفتاح جديد.</summary>
    public DateTime ExpiresAt { get; set; }
}
