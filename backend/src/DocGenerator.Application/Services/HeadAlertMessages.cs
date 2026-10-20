using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// مركز نصوص التنبيهات — المصدر الوحيد لكل قوالب الرسائل (الخطة §7.1/§7.2).
/// لا تبقى نصوص <c>HeadAlert</c> مبثوثة في الخدمات، ولا قصّ صامت داخل أي قالب.
/// </summary>
public static class HeadAlertMessages
{
    // ── سياقات الإنابة ──

    public sealed record DelegationPendingApprovalContext(string Lawyer, string SourceLabel, string Court);

    public sealed record DelegationSentToSubheadContext(int DelegationId, string SourceLabel);

    public sealed record DelegationAssignedToLawyerContext(string DelegatedCourt, string SourceLabel);

    public sealed record DelegationRejectedWrongCourtContext(int DelegationId, string Reason);

    public sealed record DelegationPendingCompletionContext(int DelegationId, string SourceLabel);

    public sealed record DelegationCompletedReturnedContext(
        string Court,
        string SourceLabel,
        string Coverage,
        string TargetLabel,
        string AssetsLine);

    // ── سياقات المرآة (T2–T5) ──

    public sealed record DelegationMirrorDeferredContext(string ExecutedOn, string BaseNumber, string Court);

    public sealed record DelegationMirrorReturnedContext(string ExecutedOn, string BaseNumber, string Court);

    public sealed record DelegationMirrorSettledContext(string ExecutedOn, string BaseNumber, string Court);

    public sealed record DelegationMirrorFullyForciblyContext(string BaseNumber, string SourceNumber);

    public sealed record DelegationMirrorUpdatedContext(string Labels);

    public sealed record DelegationMirrorLocalAdditionContext(string Parts);

    public sealed record DelegationMirrorStatusChangedContext(string Status);

    // ── سياقات الدوائر والاستئناف والمطالعة ──

    public sealed record CircuitPendingDocumentsContext(string Circuit);

    public sealed record CircuitTransferredContext(string Circuit, int Files, int Delegations, int Appeals);

    public sealed record AppealReferralToHeadContext(string Title, string Number, string Caller);

    public sealed record AppealReturnedToHeadContext(string Title, string Number);

    public sealed record AppealPendingToOwnerContext(string Title, string Number, string FileType, string Court);

    public sealed record AppealAssignedToLawyerContext(string Title, string Number);

    public sealed record AppealFinalToBaseLawyerContext(string Title, string Number, string State, string? OutcomePart);

    public sealed record ReviewLetterReplyContext(string HeadName, string LetterNumber, string FullReply);

    // ── سياقات الجهات ──

    public sealed record EntityRenameNoticeToCreatorContext(string OldName, string NewName);

    public sealed record EntityEntryMovedContext(string Canonical, string Governorate, string Branch);

    public sealed record EntityEntriesMovedAllContext(string Source, string Governorate, string Target);

    public sealed record EntityBranchRenamedContext(string Canonical, string Governorate, string OldBranch, string NewBranch);

    public sealed record EntityBranchMergedContext(string Canonical, string Governorate, string OldBranch, string NewBranch);

    public sealed record EntityBranchAbolishedContext(string Canonical, string Governorate, string Branch);

    public sealed record EntityBranchUnifiedContext(string Canonical, string Governorate, string Branch);

    // ── الإنابة ──

    public static string DelegationPendingApproval(DelegationPendingApprovalContext context)
        => $"بانتظار اعتماد الإنابة — سطّر المحامي {context.Lawyer} إنابة على الملف ({context.SourceLabel}) إلى دائرة {context.Court}";

    public static string DelegationSentToSubhead(DelegationSentToSubheadContext context)
        => $"الإنابة من اختصاصكم — يرجى التفضل بالاطلاع والإسناد (إنابة رقم {context.DelegationId} على ملف {context.SourceLabel})";

    public static string DelegationAssignedToLawyer(DelegationAssignedToLawyerContext context)
        => $"أحال إليك رئيس القسم ملف إنابة لقيده أصولًا في {context.DelegatedCourt} (ملف {context.SourceLabel})";

    public static string DelegationRejectedWrongCourt(DelegationRejectedWrongCourtContext context)
        => $"رُفضت الإنابة رقم {context.DelegationId} للدائرة الخطأ — {context.Reason} — صحّح الدائرة من بطاقة تشعبات الملف";

