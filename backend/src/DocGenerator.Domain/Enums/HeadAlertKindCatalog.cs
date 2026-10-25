namespace DocGenerator.Domain.Enums;

/// <summary>
/// كتالوج أنواع التنبيهات الدلالية (<c>HeadAlert.Kind</c>) — مصدر الحقيقة الوحيد لأنواع
/// المعتمدة، ومجموعات العائلات، والجمهور المسموح لكل نوع.
///
/// القواعد الحاكمة (خطة إعادة هيكلة التنبيهات §5):
/// <list type="bullet">
/// <item>39 رمزًا نظاميًا/يدويًا + الرمز المحجوز <see cref="Legacy"/>.</item>
/// <item>لا يُكتب <see cref="Legacy"/> في أي مسار تشغيل — للصفوف التاريخية فقط.</item>
/// <item>الرمزان <see cref="EntityRegistryEntryReview"/> و<see cref="EntityEditProposalReview"/>
/// متقاعدان: لا كتابة جديدة لهما (نُقل أثرهما إلى سجل المراجعة).</item>
/// <item>كل رمز نظامي له جمهور واحد على الأقل؛ والمسارات اليدوية لا تدخل المسار النظامي.</item>
/// </list>
/// </summary>
public static class HeadAlertKindCatalog
{
    // ── أ. الإنابة (13) ──

    public const string DelegationPendingApproval = "delegation_pending_approval";
    public const string DelegationSentToSubhead = "delegation_sent_to_subhead";
    public const string DelegationAssignedToLawyer = "delegation_assigned_to_lawyer";
    public const string DelegationRejectedWrongCourt = "delegation_rejected_wrong_court";
    public const string DelegationPendingCompletion = "delegation_pending_completion";
    public const string DelegationCompletedReturned = "delegation_completed_returned";
    public const string DelegationMirrorDeferred = "delegation_mirror_deferred";
    public const string DelegationMirrorReturned = "delegation_mirror_returned";
    public const string DelegationMirrorSettled = "delegation_mirror_settled";
    public const string DelegationMirrorFullyForcibly = "delegation_mirror_fully_forcibly";
    public const string DelegationMirrorUpdated = "delegation_mirror_updated";
    public const string DelegationMirrorLocalAddition = "delegation_mirror_local_addition";
    public const string DelegationMirrorStatusChanged = "delegation_mirror_status_changed";

    // ── ب. دوائر التنفيذ (2) ──

    public const string CircuitPendingDocuments = "circuit_pending_documents";
    public const string CircuitTransferred = "circuit_transferred";

    // ── ج. الاستئناف (5) ──

    public const string AppealReferralToHead = "appeal_referral_to_head";
    public const string AppealReturnedToHead = "appeal_returned_to_head";
    public const string AppealPendingToOwner = "appeal_pending_to_owner";
    public const string AppealAssignedToLawyer = "appeal_assigned_to_lawyer";
    public const string AppealFinalToBaseLawyer = "appeal_final_to_base_lawyer";

    // ── د. كتب المطالعة (1) ──

    public const string ReviewLetterReply = "review_letter_reply";

    // ── هـ. الجهات العامة (6) ──

    public const string EntityRenameNoticeToCreator = "entity_rename_notice_to_creator";
    public const string EntityBranchChanged = "entity_branch_changed";
    public const string EntityEntryMoved = "entity_entry_moved";
    public const string EntityEntriesMovedAll = "entity_entries_moved_all";
    public const string EntityRegistryEntryReview = "entity_registry_entry_review";
    public const string EntityEditProposalReview = "entity_edit_proposal_review";

    // ── و. الإصدار اليدوي (4) ──

    public const string ManualToDocument = "manual_to_document";
    public const string ManualToLawyer = "manual_to_lawyer";
    public const string ManualToBranch = "manual_to_branch";
    public const string ManualToHead = "manual_to_head";

    // ── ز. التعميمات الثمانية (8) ──

    public const string EntityRenameLawyers = "entity_rename_lawyers";
    public const string EntityRenameHeads = "entity_rename_heads";
    public const string EntityMergeLawyers = "entity_merge_lawyers";
    public const string EntityMergeHeads = "entity_merge_heads";
    public const string EntityUnifyLawyers = "entity_unify_lawyers";
    public const string EntityUnifyHeads = "entity_unify_heads";
    public const string EntityAbolishLawyers = "entity_abolish_lawyers";
    public const string EntityAbolishHeads = "entity_abolish_heads";

