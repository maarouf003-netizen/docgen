using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// نقل آثار التنبيهات من رئيس سابق إلى خلفه ضمن معاملة التعطيل نفسها (الخطة §8.5).
///
/// القواعد الملزمة:
/// <list type="bullet">
/// <item>تُنقل صفوف الاستلام غير المقروءة فقط، ولا تُمسّ التنبيهات المقروءة أو المؤرشفة.</item>
/// <item>يُعاد بناء <c>DedupeKey</c> باستبدال مقطع المستلم، وإلا بقي حدث الخلف مفتاحًا مكررًا.</item>
/// <item>يُدمج الاستلام المكرر عند وجود الخلف، وتُدمج التنبيهات عند تصادم المفتاح الجديد.</item>
/// <item>يُحدَّث <c>TargetUserId</c> في رسائل Outbox المعلّقة (عمودًا وحمولةً).</item>
/// <item>أي تعارض فريد على مفتاح المنع يُعاد مرة واحدة فقط بعد تراجع المعاملة وإعادة القراءة.</item>
/// </list>
/// </summary>
public interface IHeadAlertSuccessionService
{
    Task<HeadAlertSuccessionResult> ApplyAsync(
        int predecessorId,
        int successorId,
        string predecessorName,
        string successorName,
        string? actorName,
        CancellationToken ct = default);
}

/// <summary>ملخّص ما نُفّذ في التعاقب — للاختبارات والتشخيص.</summary>
public sealed record HeadAlertSuccessionResult(
    int MovedRecipients,
    int MergedRecipients,
    int MergedAlerts,
    int RekeyedAlerts,
    int UpdatedOutboxMessages);

/// <summary>تطبيق §8.5 على التنبيهات وصندوق الصادر.</summary>
public sealed class HeadAlertSuccessionService : IHeadAlertSuccessionService
{
    private const string DedupeConflictMessage =
        "تعذّر إتمام التعاقب: تعارض متكرر على مفتاح منع التكرار في التنبيهات — أعد المحاولة بعد مراجعة التنبيهات النشطة.";

    private const string SuccessionSavepoint = "head_alert_succession_attempt";

    private readonly IHeadAlertRepository _alerts;
    private readonly IHeadAlertOutboxStore _outbox;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _errors;
    private readonly ITransactionContext _transactions;

    public HeadAlertSuccessionService(
        IHeadAlertRepository alerts,
        IHeadAlertOutboxStore outbox,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier errors,
        ITransactionContext transactions)
    {
        _alerts = alerts;
        _outbox = outbox;
        _uow = uow;
        _audit = audit;
        _errors = errors;
        _transactions = transactions;
    }

