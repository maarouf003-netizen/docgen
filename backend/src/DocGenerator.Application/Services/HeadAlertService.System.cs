using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class HeadAlertService : IHeadAlertService
{
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
}
