using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface IHeadAlertService
{
    Task<List<HeadAlertDto>> ListForLawyerAsync(int userId, CancellationToken ct = default);
    /// <summary>
    /// تنبيهات الرئيس (§8: قراءة بالمستلم لا بالفرع) — ما استلمه في فرعه فقط.
    /// </summary>
    Task<List<HeadAlertDto>> ListForHeadAsync(int userId, int branchId, CancellationToken ct = default);
    /// <summary>
    /// ما أصدره الرئيس في فرعه (تبويب «أرسلتها»): يُعرض للقراءة فقط — بلا زر
    /// تعليم مقروء (المُرسِل ليس مستلمًا) وبلا أثر في العدّادات والتعاقب.
    /// </summary>
    Task<List<HeadAlertDto>> ListSentAsync(int userId, int branchId, CancellationToken ct = default);
    /// <summary>
    /// قائمة الإشراف (المدير/المشرف — قرار المالك 2026-10-16): كل التنبيهات عبر
    /// الفروع، قراءة فقط، وحالة القراءة فارغة (الحالة مستلم-محددة أصلًا).
    /// </summary>
    Task<List<HeadAlertDto>> ListForOversightAsync(CancellationToken ct = default);
    /// <summary>تنبيه مفرد للإشراف (المدير/المشرف) — بلا فلترة مستلم.</summary>
    Task<HeadAlertDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<int> CountUnreadAsync(int userId, CancellationToken ct = default);
    Task<HeadAlertDto> CreateAsync(CreateHeadAlertRequest request, int actorUserId, int actorBranchId, string? actorName, CancellationToken ct = default);
    Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default);
    Task<HeadAlertDto?> UpdateDelegationAlertAsync(int delegationId, string message, CancellationToken ct = default);
    /// <summary>
    /// دمج تنبيه المرآة (منيب ↔ مناب) لمستلمٍ بعينه: يحدّث أحدث تنبيهٍ بنفس
    /// (DelegationId + المستلم) ورسالته، أو يُنشئ تنبيهًا من نوع lawyer للمستلم الصريح —
    /// بلا قيد تطابق الفرع (يناسب الإنابات الخارجية). يُرجِعه لغرض العرض بعد التحديث/الإنشاء.
    /// </summary>
    Task<HeadAlertDto?> UpsertDelegationMessageAsync(
        int delegationId, int recipientLawyerId, int actorUserId, int actorBranchId, string message, CancellationToken ct = default);
    /// <summary>تنبيهات إنابةٍ معيّنة («معلومات الملف المنيب»/«تشعبات الملف») بعلامة قراءة بحسب المستخدم.</summary>
    Task<List<HeadAlertDto>> ListByDelegationAsync(int delegationId, int userId, CancellationToken ct = default);
    Task<bool> DeleteByDelegationAsync(int delegationId, CancellationToken ct = default);
    /// <summary>حذف كل تنبيهات الاستئناف (تصفية تنبيه «اختيار المحامي» بعد الإسناد).</summary>
    Task<bool> DeleteByAppealAsync(int appealId, CancellationToken ct = default);
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
        _transactions = transactions ?? NullTransactionContext.Instance;
    }

    public async Task<List<HeadAlertDto>> ListForLawyerAsync(int userId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListForRecipientAsync(userId, ct);
        return alerts.Select(a => ToLawyerDto(a, userId)).ToList();
    }

    public async Task<List<HeadAlertDto>> ListForHeadAsync(int userId, int branchId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByRecipientInBranchAsync(userId, branchId, ct);
        return alerts.Select(a => ToHeadDto(a, userId)).ToList();
    }

    public async Task<List<HeadAlertDto>> ListSentAsync(int userId, int branchId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListSentAsync(userId, branchId, ct);
        return alerts.Select(a => ToHeadDto(a, userId)).ToList();
    }

    public Task<List<HeadAlertDto>> ListForOversightAsync(CancellationToken ct = default)
        // المستودع يسقط مباشرة إلى `HeadAlertDto` (عدّادات محسوبة قاعديًا) —
        // لا تحويل هنا حتى لا يُعاد تحميل أي تنقل.
        => _alerts.ListForOversightAsync(ct);

    public async Task<HeadAlertDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdWithRecipientsAsync(id, ct);
        return alert is null ? null : ToHeadDto(alert);
    }

    public Task<int> CountUnreadAsync(int userId, CancellationToken ct = default)
        => _alerts.CountUnreadAsync(userId, ct);

    public async Task<HeadAlertDto> CreateAsync(
        CreateHeadAlertRequest request,
        int actorUserId,
        int actorBranchId,
        string? actorName,
        CancellationToken ct = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("نص التنبيه مطلوب");

        var branch = await _branches.GetByIdAsync(actorBranchId, ct);
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");

        var targetType = ParseTargetType(request.TargetType);
        var recipients = await ResolveRecipientsAsync(targetType, request, actorBranchId, ct);
        if (recipients.Count == 0)
            throw new ArgumentException("لا يوجد مستلمون للتنبيه");

        var alert = new HeadAlert
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
            TargetType = targetType,
            DocumentId = targetType == HeadAlertTargetType.Lawyer ? null : request.DocumentId,
            TargetLawyerId = targetType == HeadAlertTargetType.Lawyer ? request.TargetLawyerId : null,
            DelegationId = request.DelegationId,
            AppealId = request.AppealId,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            Recipients = recipients.Select(u => new HeadAlertRecipient { UserId = u.Id }).ToList(),
        };

        await _tx.RunAsync(async token =>
        {
            await _alerts.AddAsync(alert, token);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "create_alert",
                details: $"أصدر تنبيهاً في فرع {branch.Name} بنطاق {targetType}: {message[..Math.Min(message.Length, 80)]}",
                ct: token);
        }, ct);

        return ToHeadDto(alert);
    }

    public async Task<bool> MarkReadAsync(int alertId, int userId, CancellationToken ct = default)
    {
        var alert = await _alerts.GetByIdWithRecipientsAsync(alertId, ct);
        if (alert is null)
            return false;

        var recipient = alert.Recipients.FirstOrDefault(r => r.UserId == userId);
        if (recipient is null)
            return false;

        if (recipient.IsRead)
            return true;

        await _tx.RunAsync(async token =>
        {
            recipient.IsRead = true;
            recipient.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    /// <summary>
    /// تحديث رسالة آخر تنبيه لإنابة معلّقة (بانتظار الاعتماد) بعد تعديل الإنابة —
    /// يعيد null عندما لا يوجد تنبيه للإنابة (لم يُنشأ حينها)، ويُبقي المستلمين وعلامات القراءة.
    /// </summary>
    public async Task<HeadAlertDto?> UpdateDelegationAlertAsync(int delegationId, string message, CancellationToken ct = default)
    {
        var alert = await _alerts.FindLatestByDelegationAsync(delegationId, ct);
        if (alert is null)
            return null;

        var trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص التنبيه مطلوب");
        if (alert.Message == trimmed)
            return ToHeadDto(alert);

        alert.Message = trimmed;
        await _tx.RunAsync(async token =>
        {
            _alerts.Update(alert);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return ToHeadDto(alert);
    }

    /// <summary>
    /// دمج تنبيه المرآة (منيب ↔ مناب) لمستلمٍ بعينه: يحدّث أحدث تنبيهٍ بنفس
    /// (DelegationId + المستلم) ورسالته (ويُحدَّث زمنه ليقفز للصدارة)، أو يُنشئ تنبيهًا
    /// من نوع lawyer للمستلم الصريح بلا قيد تطابق الفرع (يناسب الإنابات الخارجية).
    /// سجل التعديلات يحمل التاريخ الكامل — التنبيه ملخص آخر تغيّر فقط.
    /// </summary>
    public async Task<HeadAlertDto?> UpsertDelegationMessageAsync(
        int delegationId, int recipientLawyerId, int actorUserId, int actorBranchId, string message, CancellationToken ct = default)
    {
        var trimmed = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("نص التنبيه مطلوب");

        var existing = await _alerts.FindLatestByDelegationAndRecipientAsync(delegationId, recipientLawyerId, ct);
        if (existing is not null)
        {
            if (existing.Message == trimmed)
                return ToLawyerDto(existing, recipientLawyerId);

            existing.Message = trimmed;
            existing.CreatedAt = DateTime.UtcNow;
            await _tx.RunAsync(async token =>
            {
                _alerts.Update(existing);
                await _uow.SaveChangesAsync(token);
            }, ct);
            return ToLawyerDto(existing, recipientLawyerId);
        }

        var lawyer = await _users.GetByIdAsync(recipientLawyerId, ct);
        if (lawyer is null || lawyer.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي المستلم غير موجود");

        var alert = new HeadAlert
        {
            BranchId = actorBranchId,
            CreatedById = actorUserId,
            TargetType = HeadAlertTargetType.Lawyer,
            DocumentId = null,
            TargetLawyerId = recipientLawyerId,
            DelegationId = delegationId,
            Message = trimmed,
            CreatedAt = DateTime.UtcNow,
            Recipients = new List<HeadAlertRecipient> { new() { UserId = recipientLawyerId } },
        };

        await _tx.RunAsync(async token =>
        {
            await _alerts.AddAsync(alert, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return ToLawyerDto(alert, recipientLawyerId);
    }

    /// <summary>تنبيهات إنابةٍ معيّنة («معلومات الملف المنيب»/«تشعبات الملف») بعلامة قراءة بحسب المستخدم.</summary>
    public async Task<List<HeadAlertDto>> ListByDelegationAsync(int delegationId, int userId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByDelegationWithRecipientsAsync(delegationId, ct);
        return alerts.Select(a => ToLawyerDto(a, userId)).ToList();
    }

    /// <summary>حذف كل تنبيهات الإنابة (تصفية المرحلية منها عند الاعتماد أو الإتمام أو حذف الإنابة).</summary>
    public async Task<bool> DeleteByDelegationAsync(int delegationId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByDelegationAsync(delegationId, ct);
        if (alerts.Count == 0)
            return false;

        await _tx.RunAsync(async token =>
        {
            foreach (var alert in alerts)
                _alerts.Remove(alert);
            await _uow.SaveChangesAsync(token);
        }, ct);
        return true;
    }

    /// <summary>حذف كل تنبيهات الاستئناف (تصفية تنبيه «اختيار المحامي» بعد إسناد الاستئناف).</summary>
    public async Task<bool> DeleteByAppealAsync(int appealId, CancellationToken ct = default)
    {
        var alerts = await _alerts.ListByAppealAsync(appealId, ct);
        if (alerts.Count == 0)
            return false;

        await _tx.RunAsync(async token =>
        {
            foreach (var alert in alerts)
                _alerts.Remove(alert);
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

        var creator = await _users.GetByIdAsync(request.CreatedById, ct);
        if (creator is null || !creator.IsActive)
            throw new ArgumentException("منشئ التنبيه النظامي غير موجود أو غير فعّال", nameof(request));

        var effective = ApplyTargetUserOverride(request, targetUserIdOverride);
        var overrideApplied = targetUserIdOverride is not null;
        var (targetType, recipients) = await ResolveSystemRecipientsAsync(effective, ct);
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
            var lastChanged = alert.LastContentChangedAt ?? alert.CreatedAt;
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

        var updated = 0;
        foreach (var alert in pending)
        {
            var lastChanged = alert.LastContentChangedAt ?? alert.CreatedAt;
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

            updated++;
        }

        await _uow.SaveChangesAsync(ct);
        return updated;
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
        };
    }

    /// <summary>
    /// يحدّد <c>TargetType</c> المخزَّن من <c>Audience</c> (لا العكس) ويحسب المستلمين:
    /// الجمهور الأحادي من الطلب، والجمهور المتعدد من المستخدمين النشطين وقت المعالجة.
    /// </summary>
    private async Task<(HeadAlertTargetType TargetType, List<User> Recipients)> ResolveSystemRecipientsAsync(
        CreateSystemHeadAlertRequest request,
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
                    throw new HeadAlertNoRecipientsException("المحامي المستلم غير موجود أو غير فعّال");

                return (HeadAlertTargetType.Lawyer, new List<User> { lawyer });
            }

            case HeadAlertAudience.SingleHead:
            {
                if (request.RecipientUserId is null)
                    throw new ArgumentException("الجمهور الأحادي يحتاج مستلمًا صريحًا", nameof(request));
                if (request.TargetLawyerId is not null)
                    throw new ArgumentException("الجمهور «رئيس» لا يقبل محاميًا هدفًا", nameof(request));

                var head = await _users.GetByIdAsync(request.RecipientUserId.Value, ct);
                if (head is null || !head.IsActive || head.Role is not (UserRole.Head or UserRole.SubHead))
                    throw new HeadAlertNoRecipientsException("الرئيس المستلم غير موجود أو غير فعّال");

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
        if (overrideApplied)
        {
            var keep = recipients.Select(u => u.Id).ToHashSet();
            var removed = alert.Recipients.Where(r => !keep.Contains(r.UserId)).ToList();

            foreach (var recipient in removed)
                alert.Recipients.Remove(recipient);

            if (removed.Count > 0)
            {
                var successor = alert.Recipients.FirstOrDefault(r => keep.Contains(r.UserId));
                if (successor is not null)
                {
                    var readTimes = removed
                        .Where(r => r.ReadAt.HasValue)
                        .Select(r => r.ReadAt!.Value)
                        .ToList();

                    successor.IsRead = successor.IsRead || removed.Any(r => r.IsRead);
                    if (readTimes.Count > 0)
                    {
                        var earliest = readTimes.Min();
                        successor.ReadAt = successor.ReadAt is null || successor.ReadAt > earliest
                            ? earliest
                            : successor.ReadAt;
                    }
                }
            }
        }

        var existing = alert.Recipients.Select(r => r.UserId).ToHashSet();
        foreach (var user in recipients)
        {
            if (existing.Contains(user.Id))
                continue;

            alert.Recipients.Add(new HeadAlertRecipient { UserId = user.Id, IsRead = false });
        }
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
            case HeadAlertTargetType.Head:
                // تنبيهات النظام لروّاد القسم (مراحل الإنابة): تصل لرؤساء أقسام الفرع المفعلين،
                // وعند غياب أي رئيس يُرفض الإنشاء ويُسجَّل فشل الإشعار في سجل التدقيق.
                // التوجيه لمالك واحد (§6.1 — قرار §2.21): `RecipientUserId` يقيّد
                // المستلمين به حصرًا بعد التحقق (رئيس مفعّل في الفرع).
                if (request.RecipientUserId.HasValue)
                {
                    var directed = await _users.GetByIdAsync(request.RecipientUserId.Value, ct);
                    if (directed is null || !directed.IsActive
                        || directed.Role is not (UserRole.Head or UserRole.SubHead)
                        || directed.BranchId != branchId)
                        throw new ArgumentException("المستلم الموجَّه ليس رئيسًا مفعّلًا في الفرع");
                    return new List<User> { directed };
                }
                return await _alerts.ListActiveHeadsAsync(branchId, ct);
            default:
                throw new ArgumentException("نوع التنبيه غير صالح");
        }
    }

    private static HeadAlertTargetType ParseTargetType(string? value)
    {
        if (Enum.TryParse<HeadAlertTargetType>(value?.Trim(), ignoreCase: true, out var parsed))
            return parsed;
        throw new ArgumentException("نوع التنبيه غير صالح");
    }

    private static string DocumentTitle(HeadAlert alert)
    {
        var doc = alert.Document;
        if (doc is null)
            return alert.DocumentId is null ? string.Empty : "مستند محذوف";

        var parts = new[] { doc.BorrowerName, doc.BorrowerFather, doc.BorrowerFamily }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var name = parts.Any() ? string.Join(" ", parts) : doc.DocumentType;
        return string.IsNullOrWhiteSpace(name) ? $"مستند {doc.Id}" : name;
    }

    private static HeadAlertDto ToLawyerDto(HeadAlert a, int userId) => new(
        a.Id,
        a.Message,
        a.TargetType.ToString().ToLowerInvariant(),
        a.DocumentId,
        DocumentTitle(a),
        a.TargetLawyerId,
        a.TargetLawyer?.FullName,
        a.Recipients.FirstOrDefault(r => r.UserId == userId)?.IsRead,
        null,
        null,
        a.CreatedAt,
        a.CreatedBy?.FullName,
        a.AppealId,
        a.ReviewLetterId,
        a.DelegationId);

    /// <summary>عرض الإنشاء/التحديث (بلا قارئ معيَّن — حالة القراءة فارغة).</summary>
    private static HeadAlertDto ToHeadDto(HeadAlert a) => ToHeadDto(a, -1);

    private static HeadAlertDto ToHeadDto(HeadAlert a, int userId) => new(
        a.Id,
        a.Message,
        a.TargetType.ToString().ToLowerInvariant(),
        a.DocumentId,
        DocumentTitle(a),
        a.TargetLawyerId,
        a.TargetLawyer?.FullName,
        a.Recipients.FirstOrDefault(r => r.UserId == userId)?.IsRead,
        a.Recipients.Count,
        a.Recipients.Count(r => !r.IsRead),
        a.CreatedAt,
        a.CreatedBy?.FullName,
        a.AppealId,
        a.ReviewLetterId,
        a.DelegationId);
}