    public async Task<HeadAlertSuccessionResult> ApplyAsync(
        int predecessorId,
        int successorId,
        string predecessorName,
        string successorName,
        string? actorName,
        CancellationToken ct = default)
    {
        if (!_transactions.HasActiveTransaction)
            throw new InvalidOperationException("تعاقب التنبيهات يتطلب معاملة نشطة");

        await _transactions.CreateSavepointAsync(SuccessionSavepoint, ct);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await ApplyCoreAsync(
                    predecessorId, successorId, predecessorName, successorName, actorName, ct);
            }
            catch (Exception ex) when (IsDedupeKeyConflict(ex))
            {
                if (attempt > 0)
                    throw new InvalidOperationException(DedupeConflictMessage, ex);

                await _transactions.RollbackToSavepointAsync(SuccessionSavepoint, ct);
                _uow.ClearChanges();
            }
        }
    }

    private async Task<HeadAlertSuccessionResult> ApplyCoreAsync(
        int predecessorId,
        int successorId,
        string predecessorName,
        string successorName,
        string? actorName,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var result = await MoveAlertsAsync(predecessorId, successorId, now, ct);

        // رسائل Outbox المعلّقة قد تحمل السلف في العمود والحمولة معًا؛ تُحدَّث قبل
        // أي حفظ لاحق حتى لا يعالجها المشغّل لمستلم معطّل.
        var outboxUpdated = await _outbox.UpdateTargetUserForSuccessionAsync(
            predecessorId, successorId, ct);

        await _uow.SaveChangesAsync(ct);
        await WriteAuditAsync(
            actorName, predecessorName, successorName, result, outboxUpdated, ct);
        return result with { UpdatedOutboxMessages = outboxUpdated };
    }

    private async Task<HeadAlertSuccessionResult> MoveAlertsAsync(
        int predecessorId,
        int successorId,
        DateTime now,
        CancellationToken ct)
    {
        var alerts = await _alerts.ListActiveUnreadByRecipientWithRecipientsAsync(predecessorId, ct);
        var moved = 0;
        var mergedRecipients = 0;
        var mergedAlerts = 0;
        var rekeyed = 0;

        foreach (var alert in alerts)
        {
            if (alert.IsArchived)
                continue;

            var source = alert.Recipients.FirstOrDefault(r => r.UserId == predecessorId && !r.IsRead);
            if (source is null)
                continue;

            var target = alert.Recipients.FirstOrDefault(r => r.UserId == successorId);
            if (target is not null)
            {
                MergeReadState(target, source);
                alert.Recipients.Remove(source);
                mergedRecipients++;
            }
            else
            {
                // تغيير UserId على الصف نفسه يحفظ القراءة/ReadAt ولا ينشئ صفًا
                // مكررًا؛ الفهرس الفريد (HeadAlertId, UserId) محفوظ.
                source.UserId = successorId;
                moved++;
            }

            if (alert.TargetLawyerId == predecessorId)
                alert.TargetLawyerId = successorId;

            var oldKey = alert.DedupeKey;
            var newKey = HeadAlertDedupe.RekeyRecipient(oldKey, predecessorId, successorId);
            if (string.IsNullOrEmpty(newKey) || string.Equals(oldKey, newKey, StringComparison.Ordinal))
                continue;

            var collision = await _alerts.FindLatestActiveByDedupeKeyAsync(newKey, ct);
            if (collision is null || collision.Id == alert.Id)
            {
                alert.DedupeKey = newKey;
                rekeyed++;
                continue;
            }

            var holder = ChooseHolder(alert, collision);
            var loser = ReferenceEquals(holder, alert) ? collision : alert;
            MergeAlertInto(holder, loser, predecessorId, successorId, now);

            if (ReferenceEquals(holder, alert))
                alert.DedupeKey = newKey;

            mergedAlerts++;
        }

        return new HeadAlertSuccessionResult(moved, mergedRecipients, mergedAlerts, rekeyed, 0);
    }

    /// <summary>
    /// يدمج تنبيهين نشطين لنفس التيار وفق سياسة §8.5: الأحدث
    /// <c>LastContentChangedAt</c> ثم <c>CreatedAt</c> ثم <c>Id</c> هو الحامل.
    /// </summary>
    private static void MergeAlertInto(
        HeadAlert holder,
        HeadAlert loser,
        int predecessorId,
        int successorId,
        DateTime now)
    {
        if (ReferenceEquals(holder, loser))
            return;

        foreach (var other in loser.Recipients.ToList())
        {
            var existing = holder.Recipients.FirstOrDefault(r => r.UserId == other.UserId);
            if (existing is null)
            {
                holder.Recipients.Add(new HeadAlertRecipient
                {
                    UserId = other.UserId,
                    IsRead = other.IsRead,
                    ReadAt = other.ReadAt,
                });
            }
            else
            {
                MergeReadState(existing, other);
            }

            loser.Recipients.Remove(other);
        }

        if (holder.TargetLawyerId == predecessorId)
            holder.TargetLawyerId = successorId;

        loser.IsArchived = true;
        loser.ArchivedAt = now;

        // لا يُترك مفتاح المنع على الصف المؤرشف: الفهرس الفريد جزئي، لكن ترتيب
        // تحديث EF بين إعادة مفتاح الحامل وأرشفة الخاسر غير مضمون؛ إفراغ مفتاح
        // الخاسر يمنع تعارضًا حتى لو سبق تحديث الحامل. المؤرشف لا يدخل في المنع.
        loser.DedupeKey = null;
    }

    private static HeadAlert ChooseHolder(HeadAlert current, HeadAlert existing)
    {
        var currentStamp = current.LastContentChangedAt;
        var existingStamp = existing.LastContentChangedAt;
        if (currentStamp != existingStamp)
            return currentStamp > existingStamp ? current : existing;
        if (current.CreatedAt != existing.CreatedAt)
            return current.CreatedAt > existing.CreatedAt ? current : existing;
        return current.Id >= existing.Id ? current : existing;
    }

    /// <summary>المقروء = اجتماع حالة القراءة، والقراءة الأقدم غير الفارغة.</summary>
    private static void MergeReadState(HeadAlertRecipient target, HeadAlertRecipient source)
    {
        target.IsRead = target.IsRead || source.IsRead;
        target.ReadAt = Earliest(target.ReadAt, source.ReadAt);
    }

    private static DateTime? Earliest(DateTime? first, DateTime? second)
    {
        if (first is null)
            return second;
        if (second is null)
            return first;
        return first <= second ? first : second;
    }

    private bool IsDedupeKeyConflict(Exception exception)
    {
        if (!_errors.IsUniqueViolation(exception))
            return false;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("DedupeKey", StringComparison.Ordinal)
                || current.Message.Contains("UX_HeadAlerts_DedupeKey_Active", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private async Task WriteAuditAsync(
        string? actorName,
        string predecessorName,
        string successorName,
        HeadAlertSuccessionResult result,
        int outboxUpdated,
        CancellationToken ct)
    {
        if (result.MovedRecipients > 0 || result.MergedRecipients > 0)
        {
            await _audit.LogAsync(
                actorName,
                "alert_recipient_merged",
                details: $"succession: نُقلت {result.MovedRecipients} استلامات غير مقروءة "
                    + $"ودُمجت {result.MergedRecipients} استلامات مكررة من {predecessorName} "
                    + $"إلى {successorName}",
                ct: ct);
        }

        if (result.MergedAlerts > 0)
        {
            await _audit.LogAsync(
                actorName,
                "archive_alert",
                details: $"succession_merge: دُمجت {result.MergedAlerts} تنبيهات نشطة "
                    + $"لنفس المفتاح أثناء تعاقب {predecessorName} → {successorName}",
                ct: ct);
        }

        if (result.RekeyedAlerts > 0 || outboxUpdated > 0)
        {
            await _audit.LogAsync(
                actorName,
                "alert_succession",
                details: $"succession: أُعيد مفتاح {result.RekeyedAlerts} تنبيهات نشطة "
                    + $"من {predecessorName} إلى {successorName}"
                    + (outboxUpdated > 0 ? $"، وأُعيد توجيه {outboxUpdated} رسائل صادرة معلّقة" : string.Empty),
                ct: ct);
        }
    }
}
