using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// حارس الكتابة المشترك (المرحلة 4 — البيت المخطط للحراس ذات الاعتمادات):
/// متعاون نحيف محقون (`Scoped` حصرًا — `DbContext`/`UoW` مشتركة عبر النطاق)
/// يحمل `_documents` و`_dbErrors` و`_audit` و`_occurrences` و`_uow` و`_circuits` فقط.
/// تستهلكه النواة وخدمة الحالات (أي حاجة عكسية تُحل بنقل المساعد لا بنداء خدمة).
/// ثابت ملكية المعاملة: لا يفتح معاملة خاصة إطلاقًا (`With*Guard` تُلفّف عمل المستدعي،
/// والمساعدات تنضم لمعاملة المستدعي) — اللفّ في نقاط الدخول فقط.
/// </summary>
public interface IDocumentWriteGuard
{
    /// <summary>
    /// الفحص المبكر: النسخة المرسَلة تطابق المقروءة؛ وإلا `409` ودي.
    /// ثم يزيد العدّاد ويثبّت `OriginalValue` لتكشف `EF` السباق بين القراءة والحفظ.
    /// الغياب = توافق (قبول بلا فحص مبكر، لكن الزيادة والشبكة تنشطان).
    /// </summary>
    void EnsureConcurrency(Document doc, long? version);

    /// <summary>
    /// شبكة السباق: تعارض التزامن عند الحفظ المتزامن يُترجَم `409` وديًا بدل `500`.
    /// تُلفّ حول استدعاء `_tx.RunAsync` كاملًا في نقطة الدخول.
    /// </summary>
    Task<T> WithConcurrencyGuardAsync<T>(Func<Task<T>> action);

    /// <summary>
    /// شبكة السباق: تعارض القيد الفريد عند الحفظ المتزامن يُترجَم 409 وديًا بدل 500.
    /// تُلفّ حول استدعاء `_tx.RunAsync` كاملًا في نقطة الدخول.
    /// </summary>
    Task<T> WithNumberGuardAsync<T>(Func<Task<T>> action, string? number);

    /// <summary>نسخة الإجراءات بلا قيمة راجعة (تدوير الأرقام).</summary>
    Task WithNumberGuardAsync(Func<Task> action, string? number);

    Task EnsureNumberUniqueByCircuitAsync(
        int? excludeDocumentId,
        int? circuitId,
        string? courtNormFallback,
        string? number,
        string? type,
        string? year,
        CancellationToken ct);

    /// <summary>
    /// حل الدائرة للكتابة الجديدة: إلزامية + رفض المعطلة + اشتقاق الاسم.
    /// يُستدعى داخل المعاملة (ضد TOCTOU) — الفحص والاشتقاق معًا.
    /// </summary>
    Task<ExecutionCircuit?> ResolveCircuitForWriteAsync(
        DocumentUpsertRequest request,
        int? branchId,
        bool isExternalTarget,
        CancellationToken ct);

    /// <summary>
    /// وقوعّة تبديل الدائرة من التعديل العادي (M2): صف واحد يحمل محتوى «الوقوعتين»
    /// (قديم/جديد في FromCircuitName/ToCircuitName + الرقم الحالي) — تنضم لمعاملة المستدعي.
    /// </summary>
    Task AddCircuitSwitchOccurrencesAsync(
        Document doc, string? oldCircuitName, int? userId, CancellationToken ct);

    /// <summary>
    /// تسجيل تدقيق عمليات الملف مع تفاصيل تغيّرات الحقول: حين تُرصد فروقات
    /// تُسجَّل صفوفًا (حقل/قبل/بعد) مرتبطة بالإدخال، وإلا يبقى التسجيل نصيًا كالسابق.
    /// تنضم لمعاملة المستدعي.
    /// </summary>
    Task LogDocumentChangesAsync(
        Dictionary<string, string?> before,
        Document after,
        string? actorName,
        string actionType,
        string baseDetail,
        CancellationToken ct);
}
