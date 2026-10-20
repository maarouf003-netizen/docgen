using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة مركز نصوص التنبيهات (الخطة §7.1/§7.2 و§14.1): القوالب الحرفية،
/// والبادئة حسب الدور، وغياب القصّ، وتصحيحات الهمزات.
/// </summary>
public class HeadAlertMessageTests
{
    [Fact]
    public void DelegationPendingApproval_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationPendingApproval(
            new HeadAlertMessages.DelegationPendingApprovalContext("أحمد علي", "ملف 12", "دائرة دمشق"));

        Assert.Equal("بانتظار اعتماد الإنابة — سطّر المحامي أحمد علي إنابة على الملف (ملف 12) إلى دائرة دائرة دمشق", text);
    }

    [Fact]
    public void DelegationSentToSubhead_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationSentToSubhead(
            new HeadAlertMessages.DelegationSentToSubheadContext(7, "ملف 12"));

        Assert.Equal("الإنابة من اختصاصكم — يرجى التفضل بالاطلاع والإسناد (إنابة رقم 7 على ملف ملف 12)", text);
    }

    [Fact]
    public void DelegationAssignedToLawyer_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationAssignedToLawyer(
            new HeadAlertMessages.DelegationAssignedToLawyerContext("دائرة حلب", "ملف 12"));

        Assert.Equal("أحال إليك رئيس القسم ملف إنابة لقيده أصولًا في دائرة حلب (ملف ملف 12)", text);
    }

    [Fact]
    public void DelegationRejectedWrongCourt_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationRejectedWrongCourt(
            new HeadAlertMessages.DelegationRejectedWrongCourtContext(9, "الدائرة غير مختصة"));

        Assert.Equal("رُفضت الإنابة رقم 9 للدائرة الخطأ — الدائرة غير مختصة — صحّح الدائرة من بطاقة تشعبات الملف", text);
    }

    [Fact]
    public void DelegationPendingCompletion_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationPendingCompletion(
            new HeadAlertMessages.DelegationPendingCompletionContext(3, "ملف 12"));

        Assert.Equal("بانتظار إتمام الإنابة رقم 3 — ملف ملف 12", text);
    }

    [Fact]
    public void DelegationCompletedReturned_UsesApprovedWording()
    {
        var text = HeadAlertMessages.DelegationCompletedReturned(
            new HeadAlertMessages.DelegationCompletedReturnedContext(
                "دائرة دمشق", "ملف 12", "غطى كامل المديونية", "ملف 13", "عقار 1"));

        Assert.Equal(
            "أُعيدت الإنابة المسطرة إلى دائرة دائرة دمشق في ملف ملف 12 — البدل: غطى كامل المديونية — المناب: ملف 13 — الأصول: عقار 1. يرجى تحديث حالة الملف.",
            text);
    }

    [Fact]
    public void MirrorMessages_CarryApprovedSpelling()
    {
        var deferred = HeadAlertMessages.DelegationMirrorDeferred(
            new HeadAlertMessages.DelegationMirrorDeferredContext("أحمد علي", "12", "دائرة دمشق"));
        Assert.Equal(
            "ورد كتاب تريث بملف الإنابة للمنفذ عليه أحمد علي رقم أساس 12 دائرة تنفيذ دائرة دمشق، يرجى وقف إجراءات الإنابة",
            deferred);

        var returned = HeadAlertMessages.DelegationMirrorReturned(
            new HeadAlertMessages.DelegationMirrorReturnedContext("أحمد علي", "12", "دائرة دمشق"));
        Assert.Equal(
            "يرجى متابعة السير بملف المنفذ عليه أحمد علي رقم أساس 12 دائرة تنفيذ دائرة دمشق تبعًا لكتاب الجهة العامة للسير بالملف وانتهاء حالة التريث",
            returned);

        var settled = HeadAlertMessages.DelegationMirrorSettled(
            new HeadAlertMessages.DelegationMirrorSettledContext("أحمد علي", "12", "دائرة دمشق"));
        Assert.Equal(
            "يرجى وقف إجراءات الإنابة في ملف المنفذ عليه أحمد علي رقم أساس 12 دائرة تنفيذ دائرة دمشق، تبعًا لاعتبار الملف المنيب منفذًا بالتسوية، وإعادته لمرجعه الأصلي",
            settled);

        var forcibly = HeadAlertMessages.DelegationMirrorFullyForcibly(
            new HeadAlertMessages.DelegationMirrorFullyForciblyContext("12", "13"));
        Assert.Equal(
            "استُرد ملفك المناب رقم أساس 12 لاعتبار الملف المنيب 13 منفذًا — بتحصيل المبلغ المطالب به جبريًا في الملف المنيب",
            forcibly);
    }

    [Fact]
    public void MirrorMessages_UseArabicHamzaForms()
    {
        var deferred = HeadAlertMessages.DelegationMirrorDeferred(
            new HeadAlertMessages.DelegationMirrorDeferredContext("أ", "1", "د"));
        var returned = HeadAlertMessages.DelegationMirrorReturned(
            new HeadAlertMessages.DelegationMirrorReturnedContext("أ", "1", "د"));
        var settled = HeadAlertMessages.DelegationMirrorSettled(
            new HeadAlertMessages.DelegationMirrorSettledContext("أ", "1", "د"));
        var forcibly = HeadAlertMessages.DelegationMirrorFullyForcibly(
            new HeadAlertMessages.DelegationMirrorFullyForciblyContext("1", "2"));

        // المقارنة الترتيبية (Ordinal) إلزامية هنا: المقارنة اللغوية العربية considered
        // «إ» و«ا» متكافئتين، فتفقد الهمزة في الفحص.
        Assert.Contains("إجراءات", deferred, StringComparison.Ordinal);
        Assert.DoesNotContain("اجراءات", deferred, StringComparison.Ordinal);
        Assert.Contains("إجراءات", settled, StringComparison.Ordinal);
        Assert.DoesNotContain("اجراءات", settled, StringComparison.Ordinal);

        Assert.Contains("تبعًا", returned, StringComparison.Ordinal);
        Assert.DoesNotContain("تبعاً", returned, StringComparison.Ordinal);
        Assert.Contains("تبعًا", settled, StringComparison.Ordinal);
        Assert.DoesNotContain("تبعاً", settled, StringComparison.Ordinal);

        Assert.Contains("وإعادته لمرجعه الأصلي", settled, StringComparison.Ordinal);
        Assert.Contains("استُرد", forcibly, StringComparison.Ordinal);
    }

    [Fact]
    public void MirrorUpdated_LocalAddition_StatusChanged_UseApprovedWording()
    {
        Assert.Equal(
            "تم تحديث نسخة الملف المناب لمواكبة الملف المنيب: أصول",
            HeadAlertMessages.DelegationMirrorUpdated(new HeadAlertMessages.DelegationMirrorUpdatedContext("أصول")));

        Assert.Equal(
            "تم تعديل بيانات الملف المناب بإضافة محلية — أصل",
            HeadAlertMessages.DelegationMirrorLocalAddition(new HeadAlertMessages.DelegationMirrorLocalAdditionContext("أصل")));

        Assert.Equal(
            "تغيّرت حالة الملف المنيب إلى «منفّذ»",
            HeadAlertMessages.DelegationMirrorStatusChanged(new HeadAlertMessages.DelegationMirrorStatusChangedContext("منفّذ")));
    }

    [Fact]
    public void CircuitMessages_UseApprovedWording()
    {
        Assert.Equal(
            "أحال لك رئيس القسم ملفات من دائرة دائرة دمشق — يرجى تحديث معلوماتها",
            HeadAlertMessages.CircuitPendingDocuments(new HeadAlertMessages.CircuitPendingDocumentsContext("دائرة دمشق")));

        Assert.Equal(
            "نُقلت إليك دائرة «دائرة دمشق» — 5 ملفًا، 2 إنابة معلقة، 1 استئنافًا بانتظار الإسناد",
            HeadAlertMessages.CircuitTransferred(new HeadAlertMessages.CircuitTransferredContext("دائرة دمشق", 5, 2, 1)));
    }

    [Fact]
    public void AppealMessages_UseApprovedWording()
    {
        Assert.Equal(
            "يرجى التفضل بإسناد الاستئناف (ملف ملف 12 رقم 55 — محال من مكتب)",
            HeadAlertMessages.AppealReferralToHead(new HeadAlertMessages.AppealReferralToHeadContext("ملف 12", "55", "مكتب")));

        Assert.Equal(
            "أُعيد الاستئناف (ملف ملف 12 رقم 55) لنطاقك — يرجى اختيار محامٍ لمتابعته",
            HeadAlertMessages.AppealReturnedToHead(new HeadAlertMessages.AppealReturnedToHeadContext("ملف 12", "55")));

        Assert.Equal(
            "وقع استئناف بملف ملف 12 رقم 55 نوع دين دائرة تنفيذ دائرة دمشق، يرجى اختيار محامٍ لمتابعة الاستئناف",
            HeadAlertMessages.AppealPendingToOwner(new HeadAlertMessages.AppealPendingToOwnerContext("ملف 12", "55", "دين", "دائرة دمشق")));

        Assert.Equal(
            "أحال إليك رئيس القسم استئنافًا لمتابعته أصولًا (ملف ملف 12 رقم 55)",
            HeadAlertMessages.AppealAssignedToLawyer(new HeadAlertMessages.AppealAssignedToLawyerContext("ملف 12", "55")));

        Assert.Equal(
            "أصبح استئناف قرار رئيس التنفيذ في الملف (ملف 12 رقم 55) محسوم",
            HeadAlertMessages.AppealFinalToBaseLawyer(new HeadAlertMessages.AppealFinalToBaseLawyerContext("ملف 12", "55", "محسوم", null)));
    }

    [Fact]
    public void ReviewLetterReply_KeepsFullReplyWithoutTruncation()
    {
        var reply = new string('ا', 500);
        var text = HeadAlertMessages.ReviewLetterReply(
            new HeadAlertMessages.ReviewLetterReplyContext("رئيس القسم", "12", reply));

        Assert.Equal($"رئيس القسم ردّ على كتاب مطالعتك رقم 12: {reply}", text);
        Assert.Contains(reply, text);
    }

    [Fact]
    public void EntityMessages_UseApprovedWording()
    {
        Assert.Equal(
            "تم تعديل اسم الجهة العامة التي أدخلتها من «القديم» إلى «الجديد»",
            HeadAlertMessages.EntityRenameNoticeToCreator(
                new HeadAlertMessages.EntityRenameNoticeToCreatorContext("القديم", "الجديد")));

        Assert.Equal(
            "أُلحق بقيدكم فرع من هيئة أخرى: «هيئة» — دمشق/الفرع",
            HeadAlertMessages.EntityEntryMoved(
                new HeadAlertMessages.EntityEntryMovedContext("هيئة", "دمشق", "الفرع")));

        Assert.Equal(
            "تم نقل جميع قيود «المصدر» (دمشق) إلى «الهدف»",
            HeadAlertMessages.EntityEntriesMovedAll(
                new HeadAlertMessages.EntityEntriesMovedAllContext("المصدر", "دمشق", "الهدف")));
    }

    [Fact]
    public void EntityBranchChanged_HasFourVariants()
    {
        Assert.Equal(
            "تم تغيير اسم فرع جهة «هيئة» (دمشق): من «قديم» إلى «جديد»",
            HeadAlertMessages.EntityBranchRenamed(
                new HeadAlertMessages.EntityBranchRenamedContext("هيئة", "دمشق", "قديم", "جديد")));

        Assert.Equal(
            "تم دمج فرع جهة «هيئة» (دمشق/قديم) في (دمشق/جديد)",
            HeadAlertMessages.EntityBranchMerged(
                new HeadAlertMessages.EntityBranchMergedContext("هيئة", "دمشق", "قديم", "جديد")));

        Assert.Equal(
            "تم إلغاء فرع جهة «هيئة» (دمشق/الفرع) بلا ملفات مرتبطة",
            HeadAlertMessages.EntityBranchAbolished(
                new HeadAlertMessages.EntityBranchAbolishedContext("هيئة", "دمشق", "الفرع")));

        Assert.Equal(
            "تم توحيد تسميات فروع جهة «هيئة» (دمشق/الفرع)",
            HeadAlertMessages.EntityBranchUnified(
                new HeadAlertMessages.EntityBranchUnifiedContext("هيئة", "دمشق", "الفرع")));
    }

    [Fact]
    public void Circulars_DelegateToEntityChangeMessages_WithUnifiedQuotes()
    {
        Assert.Equal(
            "يرجى ملاحظة أنه تم تعديل اسم «القديم» إلى «الجديد»",
            HeadAlertMessages.EntityRenameLawyers("القديم", "الجديد", "", "", null));

        Assert.Equal(
            "تم تعديل اسم الجهة «القديم» إلى «الجديد»",
            HeadAlertMessages.EntityRenameHeads("القديم", "الجديد", "", "", null));

        Assert.Equal(
            "يرجى ملاحظة أنه تم دمج «الممتص» مع «الباقي»",
            HeadAlertMessages.EntityMergeLawyers("الممتص", "الباقي", "", "", null));

        Assert.Equal(
            "تم دمج «الممتص» مع «الباقي»",
            HeadAlertMessages.EntityMergeHeads("الممتص", "الباقي", "", "", null));

        Assert.Equal(
            "يرجى ملاحظة أنه تم توحيد تسمية «الموحَّد» إلى «الأساسي»",
            HeadAlertMessages.EntityUnifyLawyers("الموحَّد", "الأساسي", "", "", null));

        Assert.Equal(
            "تم توحيد تسمية «الموحَّد» إلى «الأساسي»",
            HeadAlertMessages.EntityUnifyHeads("الموحَّد", "الأساسي", "", "", null));

        Assert.Equal(
            "يرجى ملاحظة أنه تم حلول «الجديدة» محل «الملغاة»",
            HeadAlertMessages.EntityAbolishLawyers("الجديدة", "الملغاة", "", "", null));

        Assert.Equal(
            "تم حلول «الجديدة» محل «الملغاة»",
            HeadAlertMessages.EntityAbolishHeads("الجديدة", "الملغاة", "", "", null));
    }

    [Fact]
    public void Circulars_AppendDecreeSuffixWhenPresent()
    {
        var text = HeadAlertMessages.EntityRenameLawyers("القديم", "الجديد", "قرار", "12", null);
        Assert.Equal("يرجى ملاحظة أنه تم تعديل اسم «القديم» إلى «الجديد» بموجب قرار رقم 12", text);
    }

    [Fact]
    public void ManualPrefix_DependsOnRole()
    {
        Assert.Equal("تنبيه من رئيس القسم: ", HeadAlertMessages.ManualPrefix(UserRole.Head));
        Assert.Equal("تنبيه من رئيس الشعبة: ", HeadAlertMessages.ManualPrefix(UserRole.SubHead));
        Assert.Equal(string.Empty, HeadAlertMessages.ManualPrefix(UserRole.Lawyer));
        Assert.Equal(string.Empty, HeadAlertMessages.ManualPrefix(UserRole.Manager));
        Assert.Equal(string.Empty, HeadAlertMessages.ManualPrefix(UserRole.Admin));
    }

    [Fact]
    public void ManualMessage_AppliesPrefixOnce()
    {
        Assert.Equal(
            "تنبيه من رئيس القسم: رسالة",
            HeadAlertMessages.ManualMessage(UserRole.Head, "رسالة"));

        Assert.Equal(
            "تنبيه من رئيس القسم: رسالة",
            HeadAlertMessages.ManualMessage(UserRole.Head, "تنبيه من رئيس القسم: رسالة"));

        Assert.Equal(
            "رسالة",
            HeadAlertMessages.ManualMessage(UserRole.Lawyer, "رسالة"));
    }

    [Fact]
    public void Templates_NeverTruncateLongInput()
    {
        var longCourt = new string('د', 400);
        var text = HeadAlertMessages.CircuitPendingDocuments(
            new HeadAlertMessages.CircuitPendingDocumentsContext(longCourt));

        Assert.Contains(longCourt, text);
        Assert.True(text.Length > 400);
    }
}
