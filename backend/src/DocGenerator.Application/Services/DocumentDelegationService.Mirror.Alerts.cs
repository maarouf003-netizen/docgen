using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentDelegationService
{
    // ── إطلاق التنبيهات عبر Outbox (داخل معاملة العمل إن وُجدت) ─────────────────────

    /// <summary>إطلاق تنبيهات المرآة المجمعة عبر Outbox. تُسجَّل داخل معاملة العمل إن كانت
    /// قائمة، وإلا تُفتح معاملة قصيرة لضمان ثبات رسائل Outbox.</summary>
    public async Task FireMirrorNotificationsAsync(IReadOnlyList<MirrorNotification> intents, CancellationToken ct)
    {
        if (intents.Count == 0)
            return;

        if (_tx is ITransactionContext { HasActiveTransaction: true })
        {
            foreach (var intent in intents)
                await EnqueueMirrorAlertAsync(intent, ct);
            await _uow.SaveChangesAsync(ct);
            return;
        }

        await _tx.RunAsync(async token =>
        {
            foreach (var intent in intents)
                await EnqueueMirrorAlertAsync(intent, token);
            await _uow.SaveChangesAsync(token);
        }, ct);
    }

    private Task EnqueueMirrorAlertAsync(MirrorNotification intent, CancellationToken ct)
        => _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                intent.Kind,
                intent.Message,
                HeadAlertAudience.SingleLawyer,
                intent.RecipientBranchId,
                intent.ActorUserId,
                DelegationId: intent.DelegationId,
                RecipientUserId: intent.RecipientLawyerId),
            new HeadAlertSupersedePlan(
                intent.DelegationId,
                AppealId: null,
                HeadAlertKindCatalog.DelegationMirrorKinds,
                "delegation_mirror_updated"),
            occurredAt: DateTime.UtcNow,
            ct: ct);

    /// <summary>تنبيه تغيّر حالة المنيب إلى مناباته المعلقة (مسار StatusChangeModal المنفصل عن التعديل).
    /// الصيغ T2-T5 بصياغات المستخدم المقفلة؛ المشطوب (N7) والجزئيا (N1) لا يصلان هنا أصلًا
    /// (نوع فارغ في تغيير الحالة) — ومسار فك الشطب يبقي الصيغة العامة القديمة.
    /// يُستدعى داخل معاملة تغيير الحالة حصرًا.</summary>
    public async Task FireStatusChangeAlertsAsync(Document doc, DelegationStatusAlertKind kind, CancellationToken ct)
    {
        var delegations = await _delegations.ListPendingBySourceWithTargetsAsync(doc.Id, ct);

        var recipientBranch = doc.BranchId;
        var intents = new List<MirrorNotification>();
        foreach (var delegation in delegations)
        {
            var target = delegation.TargetDocument;
            if (target is null)
                continue;
            var (message, alertKind) = BuildDelegationStatusChangeAlert(kind, doc, target);
            intents.Add(new MirrorNotification(
                delegation.Id,
                target.CreatedById,
                target.BranchId ?? recipientBranch ?? delegation.ExternalBranchId ?? 0,
                doc.CreatedById,
                message,
                alertKind));
        }

        await FireMirrorNotificationsAsync(intents, ct);
    }

    /// <summary>بناء رسالة تنبيه حالة المنيب ونوعه: الصيغ T2-T5 (بصياغة المستخدم حرفيًا) بحقول
    /// المناب (اسم المنفذ عليه/رقم أساسه/دائرته) والمنيب (الكتب/رقمه) — تُعبَّأ runtime.</summary>
    private (string Message, string Kind) BuildDelegationStatusChangeAlert(
        DelegationStatusAlertKind kind, Document doc, Document target)
    {
        var executedOn = DocumentText.ActorFullName(target);
        var baseNumber = EffectiveFileIdentity.Number(target, ServerClock.CurrentYear(_clock, _timeZone)) ?? string.Empty;
        var court = AlertCourtBare(target.Court);

        return kind switch
        {
            DelegationStatusAlertKind.Deferred => (
                HeadAlertMessages.DelegationMirrorDeferred(new(executedOn, baseNumber, court)),
                HeadAlertKindCatalog.DelegationMirrorDeferred),
            DelegationStatusAlertKind.Returned => (
                HeadAlertMessages.DelegationMirrorReturned(new(executedOn, baseNumber, court)),
                HeadAlertKindCatalog.DelegationMirrorReturned),
            DelegationStatusAlertKind.Settled => (
                HeadAlertMessages.DelegationMirrorSettled(new(executedOn, baseNumber, court)),
                HeadAlertKindCatalog.DelegationMirrorSettled),
            DelegationStatusAlertKind.FullyForcibly => (
                HeadAlertMessages.DelegationMirrorFullyForcibly(new(
                    baseNumber,
                    EffectiveFileIdentity.Number(doc, ServerClock.CurrentYear(_clock, _timeZone)) ?? string.Empty)),
                HeadAlertKindCatalog.DelegationMirrorFullyForcibly),
            _ => (
                HeadAlertMessages.DelegationMirrorStatusChanged(new(DocumentStatusResolver.Resolve(doc))),
                HeadAlertKindCatalog.DelegationMirrorStatusChanged),
        };
    }

    /// <summary>
    /// يثبّت الإضافات المحلية (وريث/ممثل) على الملف المناب في سجل الإنابة نفسه (عدّاد +
    /// ملخص آخر دفعة + وقت UTC) — كتابة دائمة داخل معاملة المستدعي — ثم يطلق تنبيه المنيب.
    /// </summary>
    public async Task RecordLocalAdditionsAndFireReportAsync(
        Document target, TargetEditReport report, CancellationToken ct)
    {
        var delegation = await _delegations.FindByTargetAsync(target.Id, ct);
        if (delegation?.SourceDocument is null)
            return;

        // السجل على صف الإنابة لا على التنبيه: التنبيه قابل للأرشفة والتنظيف، وبطاقة
        // «تشعبات الملف» يجب أن تُبقي أثر الإضافة المحلية بعد اختفاء التنبيه.
        RecordLocalAdditions(delegation, report);
        await _uow.SaveChangesAsync(ct);

        var source = delegation.SourceDocument;
        var intent = new MirrorNotification(
            delegation.Id,
            source.CreatedById,
            source.BranchId ?? target.BranchId ?? 0,
            target.CreatedById,
            ReverseAdditionsMessage(report),
            HeadAlertKindCatalog.DelegationMirrorLocalAddition);
        await FireMirrorNotificationsAsync([intent], ct);
    }

    /// <summary>
    /// يثبّت دفعة الإضافات المحلية على صف الإنابة: العدّاد تراكمي (عدد العناصر لا عدد الحفظات)،
    /// والملخص أحدث ما اتّسع له العمود (<see cref="LocalAdditionSummary"/>)، والوقت
    /// <see cref="DateTime.UtcNow"/> حصرًا.
    ///
    /// **يحرّك <c>Version</c> عمدًا:** الكتابة هنا تعدّل صف الإنابة، وبلا تحريك توكن الصف
    /// يبني EF شرط <c>WHERE Version = old</c> يوافق عند كاتبَين متزامنَين ⇒ ينجح الاثنان
    /// ويسقط تحديث أحد العدّادين بصمت (آخر كاتب يفوز). كل مسارات الكتابة الأخرى على
    /// الإنابة تحرّكه، وهذا ليس استثناءً. وسلامة العدّاد اليوم لا تعتمد على كتابة صف
    /// الملف المناب (اقتران خفيّ) بل على توكن الصف نفسه.
    /// </summary>
    private static void RecordLocalAdditions(DocumentDelegation delegation, TargetEditReport report)
    {
        var parts = CollectAdditionParts(report);
        if (parts.Count == 0)
            return;

        var now = DateTime.UtcNow;
        delegation.LocalAdditionCount += parts.Count;
        delegation.LocalAdditionLastMessage = LocalAdditionSummary.Build(parts);
        delegation.LocalAdditionLastAt = now;
        delegation.Version++;
        delegation.UpdatedAt = now;
    }

    private static string ReverseAdditionsMessage(TargetEditReport report)
        => HeadAlertMessages.DelegationMirrorLocalAddition(
            new(string.Join(LocalAdditionSummary.Separator, CollectAdditionParts(report))));

    /// <summary>أجزاء رسالة الإضافة المحلية بترتيب الإدخال: الورثة المضافون ثم الممثلون الشرعيون.</summary>
    private static List<string> CollectAdditionParts(TargetEditReport report)
    {
        var parts = new List<string>(report.AddedHeirs.Count + report.NewRepresentativeLabels.Count);
        foreach (var h in report.AddedHeirs)
        {
            if (string.IsNullOrWhiteSpace(h.Name))
                continue;
            var suffix = h.GuarantorNumber is { } num ? $" لكفيل-{num}" : string.Empty;
            parts.Add($"أُضيف الوريث {h.Name}{suffix}");
        }
        parts.AddRange(report.NewRepresentativeLabels.Select(label => $"أُضيف {label}"));
        return parts;
    }
}
