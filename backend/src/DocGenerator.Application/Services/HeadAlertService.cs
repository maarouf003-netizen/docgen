using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// سياق الفاعل في مسارات التنبيهات — يمرره المتحكم من التوكن، وتُبنى عليه قرارات
/// النطاق والصلاحيات في الخدمة وحدها (الخطة §9.3/§12، `D8`).
///
/// <para>كل مسند هنا يفوض إلى <see cref="RolePermissions"/> — لا مقارنة دور مباشرة —
/// كي يبقى الكتالوج مصدرًا واحدًا يستهلكه المتحكم والخدمة معًا (مراجعة PR-4 `ح‑4`).</para>
/// </summary>
public sealed record HeadAlertActor(int UserId, UserRole Role, int? BranchId)
{
    public bool IsLawyer => RolePermissions.IsLawyer(Role);
    public bool IsHeadOrSubHead => RolePermissions.IsHeadOrSubHead(Role);
    public bool IsManagerOrAdmin => RolePermissions.CanReadAllAlerts(Role);
}

/// <summary>
/// فلاتر قوائم التنبيهات (الخطة §9.1): الأرشفة، غير المقروء، وربط الإنابة.
/// الترقيم يحمله <see cref="AlertPageRequest"/> المستقل (page/pageSize بحدود صارمة)،
/// و<see cref="Cursor"/> اختياري: عند وجوده يثبّت موضع الصفحة (keyset) ويُلغي
/// <c>page</c> بلا كسر للطلبات القديمة.
/// </summary>
public sealed record HeadAlertListQuery
{
    public AlertArchiveFilter Archive { get; init; } = AlertArchiveFilter.Active;
    public bool UnreadOnly { get; init; }
    public int? DelegationId { get; init; }
    public AlertPageRequest Page { get; init; } = new();

    /// <summary>
    /// مؤشر آخر صف معروض **كما وصل سلكيًا** (<c>{ISO-8601-UTC}|{id}</c>)؛ <c>null</c>
    /// يعني الترقيم بالإزاحة (<c>page</c>). يُحلَّل في الخدمة بعد فحص الدور/الفرع
    /// ليبقى <c>403</c> قبل <c>400</c> (ترتيب القرار §9.4) — لا في المتحكم.
    /// </summary>
    public string? Cursor { get; init; }
}

public interface IHeadAlertService
{
    // ── المسار النظامي (الخطة §8.1) ──
    /// <summary>
    /// معالجة تنبيه نظامي — **معالج داخلي يعمل داخل معاملة مشغّل Outbox فقط**.
    ///
    /// ينشئ/يحدّث التنبيه بمفتاح منع التكرار المركزي، ويعيد ضبط القراءة عند تغيّر النص
    /// فقط، ولا يعدّل <c>CreatedAt</c> أبدًا. يرفض العمل بلا معاملة نشطة.
    /// </summary>
    Task<HeadAlertWriteResult> CreateSystemAsync(
        CreateSystemHeadAlertRequest request,
        DateTime occurredAt,
        HeadAlertSupersedePlan? supersedePlan,
        int? targetUserIdOverride,
        CancellationToken ct = default);

    /// <summary>
    /// معالجة إعادة تسمية دائرة — **معالج داخلي لمشغّل Outbox** (يتطلب معاملة نشطة).
    /// يحدّث التنبيهات النشطة <c>circuit_pending_documents</c> المرتبطة بالدائرة فقط،
    /// ولا ينشئ تنبيهًا جديدًا ولا يغيّر مفتاح منع التكرار.
    /// </summary>
    Task<int> ApplyCircuitRenamedAsync(
        int circuitId, string message, DateTime occurredAt, CancellationToken ct = default);

    /// <summary>
    /// أرشفة (لا حذف) تنبيهات الإنابة ضمن مجموعة أنواع صريحة. إن وُجدت معاملة نشطة
    /// تُنفَّذ بلا معاملة متداخلة؛ وإلا تفتح المعاملة بنفسها.
    /// </summary>
    Task<int> ArchiveByDelegationAsync(
        int delegationId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default);

    /// <summary>
    /// أرشفة (لا حذف) تنبيهات الاستئناف ضمن مجموعة أنواع صريحة — بقواعد
    /// <see cref="ArchiveByDelegationAsync"/>.
    /// </summary>
    Task<int> ArchiveByAppealAsync(
        int appealId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default);
}

/// <summary>
/// تنبيهات رئيس القسم — **سطح الكتابة النظامية وحده**: الإصدار/إعادة التسمية/الأرشفة
/// داخل معاملة المستدعي مع سجل التدقيق. قراءات التنبيهات الست انتقلت إلى
/// <c>UseCases/Alerts</c> (`P4-01cw`)، والحذف النهائي للفرع/المستخدم مقيد بالعلاقات.
/// </summary>
public sealed partial class HeadAlertService : IHeadAlertService, IHeadAlertManualWriteRules
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly ITransactionContext _transactions;

    public HeadAlertService(
        IHeadAlertRepository alerts,
        IDocumentRepository documents,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        ITransactionContext? transactions = null)
    {
        _alerts = alerts;
        _documents = documents;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        // الحارس يفشل مغلقًا بلا سياق: أي مسار يفرض معاملة نشطة يرمي بدل أن يعمل بلا ذرّية.
        // عند تمرير TransactionRunner (وهو ITransactionContext) نستعمل سياقه نفسه لتفادي
        // فتح معاملة متداخلة عند الأرشفة داخل معاملة العمل.
        _transactions = transactions ?? tx as ITransactionContext ?? NullTransactionContext.Instance;
    }

    // ── تنفيذ عقد مساعدات الإصدار اليدوي (P4-01bi) ───────────────────────

    int IHeadAlertManualWriteRules.RequireBranchForWrite(HeadAlertActor actor)
        => RequireBranchForWrite(actor);

    Task<List<User>> IHeadAlertManualWriteRules.ResolveRecipientsAsync(
        DocGenerator.Domain.Enums.HeadAlertTargetType targetType,
        CreateHeadAlertRequest request, int branchId, CancellationToken ct)
        => ResolveRecipientsAsync(targetType, request, branchId, ct);

    DocGenerator.Domain.Enums.HeadAlertTargetType IHeadAlertManualWriteRules.ParseManualTargetType(string? value)
        => ParseManualTargetType(value);

    string IHeadAlertManualWriteRules.ManualKindFor(DocGenerator.Domain.Enums.HeadAlertTargetType targetType)
        => ManualKindFor(targetType);

    HeadAlert IHeadAlertManualWriteRules.BuildManualAlert(
        CreateHeadAlertRequest request,
        HeadAlertActor actor,
        int branchId,
        DocGenerator.Domain.Enums.HeadAlertTargetType targetType,
        string kind,
        string message,
        IReadOnlyList<User> recipients,
        DateTime now)
        => new()
        {
            BranchId = branchId,
            CreatedById = actor.UserId,
            TargetType = targetType,
            Kind = kind,
            DedupeKey = null,
            DocumentId = targetType == DocGenerator.Domain.Enums.HeadAlertTargetType.Document ? request.DocumentId : null,
            TargetLawyerId = targetType == DocGenerator.Domain.Enums.HeadAlertTargetType.Lawyer ? request.TargetLawyerId : null,
            Message = message,
            CreatedAt = now,
            LastContentChangedAt = now,
            Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
        };

    Task IHeadAlertManualWriteRules.AddManualAlertAsync(HeadAlert alert, CancellationToken ct)
        => _alerts.AddAsync(alert, ct);
}