    public static string DelegationPendingCompletion(DelegationPendingCompletionContext context)
        => $"بانتظار إتمام الإنابة رقم {context.DelegationId} — ملف {context.SourceLabel}";

    public static string DelegationCompletedReturned(DelegationCompletedReturnedContext context)
        => $"أُعيدت الإنابة المسطرة إلى دائرة {context.Court} في ملف {context.SourceLabel} — البدل: {context.Coverage} — المناب: {context.TargetLabel} — الأصول: {context.AssetsLine}. يرجى تحديث حالة الملف.";

    // ── المرآة (T2–T5) ──

    public static string DelegationMirrorDeferred(DelegationMirrorDeferredContext context)
        => $"ورد كتاب تريث بملف الإنابة للمنفذ عليه {context.ExecutedOn} رقم أساس {context.BaseNumber} دائرة تنفيذ {context.Court}، يرجى وقف إجراءات الإنابة";

    public static string DelegationMirrorReturned(DelegationMirrorReturnedContext context)
        => $"يرجى متابعة السير بملف المنفذ عليه {context.ExecutedOn} رقم أساس {context.BaseNumber} دائرة تنفيذ {context.Court} تبعًا لكتاب الجهة العامة للسير بالملف وانتهاء حالة التريث";

    public static string DelegationMirrorSettled(DelegationMirrorSettledContext context)
        => $"يرجى وقف إجراءات الإنابة في ملف المنفذ عليه {context.ExecutedOn} رقم أساس {context.BaseNumber} دائرة تنفيذ {context.Court}، تبعًا لاعتبار الملف المنيب منفذًا بالتسوية، وإعادته لمرجعه الأصلي";

    public static string DelegationMirrorFullyForcibly(DelegationMirrorFullyForciblyContext context)
        => $"استُرد ملفك المناب رقم أساس {context.BaseNumber} لاعتبار الملف المنيب {context.SourceNumber} منفذًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب";

    public static string DelegationMirrorUpdated(DelegationMirrorUpdatedContext context)
        => $"تم تحديث نسخة الملف المناب لمواكبة الملف المنيب: {context.Labels}";

    public static string DelegationMirrorLocalAddition(DelegationMirrorLocalAdditionContext context)
        => $"تم تعديل بيانات الملف المناب بإضافة محلية — {context.Parts}";

    public static string DelegationMirrorStatusChanged(DelegationMirrorStatusChangedContext context)
        => $"تغيّرت حالة الملف المنيب إلى «{context.Status}»";

    // ── دوائر التنفيذ ──

    public static string CircuitPendingDocuments(CircuitPendingDocumentsContext context)
        => $"أحال لك رئيس القسم ملفات من دائرة {context.Circuit} — يرجى تحديث معلوماتها";

    public static string CircuitTransferred(CircuitTransferredContext context)
        => $"نُقلت إليك دائرة «{context.Circuit}» — {context.Files} ملفًا، {context.Delegations} إنابة معلقة، {context.Appeals} استئنافًا بانتظار الإسناد";

    // ── الاستئناف ──

    public static string AppealReferralToHead(AppealReferralToHeadContext context)
        => $"يرجى التفضل بإسناد الاستئناف (ملف {context.Title} رقم {context.Number} — محال من {context.Caller})";

    public static string AppealReturnedToHead(AppealReturnedToHeadContext context)
        => $"أُعيد الاستئناف (ملف {context.Title} رقم {context.Number}) لنطاقك — يرجى اختيار محامٍ لمتابعته";

    public static string AppealPendingToOwner(AppealPendingToOwnerContext context)
        => $"وقع استئناف بملف {context.Title} رقم {context.Number} نوع {context.FileType} دائرة تنفيذ {context.Court}، يرجى اختيار محامٍ لمتابعة الاستئناف";

    public static string AppealAssignedToLawyer(AppealAssignedToLawyerContext context)
        => $"أحال إليك رئيس القسم استئنافًا لمتابعته أصولًا (ملف {context.Title} رقم {context.Number})";

    public static string AppealFinalToBaseLawyer(AppealFinalToBaseLawyerContext context)
        => $"أصبح استئناف قرار رئيس التنفيذ في الملف ({context.Title} رقم {context.Number}) {context.State}{context.OutcomePart}";