    /// <summary>الرمز المحجوز للصفوف القديمة غير القابلة للتصنيف — لا يُكتب في أي مسار جديد.</summary>
    public const string Legacy = "legacy";

    /// <summary>كل الرموز المعتمدة (39) — بلا الرمز المحجوز <see cref="Legacy"/>.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        // الإنابة
        DelegationPendingApproval,
        DelegationSentToSubhead,
        DelegationAssignedToLawyer,
        DelegationRejectedWrongCourt,
        DelegationPendingCompletion,
        DelegationCompletedReturned,
        DelegationMirrorDeferred,
        DelegationMirrorReturned,
        DelegationMirrorSettled,
        DelegationMirrorFullyForcibly,
        DelegationMirrorUpdated,
        DelegationMirrorLocalAddition,
        DelegationMirrorStatusChanged,
        // دوائر التنفيذ
        CircuitPendingDocuments,
        CircuitTransferred,
        // الاستئناف
        AppealReferralToHead,
        AppealReturnedToHead,
        AppealPendingToOwner,
        AppealAssignedToLawyer,
        AppealFinalToBaseLawyer,
        // كتب المطالعة
        ReviewLetterReply,
        // الجهات العامة
        EntityRenameNoticeToCreator,
        EntityBranchChanged,
        EntityEntryMoved,
        EntityEntriesMovedAll,
        EntityRegistryEntryReview,
        EntityEditProposalReview,
        // الإصدار اليدوي
        ManualToDocument,
        ManualToLawyer,
        ManualToBranch,
        ManualToHead,
        // التعميمات الثمانية
        EntityRenameLawyers,
        EntityRenameHeads,
        EntityMergeLawyers,
        EntityMergeHeads,
        EntityUnifyLawyers,
        EntityUnifyHeads,
        EntityAbolishLawyers,
        EntityAbolishHeads,
    };

    /// <summary>الرموز المتقاعدة: لا كتابة جديدة لها (نُقل أثرها إلى سجل المراجعة).</summary>
    public static readonly IReadOnlySet<string> Retired = new HashSet<string>(StringComparer.Ordinal)
    {
        EntityRegistryEntryReview,
        EntityEditProposalReview,
    };

    /// <summary>رموز المسار اليدوي — لا تدخل <c>CreateSystemAsync</c> ولا المسار النظامي.</summary>
    public static readonly IReadOnlySet<string> ManualKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        ManualToDocument,
        ManualToLawyer,
        ManualToBranch,
        ManualToHead,
    };

    /// <summary>عائلة تنبيهات الإنابة — تُستخدم في <c>ArchiveByDelegationAsync</c>.</summary>
    public static readonly IReadOnlySet<string> DelegationKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        DelegationPendingApproval,
        DelegationSentToSubhead,
        DelegationAssignedToLawyer,
        DelegationRejectedWrongCourt,
        DelegationPendingCompletion,
        DelegationCompletedReturned,
        DelegationMirrorDeferred,
        DelegationMirrorReturned,
        DelegationMirrorSettled,
        DelegationMirrorFullyForcibly,
        DelegationMirrorUpdated,
        DelegationMirrorLocalAddition,
        DelegationMirrorStatusChanged,
    };

    /// <summary>عائلة تنبيهات مرايا الإنابة — يُستبدل بعضها ببعض عند تغيّر الحالة.</summary>
    public static readonly IReadOnlySet<string> DelegationMirrorKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        DelegationMirrorDeferred,
        DelegationMirrorReturned,
        DelegationMirrorSettled,
        DelegationMirrorFullyForcibly,
        DelegationMirrorUpdated,
        DelegationMirrorLocalAddition,
        DelegationMirrorStatusChanged,
    };

    /// <summary>عائلة تنبيهات الاستئناف — تُستخدم في <c>ArchiveByAppealAsync</c>.</summary>
    public static readonly IReadOnlySet<string> AppealKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        AppealReferralToHead,
        AppealReturnedToHead,
        AppealPendingToOwner,
        AppealAssignedToLawyer,
        AppealFinalToBaseLawyer,
    };

    /// <summary>عائلة تنبيهات دوائر التنفيذ — تُستخدم في مسار أرشفة المعلقات.</summary>
    public static readonly IReadOnlySet<string> CircuitKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        CircuitPendingDocuments,
        CircuitTransferred,
    };

    /// <summary>
    /// عائلة تنبيهات كتب المطالعة — **بلا مستهلك أرشفة في هذه الجولة** (لا يوجد
    /// <c>ArchiveByReviewLetter*</c>)؛ تُبقى للتناظر والتحقق المستقبلي.
    /// </summary>
    public static readonly IReadOnlySet<string> ReviewLetterKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        ReviewLetterReply,
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<HeadAlertAudience>> AudienceMap =
        new Dictionary<string, IReadOnlySet<HeadAlertAudience>>(StringComparer.Ordinal)
        {
            // ── SingleHead ──
            [DelegationPendingApproval] = Set(HeadAlertAudience.SingleHead),
            [DelegationSentToSubhead] = Set(HeadAlertAudience.SingleHead),
            [DelegationPendingCompletion] = Set(HeadAlertAudience.SingleHead),
            [CircuitTransferred] = Set(HeadAlertAudience.SingleHead),
            [AppealReferralToHead] = Set(HeadAlertAudience.SingleHead),
            [AppealReturnedToHead] = Set(HeadAlertAudience.SingleHead),
            [AppealPendingToOwner] = Set(HeadAlertAudience.SingleHead),
            [EntityRegistryEntryReview] = Set(HeadAlertAudience.SingleHead),
            [EntityEditProposalReview] = Set(HeadAlertAudience.SingleHead),

            // ── SingleLawyer ──
            [DelegationMirrorDeferred] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorReturned] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorSettled] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorFullyForcibly] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorUpdated] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorLocalAddition] = Set(HeadAlertAudience.SingleLawyer),
            [DelegationMirrorStatusChanged] = Set(HeadAlertAudience.SingleLawyer),
            [CircuitPendingDocuments] = Set(HeadAlertAudience.SingleLawyer),
            [AppealAssignedToLawyer] = Set(HeadAlertAudience.SingleLawyer),

            // ── DocumentCreator ──
            [DelegationAssignedToLawyer] = Set(HeadAlertAudience.DocumentCreator),
            [DelegationRejectedWrongCourt] = Set(HeadAlertAudience.DocumentCreator),
            [DelegationCompletedReturned] = Set(HeadAlertAudience.DocumentCreator),
            [AppealFinalToBaseLawyer] = Set(HeadAlertAudience.DocumentCreator),

            // ── SingleLawyer أو SingleHead (حسب دور المالك) ──
            [ReviewLetterReply] = Set(HeadAlertAudience.SingleLawyer, HeadAlertAudience.SingleHead),
            [EntityRenameNoticeToCreator] = Set(HeadAlertAudience.SingleLawyer, HeadAlertAudience.SingleHead),

            // ── BranchHeads ──
            [EntityBranchChanged] = Set(HeadAlertAudience.BranchHeads),
            [EntityEntryMoved] = Set(HeadAlertAudience.BranchHeads),
            [EntityEntriesMovedAll] = Set(HeadAlertAudience.BranchHeads),
            [EntityRenameHeads] = Set(HeadAlertAudience.BranchHeads),
            [EntityMergeHeads] = Set(HeadAlertAudience.BranchHeads),
            [EntityUnifyHeads] = Set(HeadAlertAudience.BranchHeads),
            [EntityAbolishHeads] = Set(HeadAlertAudience.BranchHeads),

            // ── BranchLawyers ──
            [EntityRenameLawyers] = Set(HeadAlertAudience.BranchLawyers),
            [EntityMergeLawyers] = Set(HeadAlertAudience.BranchLawyers),
            [EntityUnifyLawyers] = Set(HeadAlertAudience.BranchLawyers),
            [EntityAbolishLawyers] = Set(HeadAlertAudience.BranchLawyers),
        };

    private static IReadOnlySet<HeadAlertAudience> Set(params HeadAlertAudience[] audiences)
        => new HashSet<HeadAlertAudience>(audiences);

    /// <summary>هل الرمز معروف (من الـ39)؟ الرمز المحجوز <see cref="Legacy"/> ليس جزءًا منه.</summary>
    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind);

    /// <summary>
    /// هل يُسمح بكتابة هذا الرمز؟ يستبعد الرمز المحجوز <see cref="Legacy"/> والرموز المتقاعدة.
    /// </summary>
    public static bool IsWritable(string? kind)
        => IsValid(kind) && !Retired.Contains(kind!);

    /// <summary>هل الرمز من المسار اليدوي؟</summary>
    public static bool IsManual(string? kind)
        => kind is not null && ManualKinds.Contains(kind);

    /// <summary>
    /// الجمهور المسموح للنوع. يرفض الرمز غير المعروف و<see cref="Legacy"/> ورموز المسار
    /// اليدوي (لا تدخل المسار النظامي)، ويذكر الرمز المتقاعد بجمهوره لكن
    /// <see cref="IsWritable"/> هو الذي يمنع كتابته.
    /// </summary>
    /// <exception cref="ArgumentException">رمز غير معروف أو محجوز أو يدوي.</exception>
    public static IReadOnlySet<HeadAlertAudience> AllowedAudiences(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            throw new ArgumentException("نوع التنبيه مطلوب", nameof(kind));
        if (kind == Legacy)
            throw new ArgumentException("نوع التنبيه المحجوز (legacy) لا يقبل كتابة جديدة", nameof(kind));
        if (ManualKinds.Contains(kind))
            throw new ArgumentException("أنواع التنبيه اليدوي لا تدخل المسار النظامي", nameof(kind));
        if (!AudienceMap.TryGetValue(kind, out var audiences))
            throw new ArgumentException($"نوع التنبيه غير معروف: {kind}", nameof(kind));
        return audiences;
    }

    /// <summary>التسمية العربية المعروضة للنوع.</summary>
    public static string ToLabel(string kind) => kind switch
    {
        DelegationPendingApproval => "بانتظار اعتماد الإنابة",
        DelegationSentToSubhead => "إنابة من اختصاص الشعبة",
        DelegationAssignedToLawyer => "إسناد ملف إنابة",
        DelegationRejectedWrongCourt => "رفض إنابة للدائرة الخطأ",
        DelegationPendingCompletion => "بانتظار إتمام الإنابة",
        DelegationCompletedReturned => "إعادة الإنابة المسطرة",
        DelegationMirrorDeferred => "كتاب تريث (تأجيل)",
        DelegationMirrorReturned => "عودة السير",
        DelegationMirrorSettled => "تسوية المديونية",
        DelegationMirrorFullyForcibly => "استرداد جبري",
        DelegationMirrorUpdated => "تحديث نسخة المناب",
        DelegationMirrorLocalAddition => "إضافة محلية على المناب",
        DelegationMirrorStatusChanged => "تغيّر حالة المنيب العام",
        CircuitPendingDocuments => "ملفات دائرة بانتظار التحديث",
        CircuitTransferred => "نقل دائرة",
        AppealReferralToHead => "إحالة استئناف للإسناد",
        AppealReturnedToHead => "إعادة استئناف للنطاق",
        AppealPendingToOwner => "استئناف بانتظار اختيار محامٍ",
        AppealAssignedToLawyer => "إسناد استئناف",
        AppealFinalToBaseLawyer => "نتيجة الاستئناف",
        ReviewLetterReply => "رد على كتاب المطالعة",
        EntityRenameNoticeToCreator => "تغيير اسم الجهة (إشعار المنشئ)",
        EntityBranchChanged => "تغيير فرع الجهة",
        EntityEntryMoved => "نقل قيد",
        EntityEntriesMovedAll => "نقل جميع قيود",
        EntityRegistryEntryReview => "مراجعة قيد (متقاعد)",
        EntityEditProposalReview => "اقتراح تعديل (متقاعد)",
        ManualToDocument => "تنبيه على ملف",
        ManualToLawyer => "رسالة خاصة",
        ManualToBranch => "تعميم للفرع",
        ManualToHead => "توجيه لرئيس (محجوز)",
        EntityRenameLawyers => "تعميم: إعادة تسمية جهة",
        EntityRenameHeads => "تعميم رؤساء: إعادة تسمية جهة",
        EntityMergeLawyers => "تعميم: دمج جهات",
        EntityMergeHeads => "تعميم رؤساء: دمج جهات",
        EntityUnifyLawyers => "تعميم: توحيد تسمية جهة",
        EntityUnifyHeads => "تعميم رؤساء: توحيد تسمية جهة",
        EntityAbolishLawyers => "تعميم: حل جهة محل أخرى",
        EntityAbolishHeads => "تعميم رؤساء: حل جهة محل أخرى",
        Legacy => "تنبيه سابق",
        _ => kind,
    };
}
