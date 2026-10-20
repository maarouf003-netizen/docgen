using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مُنتِج رسائل صندوق الصادر (الخطة §6.4) — يسجّل داخل معاملة العمل نفسها.
///
/// الضمانات:
/// <list type="bullet">
/// <item>يرفض العمل بلا معاملة نشطة (<see cref="ITransactionContext.HasActiveTransaction"/>).</item>
/// <item>لا يبدأ/ينهي معاملة، ولا يستدعي <c>SaveChangesAsync</c> — يقع التسجيل
/// **قبل آخر** <c>SaveChanges</c> في معاملة العمل أو يتبعه حفظ نهائي صريح.</item>
/// <item>لا يبتلع فشل التسجيل: الفشل يُرجع معاملة العمل كاملة (حدث نظامي غير قابل
/// للتسجيل خطأ برمجي، ولا يجوز ترك العمل بلا تنبيه).</item>
/// </list>
/// </summary>
public sealed class HeadAlertOutbox : IHeadAlertOutbox
{
    private readonly IHeadAlertOutboxStore _store;
    private readonly ITransactionContext _transactions;

    public HeadAlertOutbox(IHeadAlertOutboxStore store, ITransactionContext transactions)
    {
        _store = store;
        _transactions = transactions;
    }

    public Task EnqueueSystemAsync(
        CreateSystemHeadAlertRequest request,
        HeadAlertSupersedePlan? supersedePlan,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        RequireActiveTransaction();

        if (!HeadAlertKindCatalog.IsWritable(request.Kind))
            throw new ArgumentException($"نوع التنبيه غير قابل للكتابة: {request.Kind}", nameof(request));
        if (!HeadAlertKindCatalog.AllowedAudiences(request.Kind).Contains(request.Audience))
            throw new ArgumentException(
                $"الجمهور {request.Audience} غير مسموح للنوع {request.Kind}", nameof(request));
        if (request.CreatedById <= 0)
            throw new ArgumentException("منشئ التنبيه النظامي إلزامي", nameof(request));

        var message = HeadAlertTextLimits.EnsureWithinSafety(request.Message);
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب", nameof(request));

        if (supersedePlan is not null)
            ValidateSupersedePlan(supersedePlan, request);

        var payload = new HeadAlertOutboxPayloadV1
        {
            Operation = HeadAlertOutboxOperation.Upsert.ToString().ToLowerInvariant(),
            OccurredAt = occurredAt,
            TargetUserId = DeriveTargetUserId(request),
            Request = request with { Message = message },
            SupersedePlan = supersedePlan is null
                ? null
                : new HeadAlertOutboxPayloadV1.HeadAlertSupersedePlanPayload
                {
                    DelegationId = supersedePlan.DelegationId,
                    AppealId = supersedePlan.AppealId,
                    ArchiveKinds = supersedePlan.ArchiveKinds.ToArray(),
                    Reason = supersedePlan.Reason,
                },
        };

        _store.Enqueue(new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Upsert,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = request.Kind,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(payload),
            PayloadVersion = HeadAlertOutboxPayloadSerializer.CurrentPayloadVersion,
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
            Attempts = 0,
            TargetUserId = payload.TargetUserId,
        });

        return Task.CompletedTask;
    }

    public Task EnqueueCircuitRenamedAsync(
        int circuitId,
        string message,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        RequireActiveTransaction();

        if (circuitId <= 0)
            throw new ArgumentException("معرّف الدائرة إلزامي", nameof(circuitId));

        var text = HeadAlertTextLimits.EnsureWithinSafety(message);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("نص إعادة التسمية مطلوب", nameof(message));

        var payload = new HeadAlertOutboxPayloadV1
        {
            Operation = HeadAlertOutboxOperation.Circuit_Renamed.ToString().ToLowerInvariant(),
            OccurredAt = occurredAt,
            CircuitId = circuitId,
            Message = text,
        };

        _store.Enqueue(new HeadAlertOutboxMessage
        {
            Operation = HeadAlertOutboxOperation.Circuit_Renamed,
            Status = HeadAlertOutboxStatus.Pending,
            Kind = null,
            Payload = HeadAlertOutboxPayloadSerializer.Serialize(payload),
            PayloadVersion = HeadAlertOutboxPayloadSerializer.CurrentPayloadVersion,
            OccurredAt = occurredAt,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow,
            Attempts = 0,
        });

        return Task.CompletedTask;
    }

    private void RequireActiveTransaction()
    {
        if (!_transactions.HasActiveTransaction)
            throw new InvalidOperationException(
                "تسجيل رسالة التنبيه يتطلب معاملة عمل نشطة — التسجيل خارج المعاملة يفقد الذرّية.");
    }

    /// <summary>
    /// يشتق المستخدم المستهدف من الجمهور الأحادي — يُستخدم كتجاوز صريح للمستلم عند
    /// المعالجة (ولإعادة توجيهه عند التعاقب). يبقى <c>null</c> للبث ولجمهور
    /// «منشئ الملف» (فالمستلم يُشتق من الملف وقت المعالجة).
    /// </summary>
    private static int? DeriveTargetUserId(CreateSystemHeadAlertRequest request)
        => request.Audience switch
        {
            HeadAlertAudience.SingleLawyer => request.RecipientUserId ?? request.TargetLawyerId,
            HeadAlertAudience.SingleHead => request.RecipientUserId,
            _ => null,
        };

    private static void ValidateSupersedePlan(
        HeadAlertSupersedePlan plan,
        CreateSystemHeadAlertRequest request)
    {
        if (plan.DelegationId is null && plan.AppealId is null)
            throw new ArgumentException("خطة الاستبدال تحدّد إنابة أو استئنافًا واحدًا على الأقل", nameof(plan));
        if (plan.ArchiveKinds is null || plan.ArchiveKinds.Count == 0)
            throw new ArgumentException("مجموعة أنواع الأرشفة إلزامية", nameof(plan));
        if (string.IsNullOrWhiteSpace(plan.Reason))
            throw new ArgumentException("سبب الأرشفة إلزامي", nameof(plan));

        if (plan.DelegationId is not null && plan.DelegationId != request.DelegationId)
            throw new ArgumentException("خطة الاستبدال لا تطابق ربط الإنابة في الطلب", nameof(plan));
        if (plan.AppealId is not null && plan.AppealId != request.AppealId)
            throw new ArgumentException("خطة الاستبدال لا تطابق ربط الاستئناف في الطلب", nameof(plan));
    }
}
