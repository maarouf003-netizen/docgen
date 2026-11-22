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
    /// <summary>
    /// قائمة المستلَم حسب الدور (الخطة §9.3): المحامي استلامه الشخصي بلا قيد فرع،
    /// ورئيس القسم/الشعبة ما استلمه في فرعه، والمدير/المشرف إشراف كل الفروع
    /// (<c>IsRead=null</c> وبلا <c>unreadOnly</c>). ملخصات مرقّمة بلا نص كامل.
    /// </summary>
    Task<PagedResult<HeadAlertSummaryDto>> ListForActorAsync(
        HeadAlertActor actor, HeadAlertListQuery query, CancellationToken ct = default);

    /// <summary>
    /// ما أصدره رئيس القسم/الشعبة في فرعه (تبويب «أرسلتها»): قراءة فقط —
    /// بلا زر تعليم مقروء (المُرسِل ليس مستلمًا) وبلا أثر في العدّادات.
    /// <paramref name="cursor"/> غير الفارغ يثبّت موضع الصفحة (keyset)، ويُحلَّل
    /// بعد فحص الدور/الفرع (403 قبل 400).
    /// </summary>
    Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
        HeadAlertActor actor,
        AlertArchiveFilter archive,
        AlertPageRequest page,
        string? cursor,
        CancellationToken ct = default);

    /// <summary>
    /// تنبيهات إنابةٍ معيّنة («معلومات الملف المنيب»/«تشعبات الملف») — نشطة فقط،
    /// بعلامة قراءة بحسب المستخدم، وملخصات مرقّمة (قرار D1: page=1/pageSize=100 للشريط).
    /// </summary>
    Task<PagedResult<HeadAlertSummaryDto>> ListByDelegationAsync(
        int delegationId, HeadAlertActor actor, AlertPageRequest page, CancellationToken ct = default);

    /// <summary>
    /// تفاصيل كاملة (الخطة §9.2): المستلم بأولوية مطلقة بلا شرط فرع، ثم المنشئ،
    /// ثم المدير/المشرف؛ غير ذلك <c>null</c> (يُترجم إلى 404 بلا تسريب وجود).
    /// </summary>
    Task<HeadAlertDto?> GetDetailsAsync(int id, HeadAlertActor actor, CancellationToken ct = default);

    /// <summary>
    /// مستلمو التنبيه وحالة قراءتهم — للمنشئ أو المدير/المشرف فقط؛
    /// غير ذلك <c>null</c> (يُترجم إلى 404).
    /// </summary>
    Task<List<HeadAlertRecipientDto>?> GetRecipientsAsync(
        int id, HeadAlertActor actor, CancellationToken ct = default);

    /// <summary>
    /// عدّاد غير المقروء لصفوف الفاعل نفسه (`D6`/`D7`): المحامي بلا قيد فرع،
    /// ورئيس/شعبة داخل فرعه. المدير/المشرف يُرفضون <c>403</c>، ورئيس بلا فرع كذلك
    /// (`D11`)، وقيمة نطاق غير معروفة <c>400</c>. (حُذف نطاق <c>received</c> في
    /// <c>PR-6a</c> فلم يبقَ إلا <see cref="AlertUnreadScope.User"/>.)
    /// </summary>
    Task<int> CountUnreadAsync(
        HeadAlertActor actor, AlertUnreadScope scope, CancellationToken ct = default);

    /// <summary>
    /// إصدار يدوي (رئيس/شعبة لفرعه) — يعيد ملخص الصف المنشأ (D3: جسم 201).
    /// الدور والفرع قرار الخدمة: غير المخوّل <c>403</c>، ورئيس بلا فرع <c>400</c>.
    /// </summary>
    Task<HeadAlertSummaryDto> CreateAsync(
        CreateHeadAlertRequest request, HeadAlertActor actor, string? actorName, CancellationToken ct = default);

    /// <summary>
    /// تعليم تنبيه كمقروء لصاحب صفّ الاستلام وحده — <c>false</c> لغير المستلم (يُترجم 404)،
    /// و<see cref="UnauthorizedAccessException"/> لدور لا يملك استلامًا أصلًا (403).
    /// </summary>
    Task<bool> MarkReadAsync(int alertId, HeadAlertActor actor, CancellationToken ct = default);
    /// <summary>
    /// حذف انتقائي لمستلم واحد من تنبيهات استئناف (§6.2/§6.5 — إحالة/تراجع) —
    /// بلا مساس بباقي المستلمين. أفضل جهد للمتصل (لا يرمي).
    /// </summary>
    Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default);

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
/// تنبيهات رئيس القسم: الإصدار لفرعه فقط (رئيس القسم)، والاستلام بحسب الدور.
/// الحذف النهائي للفرع/المستخدم مقيد بالعلاقات، والكتابة ضمن معاملة مع سجل التدقيق.
/// </summary>
public sealed partial class HeadAlertService : IHeadAlertService
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IDelegationRepository _delegations;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly ITransactionContext _transactions;

    public HeadAlertService(
        IHeadAlertRepository alerts,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Branch> branches,
        IDelegationRepository delegations,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        ITransactionContext? transactions = null)
    {
        _alerts = alerts;
        _documents = documents;
        _users = users;
        _branches = branches;
        _delegations = delegations;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        // الحارس يفشل مغلقًا بلا سياق: أي مسار يفرض معاملة نشطة يرمي بدل أن يعمل بلا ذرّية.
        // عند تمرير TransactionRunner (وهو ITransactionContext) نستعمل سياقه نفسه لتفادي
        // فتح معاملة متداخلة عند الأرشفة داخل معاملة العمل.
        _transactions = transactions ?? tx as ITransactionContext ?? NullTransactionContext.Instance;
    }
}
