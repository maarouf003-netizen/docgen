using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// سياق الفاعل في مسارات التنبيهات — يمرره المتحكم من التوكن بعد فحص الصلاحية
/// العامة، وتُبنى عليه قرارات النطاق والصلاحيات في الخدمة (الخطة §9.3/§12).
/// </summary>
public sealed record HeadAlertActor(int UserId, UserRole Role, int? BranchId)
{
    public bool IsLawyer => Role == UserRole.Lawyer;
    public bool IsHeadOrSubHead => Role is UserRole.Head or UserRole.SubHead;
    public bool IsManagerOrAdmin => Role is UserRole.Manager or UserRole.Admin;
}

/// <summary>
/// فلاتر قوائم التنبيهات (الخطة §9.1): الأرشفة، غير المقروء، وربط الإنابة.
/// الترقيم يحمله <see cref="AlertPageRequest"/> المستقل (page/pageSize بحدود صارمة).
/// </summary>
public sealed record HeadAlertListQuery
{
    public AlertArchiveFilter Archive { get; init; } = AlertArchiveFilter.Active;
    public bool UnreadOnly { get; init; }
    public int? DelegationId { get; init; }
    public AlertPageRequest Page { get; init; } = new();
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
    /// </summary>
    Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
        HeadAlertActor actor, AlertArchiveFilter archive, AlertPageRequest page, CancellationToken ct = default);

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

    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);

    /// <summary>إصدار يدوي (رئيس/شعبة لفرعه) — يعيد ملخص الصف المنشأ (D3: جسم 201).</summary>
    Task<HeadAlertSummaryDto> CreateAsync(CreateHeadAlertRequest request, int actorUserId, int actorBranchId, string? actorName, CancellationToken ct = default);

    Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default);
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
public sealed class HeadAlertService : IHeadAlertService
{
    private readonly IHeadAlertRepository _alerts;
    private readonly IDocumentRepository _documents;
    private readonly IUserRepository _users;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly ITransactionContext _transactions;

    public HeadAlertService(
        IHeadAlertRepository alerts,
        IDocumentRepository documents,
        IUserRepository users,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        ITransactionContext? transactions = null)
    {
        _alerts = alerts;
        _documents = documents;
        _users = users;
        _branches = branches;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        // الحارس يفشل مغلقًا بلا سياق: أي مسار يفرض معاملة نشطة يرمي بدل أن يعمل بلا ذرّية.
        // عند تمرير TransactionRunner (وهو ITransactionContext) نستعمل سياقه نفسه لتفادي
        // فتح معاملة متداخلة عند الأرشفة داخل معاملة العمل.
        _transactions = transactions ?? tx as ITransactionContext ?? NullTransactionContext.Instance;
    }

    public async Task<PagedResult<HeadAlertSummaryDto>> ListForActorAsync(
        HeadAlertActor actor, HeadAlertListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(query);
        query.Page.Validate();

        if (actor.IsManagerOrAdmin)
        {
            // الإشراف لا يدعم «غير المقروء»: الحالة مستلم-محددة لا إشرافية (§9.1).
            if (query.UnreadOnly)
                throw new ArgumentException("خيار «غير المقروء» غير متاح للمدير/المشرف", nameof(query));

            var page = await _alerts.ListSummariesForOversightAsync(
                query.Archive, query.DelegationId, query.Page.Skip, query.Page.PageSize, ct);
            return ToPagedResult(page, query.Page);
        }

        if (actor.IsLawyer)
        {
            var page = await _alerts.ListSummariesForRecipientAsync(
                actor.UserId,
                branchId: null,
                query.Archive,
                query.UnreadOnly,
                query.DelegationId,
                query.Page.Skip,
                query.Page.PageSize,
                includeCounters: false,
                ct);
            return ToPagedResult(page, query.Page);
        }

        if (actor.IsHeadOrSubHead)
        {
            var branchId = RequireBranch(actor);
            var page = await _alerts.ListSummariesForRecipientAsync(
                actor.UserId,
                branchId,
                query.Archive,
                query.UnreadOnly,
                query.DelegationId,
                query.Page.Skip,
                query.Page.PageSize,
                includeCounters: true,
                ct);
            return ToPagedResult(page, query.Page);
        }

        throw new UnauthorizedAccessException("الدور غير مخوّل لقوائم التنبيهات");
    }

    public async Task<PagedResult<HeadAlertSummaryDto>> ListSentAsync(
        HeadAlertActor actor, AlertArchiveFilter archive, AlertPageRequest page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);
        page.Validate();

        if (!actor.IsHeadOrSubHead)
            throw new UnauthorizedAccessException("قائمة الصادر مخصصة لرئيس القسم/الشعبة");

