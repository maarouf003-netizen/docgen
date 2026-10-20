using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Common.Interfaces;

/// <summary>
/// تسجيل رسائل التنبيه النظامي في صندوق الصادر (Outbox) — عقد المنتجين (الخطة §6.4).
///
/// كل دالة تسجّل **داخل معاملة العمل نفسها** (تُرفض بلا معاملة نشطة)، ولا تحفظ ولا
/// تبدأ/تنهي معاملة، ولا تبتلع فشل التسجيل: فشل التسجيل يُرجع معاملة العمل كاملة.
/// </summary>
public interface IHeadAlertOutbox
{
    /// <summary>
    /// يسجّل إنشاء/تحديث تنبيه نظامي (<c>upsert</c>) مع خطة استبدال اختيارية.
    /// </summary>
    /// <param name="request">الطلب الداخلي — يحمل <c>Kind</c> و<code>Audience</code> و<code>CreatedById</c> الإلزامي.</param>
    /// <param name="supersedePlan">خطة أرشفة التنبيهات القديمة المقابلة (اختيارية).</param>
    /// <param name="occurredAt">زمن وقوع الحدث (UTC) — لا وقت المعالجة.</param>
    Task EnqueueSystemAsync(
        CreateSystemHeadAlertRequest request,
        HeadAlertSupersedePlan? supersedePlan,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// يسجّل إعادة تسمية دائرة تنفيذ — المعالج يحدّث التنبيهات النشطة المرتبطة بها
    /// (بلا إنشاء تنبيه جديد).
    /// </summary>
    Task EnqueueCircuitRenamedAsync(
        int circuitId,
        string message,
        DateTime occurredAt,
        CancellationToken ct = default);
}

/// <summary>
/// مخزن صندوق الصادر (الخطة §8.2) — منفصل عن <c>IHeadAlertRepository</c>.
///
/// التسجيل يعمل على **نفس <c>DbContext</c> المسجّل في النطاق** الذي تعمل عليه معاملة
/// العمل — إنشاء سياق جديد في التسجيل يُلغي الذرّية. المعالجة (الالتقاط/التحديث) تفتح
/// معاملتها عبر <see cref="ExecuteInTransactionAsync"/> (عضو صريح في المخزن)، لا عبر
/// <c>ITransactionRunner</c> التجاري، فلا تتداخل معاملة عمل.
/// </summary>
public interface IHeadAlertOutboxStore
{
    /// <summary>يسجّل رسالة في الصندوق (بلا حفظ — يحفظه المُنتِج ضمن معاملته).</summary>
    void Enqueue(HeadAlertOutboxMessage message);

    /// <summary>
    /// يلتقط صفًا واحدًا بالترتيب (<c>Id ASC</c>) من <c>pending</c> أو <c>processing</c>
    /// المنتهي الـlease، ويثبّت <c>processing</c> و<code>Attempts++</code> و<code>LockedUntil</code>
    /// و<code>LockedBy</code> في معاملة قصيرة ثم يلتزم.
    /// </summary>
    Task<HeadAlertOutboxMessage?> ClaimNextAsync(
        string workerId,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    /// <summary>يضع الرسالة <c>processed</c> ويضبط <c>ProcessedAt</c> (ضمن معاملة المعالجة).</summary>
    Task MarkProcessedAsync(long id, CancellationToken ct = default);

    /// <summary>يضع الرسالة <c>skipped</c> ويضبط سببًا قصيرًا في <c>LastError</c>.</summary>
    Task MarkSkippedAsync(long id, string reason, CancellationToken ct = default);

    /// <summary>يعيد الرسالة إلى <c>pending</c> بموعد لاحق وسبب مختصر (بلا حدّ المحاولات).</summary>
    Task MarkRetryAsync(long id, DateTime nextAttemptAt, string? error, CancellationToken ct = default);

    /// <summary>يضع الرسالة <c>failed</c> مع سبب مختصر — تبقى للمراجعة اليدوية.</summary>
    Task MarkFailedAsync(long id, string error, CancellationToken ct = default);

    /// <summary>
    /// يعيد توجيه <c>TargetUserId</c> من السلف إلى الخلف في صفوف <c>pending</c> فقط
    /// (لا <c>processing</c>/<c>processed</c>/<c>failed</c>).
    /// </summary>
    Task<int> UpdateTargetUserForSuccessionAsync(int predecessorId, int successorId, CancellationToken ct = default);

    /// <summary>ينظّف الرسائل المنتهية: <c>processed</c> بعد 7 أيام و<code>skipped</code> بعد 30.</summary>
    Task<int> CleanupAsync(DateTime now, int processedRetentionDays, int skippedRetentionDays, CancellationToken ct = default);

    /// <summary>
    /// يفتح معاملة على <c>DbContext</c> النطاق نفسه (معاملة المعالجة) — لا يستدعي
    /// <c>ITransactionRunner</c> التجاري فلا يتداخل مع معاملة عمل.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
}