    // ── كتب المطالعة ──

    public static string ReviewLetterReply(ReviewLetterReplyContext context)
        => $"{context.HeadName} ردّ على كتاب مطالعتك رقم {context.LetterNumber}: {context.FullReply}";

    // ── الجهات العامة ──

    public static string EntityRenameNoticeToCreator(EntityRenameNoticeToCreatorContext context)
        => $"تم تعديل اسم الجهة العامة التي أدخلتها من «{context.OldName}» إلى «{context.NewName}»";

    public static string EntityEntryMoved(EntityEntryMovedContext context)
        => $"أُلحق بقيدكم فرع من هيئة أخرى: «{context.Canonical}» — {context.Governorate}/{context.Branch}";

    public static string EntityEntriesMovedAll(EntityEntriesMovedAllContext context)
        => $"تم نقل جميع قيود «{context.Source}» ({context.Governorate}) إلى «{context.Target}»";

    public static string EntityBranchRenamed(EntityBranchRenamedContext context)
        => $"تم تغيير اسم فرع جهة «{context.Canonical}» ({context.Governorate}): من «{context.OldBranch}» إلى «{context.NewBranch}»";

    public static string EntityBranchMerged(EntityBranchMergedContext context)
        => $"تم دمج فرع جهة «{context.Canonical}» ({context.Governorate}/{context.OldBranch}) في ({context.Governorate}/{context.NewBranch})";

    public static string EntityBranchAbolished(EntityBranchAbolishedContext context)
        => $"تم إلغاء فرع جهة «{context.Canonical}» ({context.Governorate}/{context.Branch}) بلا ملفات مرتبطة";

    public static string EntityBranchUnified(EntityBranchUnifiedContext context)
        => $"تم توحيد تسميات فروع جهة «{context.Canonical}» ({context.Governorate}/{context.Branch})";

    // ── التعميمات الثمانية (المصدر الموحّد: EntityChangeMessages) ──

    public static string EntityRenameLawyers(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.RenameLawyersAlert(oldCanonical, newCanonical, decreeKind, decreeNumber, decreeDate);

    public static string EntityRenameHeads(string oldCanonical, string newCanonical, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.RenameHeadsAlert(oldCanonical, newCanonical, decreeKind, decreeNumber, decreeDate);

    public static string EntityMergeLawyers(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.MergeLawyersAlert(absorbedNames, survivorName, decreeKind, decreeNumber, decreeDate);

    public static string EntityMergeHeads(string absorbedNames, string survivorName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.MergeHeadsAlert(absorbedNames, survivorName, decreeKind, decreeNumber, decreeDate);

    public static string EntityUnifyLawyers(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.UnifyLawyersAlert(unifiedNames, canonicalName, decreeKind, decreeNumber, decreeDate);

    public static string EntityUnifyHeads(string unifiedNames, string canonicalName, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.UnifyHeadsAlert(unifiedNames, canonicalName, decreeKind, decreeNumber, decreeDate);

    public static string EntityAbolishLawyers(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.AbolishLawyersAlert(newCanonical, abolishedNames, decreeKind, decreeNumber, decreeDate);

    public static string EntityAbolishHeads(string newCanonical, string abolishedNames, string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.AbolishHeadsAlert(newCanonical, abolishedNames, decreeKind, decreeNumber, decreeDate);

    // ── الإصدار اليدوي ──

    /// <summary>
    /// بادئة التنبيه اليدوي حسب دور المُصدر — موحّدة (الخطة §4.2 #31). الأدوار غير
    /// المُصدِرة لا بادئة لها (لا يُصدر يدويًا أصلًا).
    /// </summary>
    public static string ManualPrefix(UserRole role) => role switch
    {
        UserRole.Head => "تنبيه من رئيس القسم: ",
        UserRole.SubHead => "تنبيه من رئيس الشعبة: ",
        _ => string.Empty,
    };

    /// <summary>النص اليدوي بعد البادئة — بلا تكرار البادئة.</summary>
    public static string ManualMessage(UserRole role, string userMessage)
    {
        var prefix = ManualPrefix(role);
        var text = userMessage?.Trim() ?? string.Empty;
        if (prefix.Length == 0)
            return text;
        return text.StartsWith(prefix, StringComparison.Ordinal) ? text : prefix + text;
    }
}