        var branchId = RequireBranch(actor);
        var result = await _alerts.ListSummariesSentAsync(
            actor.UserId, branchId, archive, page.Skip, page.PageSize, ct);
        return ToPagedResult(result, page);
    }

    public async Task<PagedResult<HeadAlertSummaryDto>> ListByDelegationAsync(
        int delegationId, HeadAlertActor actor, AlertPageRequest page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(page);
        page.Validate();

        // «طرف في الإنابة» محامٍ فقط كما هو السلوك القائم؛ التحقق من الطرفية
        // نفسه في المتحكم عبر IDocumentDelegationService.IsPartyAsync.
        if (!actor.IsLawyer)
            throw new UnauthorizedAccessException("تنبيهات الإنابة متاحة لطرفها المحامي فقط");

        var result = await _alerts.ListSummariesByDelegationAsync(
            delegationId, actor.UserId, page.Skip, page.PageSize, ct);
        return ToPagedResult(result, page);
    }

    public async Task<HeadAlertDto?> GetDetailsAsync(
        int id, HeadAlertActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // صف واحد مسقط في SQL — لا «اجلب القائمة وابحث فيها» ولا كيانات محمَّلة (§9.2).
        var row = await _alerts.GetDetailRowAsync(id, actor.UserId, ct);
        if (row is null)
            return null;

        var isCreator = row.CreatedById == actor.UserId;
        var isOversight = actor.IsManagerOrAdmin;

        // المستلم أولًا بأولوية مطلقة (بلا شرط فرع — إنابات خارجية)، ثم المنشئ،
        // ثم الإشراف؛ غير ذلك 404 بلا كشف وجود (D2/§12).
        if (!row.IsRecipient && !isCreator && !isOversight)
            return null;

        return ToDetailDto(row, canViewRecipients: isOversight || isCreator);
    }

    public async Task<List<HeadAlertRecipientDto>?> GetRecipientsAsync(
        int id, HeadAlertActor actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var alert = await _alerts.GetByIdAsync(id, ct);
        if (alert is null)
            return null;

        // منشئ أو مدير/مشرف فقط؛ أي دور آخر 404 بلا تسريب وجود (§9.3/§12).
        if (alert.CreatedById != actor.UserId && !actor.IsManagerOrAdmin)
            return null;

        var recipients = await _alerts.ListRecipientsWithUserAsync(id, ct);
        return recipients.Select(r => new HeadAlertRecipientDto(
            r.Id,
            r.UserId,
            r.User?.FullName ?? string.Empty,
            r.User?.Role.ToString().ToLowerInvariant() ?? string.Empty,
            r.IsRead,
            r.ReadAt)).ToList();
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => _alerts.CountUnreadAsync(userId, ct);

    public async Task<HeadAlertSummaryDto> CreateAsync(
        CreateHeadAlertRequest request,
        int actorUserId,
        int actorBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = await _users.GetByIdAsync(actorUserId, ct);
        if (actor is null || !actor.IsActive
            || actor.Role is not (UserRole.Head or UserRole.SubHead)
            || actor.BranchId != actorBranchId)
            throw new ArgumentException("الإصدار اليدوي مخصص لرئيس قسم/شعبة مفعّل في فرعه");

        var rawMessage = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("نص التنبيه مطلوب");

        // الحدّ على نص المستخدم قبل البادئة، ثم سقف الأمان على النص النهائي.
        var withinLimit = HeadAlertTextLimits.EnsureWithinManualLimit(rawMessage);
        var message = HeadAlertTextLimits.EnsureWithinSafety(
            HeadAlertMessages.ManualMessage(actor.Role, withinLimit));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب");

        var branch = await _branches.GetByIdAsync(actorBranchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var targetType = ParseManualTargetType(request.TargetType);
        var recipients = await ResolveRecipientsAsync(targetType, request, actorBranchId, ct);
        if (recipients.Count == 0)
            throw new ArgumentException("لا يوجد مستلمون للتنبيه");

        var now = DateTime.UtcNow;
        var alert = new HeadAlert
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
            TargetType = targetType,
            Kind = ManualKindFor(targetType),
            DedupeKey = null,
            DocumentId = targetType == HeadAlertTargetType.Document ? request.DocumentId : null,
            TargetLawyerId = targetType == HeadAlertTargetType.Lawyer ? request.TargetLawyerId : null,
            Message = message,
            CreatedAt = now,
            LastContentChangedAt = now,
            Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
        };

        // جسم 201 (D3) يجب أن يطابق صفَّ القائمة لاحقًا حقلًا بحقل، ولذلك يُعاد قراءته
        // بالإسقاط الواحد نفسه الذي تبنيه القوائم (لا تحويل الكيان)، **داخل المعاملة**
        // كي لا يبقى صفٌّ مثبَّت يعقبه 500 إن فشل الجلب (فيتكرر الإنشاء من العميل).
        var created = await _tx.RunAsync(async token =>
        {
            await _alerts.AddAsync(alert, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_alert",
                details: $"أصدر تنبيهاً يدوياً في فرع {branch.Name} بنطاق {targetType}: "
                    + HeadAlertTextLimits.PreviewForAudit(message),
                ct: token);

            return await _alerts.GetSummaryAsync(
                    alert.Id, recipientUserId: null, includeCounters: true, token)
                ?? throw new InvalidOperationException("تعذّر جلب ملخص التنبيه بعد إنشائه");
        }, ct);

        return created;
    }

    public async Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default)
    {
        // صف الاستلام وحده (متتبَّع) — لا تحميل التنبيه ولا بقية المستلمين.
        var recipient = await _alerts.GetRecipientAsync(alertId, userId, ct);
        if (recipient is null)
            return false;

        if (recipient.IsRead)
            return true;

        // اصطلاح كل كتابات الخدمة: التعديل والحفظ داخل _tx.RunAsync (§6، المرحلة 2).
        await _tx.RunAsync(async token =>
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    /// <summary>
    /// حذف انتقائي لمستلم واحد من تنبيهات استئناف (§6.2/§6.5 — إحالة/تراجع) —
    /// بلا مساس بباقي المستلمين. أفضل جهد للمتصل (لا يرمي).
    /// </summary>
    public async Task<int> RemoveAppealRecipientAsync(int appealId, int userId, CancellationToken ct = default)
    {
        var removed = 0;
        await _tx.RunAsync(async token =>
        {
            removed = await _alerts.RemoveAppealRecipientAsync(appealId, userId, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return removed;
    }

    // ── المسار النظامي (الخطة §8.1) ──

    public async Task<HeadAlertWriteResult> CreateSystemAsync(
        CreateSystemHeadAlertRequest request,
        DateTime occurredAt,
        HeadAlertSupersedePlan? supersedePlan,
        int? targetUserIdOverride,
        CancellationToken ct = default)
    {
        // حارس المعاملة: المعالج يعمل داخل معاملة المشغّل فقط (لا يبدأ معاملة ولا
        // ينهيها، ولا يستدعي ITransactionRunner) — فلا يتداخل مع معاملة عمل.
        if (!_transactions.HasActiveTransaction)
            throw new InvalidOperationException("معالجة التنبيه النظامي تتطلب معاملة نشطة");

        ArgumentNullException.ThrowIfNull(request);

        if (!HeadAlertKindCatalog.IsWritable(request.Kind))
            throw new ArgumentException($"نوع التنبيه غير قابل للكتابة: {request.Kind}", nameof(request));
        if (!HeadAlertKindCatalog.AllowedAudiences(request.Kind).Contains(request.Audience))
            throw new ArgumentException(
                $"الجمهور {request.Audience} غير مسموح للنوع {request.Kind}", nameof(request));

        var message = HeadAlertTextLimits.EnsureWithinSafety(request.Message);
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب", nameof(request));

        // فحص المراجع قبل الإدراج (الخطة §8.1): مرجع محذوف بين التسجيل والمعالجة
        // يُنهي الرسالة كـ skipped: missing_reference، لا failed ولا إعادة لا نهائية.
        var missingReference = await _alerts.FindMissingReferenceAsync(request, ct);
        if (missingReference is not null)
            throw new HeadAlertMissingReferenceException(
                DescribeMissingReference(request, missingReference.Value));

        var creator = await _users.GetByIdAsync(request.CreatedById, ct);
        if (creator is null || !creator.IsActive)
            throw new HeadAlertMissingReferenceException("منشئ التنبيه النظامي غير موجود أو غير فعّال");

        var effective = ApplyTargetUserOverride(request, targetUserIdOverride);

        // «تجاوز التعاقب» ليس مجرد وجود TargetUserId في الحمولة (المشغّل يمرّره
        // دائمًا للجمهور الأحادي)، بل اختلافه عن مستلم الحدث الأصلي. هذا الفرق هو
        // ما يجعل تعذّر الخلف `recipient_inactive` لا `no_recipients` (الخطة §8.1).
        var overrideApplied = targetUserIdOverride is not null
            && targetUserIdOverride != request.RecipientUserId
            && targetUserIdOverride != request.TargetLawyerId;

        var (targetType, recipients) = await ResolveSystemRecipientsAsync(effective, overrideApplied, ct);
        if (recipients.Count == 0)
            throw new HeadAlertNoRecipientsException("لا يوجد مستلمون مؤهّلون للتنبيه");

        // نطاق المفتاح مبنيٌّ من **المستلِم المُحلّول** لا من الطلب الخام: جمهور
        // «منشئ الملف» قد يُشتق من الملف وحده (بلا RecipientUserId) — فبناء النطاق
        // من الطلب الخام يفشل بلا مستلم.
        var dedupeKey = HeadAlertDedupe.Build(BuildDedupeScope(effective, recipients));
        var existing = await _alerts.FindLatestActiveByDedupeKeyAsync(dedupeKey, ct);

        HeadAlert alert;
        HeadAlertWriteResult result;

        if (existing is null)
        {
            alert = new HeadAlert
            {
                BranchId = request.BranchId,
                CreatedById = request.CreatedById,
                TargetType = targetType,
                Kind = request.Kind,
                DedupeKey = dedupeKey,
                DocumentId = request.DocumentId,
                TargetLawyerId = effective.TargetLawyerId,
                DelegationId = request.DelegationId,
                AppealId = request.AppealId,
                ReviewLetterId = request.ReviewLetterId,
                PublicEntityId = request.PublicEntityId,
                ExecutionCircuitId = request.ExecutionCircuitId,
                Message = message,
                CreatedAt = occurredAt,
                LastContentChangedAt = occurredAt,
                IsArchived = false,
                Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
            };
            await _alerts.AddAsync(alert, ct);
            result = HeadAlertWriteResult.Created;
        }
        else
        {
            alert = existing;

            // حارس الحداثة: رسالة أقدم من آخر تغيير محتوى لا تمحو الأحدث.
            var lastChanged = alert.LastContentChangedAt;
            if (occurredAt < lastChanged)
                return HeadAlertWriteResult.Stale;

            if (string.Equals(alert.Message, message, StringComparison.Ordinal))
            {
                result = HeadAlertWriteResult.Unchanged;
            }
            else
            {
                alert.Message = message;
                alert.LastContentChangedAt = lastChanged > occurredAt ? lastChanged : occurredAt;
                foreach (var recipient in alert.Recipients)
                {
                    recipient.IsRead = false;
                    recipient.ReadAt = null;
                }
                result = HeadAlertWriteResult.Updated;
            }

            // المحامي الفعلي يُثبَّت في الصف (لا يبقى السلف بعد التعاقب).
            if (targetType == HeadAlertTargetType.Lawyer
                && effective.TargetLawyerId != alert.TargetLawyerId)
            {
                alert.TargetLawyerId = effective.TargetLawyerId;
            }

            MatchRecipients(alert, recipients, overrideApplied);
        }

        // ترتيب الخطة §8.6: (1) إنشاء/تحديث البديل **وحفظه** للحصول على معرّفه،
        // (2) أرشفة التنبيهات القديمة مع استثناء هذا المعرّف، (3) حفظ ثانٍ.
        // بلا الحفظ الأول يكون `excludeAlertId` صفرًا فلا استثناء — فيؤرشف البديل نفسه.
        await _uow.SaveChangesAsync(ct);

        // خطة الاستبدال: تُطبَّق بعد الإنشاء/التحديث **وأيضًا عند Unchanged**، وفي
        // المعاملة نفسها، مع استثناء التنبيه الحالي (لا يُؤرشف البديل).
        if (supersedePlan is not null)
            await ApplySupersedePlanAsync(alert, supersedePlan, ct);

        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(
            creator.FullName,
            result == HeadAlertWriteResult.Created ? "create_alert" : "update_alert",
            details: $"{HeadAlertKindCatalog.ToLabel(request.Kind)}: {HeadAlertTextLimits.PreviewForAudit(message)}",
            ct: ct);

        return result;
    }

    public async Task<int> ApplyCircuitRenamedAsync(
        int circuitId, string message, DateTime occurredAt, CancellationToken ct = default)
    {
        if (!_transactions.HasActiveTransaction)
            throw new InvalidOperationException("معالجة إعادة التسمية تتطلب معاملة نشطة");

        var text = HeadAlertTextLimits.EnsureWithinSafety(message);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("نص إعادة التسمية مطلوب", nameof(message));

        var pending = await _alerts.ListActivePendingByCircuitAndKindAsync(
            circuitId, HeadAlertKindCatalog.CircuitPendingDocuments, ct);

        var active = 0;
        foreach (var alert in pending)
        {
            // الرسالة الأقدم من آخر تغيير محتوى لا تمحو الأحدث؛ لكن الصف نشط
            // فتُعدّ الرسالة معالَجة، لا «لا تنبيهات نشطة» (الخطة §6.4).
            active++;
            var lastChanged = alert.LastContentChangedAt;
            if (occurredAt < lastChanged)
                continue;

            if (!string.Equals(alert.Message, text, StringComparison.Ordinal))
            {
                alert.Message = text;
                alert.LastContentChangedAt = lastChanged > occurredAt ? lastChanged : occurredAt;
                foreach (var recipient in alert.Recipients)
                {
                    recipient.IsRead = false;
                    recipient.ReadAt = null;
                }
            }
        }

        await _uow.SaveChangesAsync(ct);
        return active;
    }

    public Task<int> ArchiveByDelegationAsync(
        int delegationId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
    {
        ValidateArchiveArguments(archiveKinds, reason);

        // معاملة محيطة: إن وُجدت معاملة نشطة فلا تُفتح معاملة متداخلة.
        if (_transactions.HasActiveTransaction)
            return ArchiveByDelegationCoreAsync(delegationId, archiveKinds, null, reason, ct);

        return _tx.RunAsync(
            token => ArchiveByDelegationCoreAsync(delegationId, archiveKinds, null, reason, token),
            ct);
    }

    public Task<int> ArchiveByAppealAsync(
        int appealId, IReadOnlySet<string> archiveKinds, string reason, CancellationToken ct = default)
    {
        ValidateArchiveArguments(archiveKinds, reason);

        if (_transactions.HasActiveTransaction)
            return ArchiveByAppealCoreAsync(appealId, archiveKinds, null, reason, ct);

        return _tx.RunAsync(
            token => ArchiveByAppealCoreAsync(appealId, archiveKinds, null, reason, token),
            ct);
    }

    /// <summary>
    /// الأرشفة الأساسية — بلا <c>ITransactionRunner</c> (تعمل داخل معاملة المتصل).
    /// </summary>
    internal async Task<int> ArchiveByDelegationCoreAsync(
        int delegationId,
        IReadOnlySet<string> archiveKinds,
        int? excludeAlertId,
        string reason,
        CancellationToken ct)
    {
        var archived = await _alerts.ArchiveByScopeAndKindsAsync(
            delegationId, HeadAlertScopeKind.Delegation, archiveKinds, excludeAlertId, ct);

        if (archived > 0)
            await _audit.LogAsync(null, "archive_alert",
                details: $"أُرشفت {archived} تنبيه إنابة ({reason})", ct: ct);

        return archived;
    }

    /// <summary>الأرشفة الأساسية للاستئناف — بلا <c>ITransactionRunner</c>.</summary>
    internal async Task<int> ArchiveByAppealCoreAsync(
        int appealId,
        IReadOnlySet<string> archiveKinds,
        int? excludeAlertId,
        string reason,
        CancellationToken ct)
    {
        var archived = await _alerts.ArchiveByScopeAndKindsAsync(
            appealId, HeadAlertScopeKind.Appeal, archiveKinds, excludeAlertId, ct);

        if (archived > 0)
            await _audit.LogAsync(null, "archive_alert",
                details: $"أُرشفت {archived} تنبيه استئناف ({reason})", ct: ct);

        return archived;
    }

    private static void ValidateArchiveArguments(IReadOnlySet<string> archiveKinds, string reason)
    {
        if (archiveKinds is null || archiveKinds.Count == 0)
            throw new ArgumentException("مجموعة أنواع الأرشفة إلزامية", nameof(archiveKinds));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("سبب الأرشفة إلزامي", nameof(reason));
    }

    private async Task ApplySupersedePlanAsync(
        HeadAlert current,
        HeadAlertSupersedePlan plan,
        CancellationToken ct)
    {
        if (plan.DelegationId is not null)
        {
            await ArchiveByDelegationCoreAsync(
                plan.DelegationId.Value, plan.ArchiveKinds, current.Id, plan.Reason, ct);
        }
        else if (plan.AppealId is not null)
        {
            await ArchiveByAppealCoreAsync(
                plan.AppealId.Value, plan.ArchiveKinds, current.Id, plan.Reason, ct);
        }
    }

    /// <summary>
    /// يطبّق تجاوز المستلم (عند التعاقب) — يحل محل <c>RecipientUserId</c>/
    /// <c>TargetLawyerId</c> في الجمهور الأحادي فقط.
    /// </summary>
    private static CreateSystemHeadAlertRequest ApplyTargetUserOverride(
        CreateSystemHeadAlertRequest request,
        int? targetUserIdOverride)
    {
        if (targetUserIdOverride is null)
            return request;

        if (request.Audience is not (HeadAlertAudience.SingleLawyer or HeadAlertAudience.SingleHead))
            throw new ArgumentException("تجاوز المستلم يقتصر على الجمهور الأحادي", nameof(targetUserIdOverride));

        return request with
        {
            RecipientUserId = targetUserIdOverride.Value,
            TargetLawyerId = request.Audience == HeadAlertAudience.SingleLawyer
                ? targetUserIdOverride.Value
                : null,
        };
    }

    /// <summary>يبني نطاق الجمهور من الطلب والمستلِم المُحلّول — الجمهور هو المرجع لا <c>TargetType</c>.</summary>
    private static HeadAlertDedupeScope BuildDedupeScope(
        CreateSystemHeadAlertRequest request,
        IReadOnlyCollection<User> recipients)
    {
        var resolvedRecipient = request.Audience switch
        {
            HeadAlertAudience.SingleLawyer => request.RecipientUserId ?? request.TargetLawyerId,
            HeadAlertAudience.SingleHead => request.RecipientUserId,
            // «منشئ الملف»: يُشتق من الملف عند غياب RecipientUserId — والمستلِم
            // المُحلول هو المرجع في بناء المفتاح.
            HeadAlertAudience.DocumentCreator => recipients.FirstOrDefault()?.Id,
            _ => null,
        };

        return new HeadAlertDedupeScope
        {
            Kind = request.Kind,
            Audience = request.Audience,
            BranchId = request.BranchId,
            RecipientUserId = resolvedRecipient,
            DelegationId = request.DelegationId,
            AppealId = request.AppealId,
            ReviewLetterId = request.ReviewLetterId,
            DocumentId = request.DocumentId,
            PublicEntityId = request.PublicEntityId,
            ExecutionCircuitId = request.ExecutionCircuitId,
            EntityOperation = request.EntityOperation,
            EntityGroupKey = request.EntityGroupKey,
        };
    }

    /// <summary>
    /// وصف عربي مختصر للمرجع المفقود — يُسجَّل مع رسالة التخطي للتشخيص.
    /// </summary>
    private static string DescribeMissingReference(
        CreateSystemHeadAlertRequest request, HeadAlertReferenceKind kind)
    {
        return kind switch
        {
            HeadAlertReferenceKind.Branch =>
                $"مرجع التنبيه النظامي مفقود: الفرع ({request.BranchId})",
            HeadAlertReferenceKind.Document =>
                $"مرجع التنبيه النظامي مفقود: الملف ({request.DocumentId})",
            HeadAlertReferenceKind.Delegation =>
                $"مرجع التنبيه النظامي مفقود: الإنابة ({request.DelegationId})",
            HeadAlertReferenceKind.Appeal =>
                $"مرجع التنبيه النظامي مفقود: الاستئناف ({request.AppealId})",
            HeadAlertReferenceKind.ReviewLetter =>
                $"مرجع التنبيه النظامي مفقود: كتاب المطالعة ({request.ReviewLetterId})",
            HeadAlertReferenceKind.PublicEntity =>
                $"مرجع التنبيه النظامي مفقود: الجهة العامة ({request.PublicEntityId})",
            HeadAlertReferenceKind.ExecutionCircuit =>
                $"مرجع التنبيه النظامي مفقود: دائرة التنفيذ ({request.ExecutionCircuitId})",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "نوع مرجع غير معروف"),
        };
    }

    /// <summary>
    /// يحدّد <c>TargetType</c> المخزَّن من <c>Audience</c> (لا العكس) ويحسب المستلمين:
    /// الجمهور الأحادي من الطلب، والجمهور المتعدد من المستخدمين النشطين وقت المعالجة.
    /// </summary>
    private async Task<(HeadAlertTargetType TargetType, List<User> Recipients)> ResolveSystemRecipientsAsync(
        CreateSystemHeadAlertRequest request,
        bool overrideApplied,
        CancellationToken ct)
    {
        switch (request.Audience)
        {
            case HeadAlertAudience.SingleLawyer:
            {
                var lawyerId = request.RecipientUserId ?? request.TargetLawyerId;
                if (lawyerId is null)
                    throw new ArgumentException("الجمهور الأحادي يحتاج مستلمًا صريحًا", nameof(request));
                if (request.RecipientUserId is not null
                    && request.TargetLawyerId is not null
                    && request.RecipientUserId != request.TargetLawyerId)
                    throw new ArgumentException("المستلم المحدد يختلف عن المحامي الهدف", nameof(request));

                var lawyer = await _users.GetByIdAsync(lawyerId.Value, ct);
                if (lawyer is null || !lawyer.IsActive || lawyer.Role != UserRole.Lawyer)
                {
                    if (overrideApplied)
                        throw new HeadAlertRecipientInactiveException("المحامي الخلف غير موجود أو غير فعّال");
                    throw new HeadAlertNoRecipientsException("المحامي المستلم غير موجود أو غير فعّال");
                }

                return (HeadAlertTargetType.Lawyer, new List<User> { lawyer });
            }

            case HeadAlertAudience.SingleHead:
            {
                // غياب المستلم هنا ليس خطأ برمجيًا: مسارات الإنابة تُسجّل الرسالة حتى
                // عند غياب رئيس نشط للفرع/القسم، فتُعامل حالة عمل بلا مستلمين (تخطٍّ)
                // لا استثناءً عامًا يُعاد ثم يفشل بعد استنفاد المحاولات.
                if (request.RecipientUserId is null)
                    throw new HeadAlertNoRecipientsException("لا يوجد رئيس مستلم للتنبيه");
                if (request.TargetLawyerId is not null)
                    throw new ArgumentException("الجمهور «رئيس» لا يقبل محاميًا هدفًا", nameof(request));

                var head = await _users.GetByIdAsync(request.RecipientUserId.Value, ct);
                if (head is null || !head.IsActive || head.Role is not (UserRole.Head or UserRole.SubHead))
                {
                    if (overrideApplied)
                        throw new HeadAlertRecipientInactiveException("الرئيس الخلف غير موجود أو غير فعّال");
                    throw new HeadAlertNoRecipientsException("الرئيس المستلم غير موجود أو غير فعّال");
                }

                return (HeadAlertTargetType.Head, new List<User> { head });
            }

            case HeadAlertAudience.DocumentCreator:
            {
                if (request.TargetLawyerId is not null)
                    throw new ArgumentException("الجمهور «منشئ الملف» لا يقبل محاميًا هدفًا", nameof(request));

                User? creator = null;
                if (request.RecipientUserId is not null)
                {
                    creator = await _users.GetByIdAsync(request.RecipientUserId.Value, ct);
                }
                else if (request.DocumentId is not null)
                {
                    var document = await _documents.GetByIdAsync(request.DocumentId.Value, ct);
                    creator = document?.CreatedBy;
                }
                else
                {
                    throw new ArgumentException(
                        "جمهور «منشئ الملف» يحتاج مستلمًا صريحًا أو ملفًا صالحًا", nameof(request));
                }

                if (creator is null || !creator.IsActive || creator.Role != UserRole.Lawyer)
                    throw new HeadAlertNoRecipientsException("منشئ الملف غير موجود أو غير فعّال");

                if (request.DocumentId is not null && request.RecipientUserId is not null)
                {
                    var document = await _documents.GetByIdAsync(request.DocumentId.Value, ct);
                    if (document?.CreatedById != creator.Id)
                        throw new ArgumentException("المستلم لا يطابق منشئ الملف", nameof(request));
                }

                return (HeadAlertTargetType.Document, new List<User> { creator });
            }

            case HeadAlertAudience.BranchLawyers:
            {
                if (request.RecipientUserId is not null || request.TargetLawyerId is not null)
                    throw new ArgumentException("البث لا يقبل مستلمًا صريحًا", nameof(request));

                var lawyers = await _alerts.ListActiveLawyersAsync(request.BranchId, ct);
                return (HeadAlertTargetType.Lawyer, lawyers);
            }

            case HeadAlertAudience.BranchHeads:
            {
                if (request.RecipientUserId is not null || request.TargetLawyerId is not null)
                    throw new ArgumentException("البث لا يقبل مستلمًا صريحًا", nameof(request));

                var heads = await _alerts.ListActiveHeadsAsync(request.BranchId, ct);
                return (HeadAlertTargetType.Head, heads);
            }

            default:
                throw new ArgumentException("جمهور التنبيه غير معروف", nameof(request));
        }
    }

    /// <summary>
    /// مطابقة المستلمين: يُضاف المستلم الجديد بـ<code>IsRead=false</code>، ويُحافظ على
    /// حالة قراءة الموجودين، ولا يُحذف مستلم قديم إلا في مسار تعاقب/حذف مستخدم صريح.
    ///
    /// عند تعاقب (تجاوز مستلم) يُستبدل السلف بالبديل: «لا يبقى السلف في التنبيه»،
    /// وتُنقل حالة القراءة بالدمج (§8.5: المقروء = المقروء ∪ المقروء، والقراءة الأقدم).
    /// </summary>
    private static void MatchRecipients(
        HeadAlert alert,
        IReadOnlyCollection<User> recipients,
        bool overrideApplied)
    {
        var inheritedRead = false;
        DateTime? inheritedReadAt = null;
        int? overrideRecipientId = null;

        if (overrideApplied)
        {
            var keep = recipients.Select(u => u.Id).ToHashSet();
            overrideRecipientId = recipients.FirstOrDefault()?.Id;
            var removed = alert.Recipients.Where(r => !keep.Contains(r.UserId)).ToList();

            foreach (var recipient in removed)
                alert.Recipients.Remove(recipient);

            if (removed.Count > 0)
            {
                inheritedRead = removed.Any(r => r.IsRead);
                var readTimes = removed
                    .Where(r => r.ReadAt.HasValue)
                    .Select(r => r.ReadAt!.Value)
                    .ToList();
                if (readTimes.Count > 0)
                    inheritedReadAt = readTimes.Min();

                // الخلف موجود مسبقًا: يُدمج الآن. وإن لم يكن موجودًا فسيُضاف
                // أدناه بحالة السلف الموروثة — لا تُفقد القراءة لأن الترتيب
                // السابق كان يحذف السلف قبل إضافة الخلف.
                var existing = alert.Recipients.FirstOrDefault(r => keep.Contains(r.UserId));
                if (existing is not null)
                {
                    existing.IsRead = existing.IsRead || inheritedRead;
                    existing.ReadAt = EarliestReadAt(existing.ReadAt, inheritedReadAt);
                }
            }
        }

        var present = alert.Recipients.Select(r => r.UserId).ToHashSet();
        foreach (var user in recipients)
        {
            if (present.Contains(user.Id))
                continue;

            var isInherited = overrideApplied && user.Id == overrideRecipientId && inheritedRead;
            alert.Recipients.Add(new HeadAlertRecipient
            {
                UserId = user.Id,
                IsRead = isInherited,
                ReadAt = isInherited ? inheritedReadAt : null,
            });
        }
    }

    private static DateTime? EarliestReadAt(DateTime? first, DateTime? second)
    {
        if (first is null)
            return second;
        if (second is null)
            return first;
        return first <= second ? first : second;
    }

    private async Task<List<User>> ResolveRecipientsAsync(
        HeadAlertTargetType targetType,
        CreateHeadAlertRequest request,
        int branchId,
        CancellationToken ct)
    {
        switch (targetType)
        {
            case HeadAlertTargetType.Document:
            {
                if (request.DocumentId is null)
                    throw new ArgumentException("يجب تحديد الملف المرتبط بالتنبيه");

                var doc = await _documents.GetByIdAsync(request.DocumentId.Value, ct);
                if (doc is null)
                    throw new ArgumentException("الملف غير موجود");
                if (doc.BranchId != branchId)
                    throw new ArgumentException("الملف ليس ضمن فرعك");
                if (doc.CreatedBy is null || doc.CreatedBy.Role != UserRole.Lawyer)
                    throw new ArgumentException("لا يوجد محامٍ مختص لهذا الملف");

                return new List<User> { doc.CreatedBy };
            }
            case HeadAlertTargetType.Lawyer:
            {
                if (request.TargetLawyerId is null)
                    throw new ArgumentException("يجب تحديد المحامي المستلم");

                var lawyer = await _users.GetByIdAsync(request.TargetLawyerId.Value, ct);
                if (lawyer is null || lawyer.Role != UserRole.Lawyer || lawyer.BranchId != branchId)
                    throw new ArgumentException("المحامي غير موجود ضمن فرعك");

                return new List<User> { lawyer };
            }
            case HeadAlertTargetType.Branch:
                return await _alerts.ListActiveLawyersAsync(branchId, ct);
            default:
                throw new ArgumentException("نوع التنبيه غير صالح");
        }
    }

    /// <summary>
    /// تحليل صريح لنطاق الإصدار اليدوي — لا <c>Enum.TryParse</c> (يرفض الأرقام)
    /// ولا النطاق النظامي <c>head</c> (لا يملك العميل دورة حياته).
    /// </summary>
    private static HeadAlertTargetType ParseManualTargetType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "document" => HeadAlertTargetType.Document,
        "lawyer" => HeadAlertTargetType.Lawyer,
        "branch" => HeadAlertTargetType.Branch,
        _ => throw new ArgumentException("نوع التنبيه غير صالح"),
    };

    private static string ManualKindFor(HeadAlertTargetType targetType) => targetType switch
    {
        HeadAlertTargetType.Document => HeadAlertKindCatalog.ManualToDocument,
        HeadAlertTargetType.Lawyer => HeadAlertKindCatalog.ManualToLawyer,
        HeadAlertTargetType.Branch => HeadAlertKindCatalog.ManualToBranch,
        _ => throw new ArgumentException("نوع التنبيه غير صالح"),
    };

    private static PagedResult<HeadAlertSummaryDto> ToPagedResult(
        HeadAlertSummaryPage page, AlertPageRequest request) => new()
        {
            Items = page.Items.ToList(),
            Page = request.Page,
            PerPage = request.PageSize,
            TotalCount = page.TotalCount,
        };

    /// <summary>الفرع إلزامي لرئيس القسم/الشعبة — وإلا فالحساب غير صالح للاستخدام.</summary>
    private static int RequireBranch(HeadAlertActor actor) =>
        actor.BranchId ?? throw new UnauthorizedAccessException("حساب رئيس القسم/الشعبة غير مرتبط بفرع");

    /// <summary>
    /// تحويل صف التفاصيل المسقط إلى العقد الكامل (الخطة §9.2): النص الكامل هنا وحده،
    /// و<c>IsRead</c> حالة القارئ نفسه (أو <c>null</c> لغير المستلم)، و<c>CanViewRecipients</c>
    /// قرار خادم — لا يملك العميل تعيينه.
    /// </summary>
    private static HeadAlertDto ToDetailDto(HeadAlertDetailRow row, bool canViewRecipients) => new(
        row.Id,
        row.Kind,
        row.Message,
        row.TargetType.ToString().ToLowerInvariant(),
        row.DocumentId,
        HeadAlertTitle.Build(
            row.DocumentId,
            row.DocumentExists,
            row.BorrowerName,
            row.BorrowerFather,
            row.BorrowerFamily,
            row.DocumentType),
        row.TargetLawyerId,
        row.TargetLawyerName,
        row.DelegationId,
        row.AppealId,
        row.ReviewLetterId,
        row.PublicEntityId,
        row.ExecutionCircuitId,
        row.CreatedAt,
        row.LastContentChangedAt,
        row.IsArchived,
        row.ArchivedAt,
        row.CreatedByName,
        row.IsRead,
        canViewRecipients,
        row.RecipientCount,
        row.UnreadCount);
}
