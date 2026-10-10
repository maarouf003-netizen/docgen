using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Authorization;

/// <summary>
/// مصفوفة الصلاحيات المركزية: كل تحقق من الصلاحيات يمر عبر هذا الكتالوج
/// حتى لا تتفرق القواعد بين المتحكمات، ويكون أي تعديل لاحق في مكان واحد.
/// </summary>
/// <remarks>
/// رئيس الشعبة (`SubHead`) يرث صلاحيات رئيس القسم الخمس عشرة بنطاق شعبته
/// (قرار §2.1) عبر <see cref="IsHeadOrSubHead"/> — والمجموعة السلبية (لا يرثها
/// أبدًا): `HasFullAccess`، `CanManageUsers`، `CanManageBranches`،
/// `CanSeeAdministrativeBranch`، `IsReadOnlyOnDocuments`.
/// (`CanManageBranches` للمدير توسعة مقصودة بقرار §2.15 — لا علاقة لها بالشعبة).
/// </remarks>
public static class RolePermissions
{
    /// <summary>
    /// مساعد التفويض المركزي (قرار §2.21): رئيس قسم أو رئيس شعبة — يُستبدل به
    /// كل فحص `Role == UserRole.Head` المشتق للنطاق (لا تعديل يدوي متناثر).
    /// </summary>
    public static bool IsHeadOrSubHead(UserRole role) =>
        role is UserRole.Head or UserRole.SubHead;
    /// <summary>إدخال/تعديل مستند جديد — المحامي فقط (للملفات التي يملكها).</summary>
    public static bool CanEditDocuments(UserRole role) => role == UserRole.Lawyer;

    /// <summary>تغيير/إلغاء حالة مستند — المحامي فقط.</summary>
    public static bool CanChangeDocumentStatus(UserRole role) => role == UserRole.Lawyer;

    /// <summary>حذف/استعادة مستند منطقياً — المحامي فقط لملفاته.</summary>
    public static bool CanDeleteDocuments(UserRole role) => role == UserRole.Lawyer;

    /// <summary>
    /// رؤية قائمة المستندات المحذوفة —
    /// محامٍ (ملفاته) / رئيس قسم وشعبة (نطاقه) / مدير ومشرف (الكل) — `BQ-001`.
    /// </summary>
    public static bool CanViewDeletedDocuments(UserRole role) =>
        role is UserRole.Lawyer || IsHeadOrSubHead(role) || role is UserRole.Manager or UserRole.Admin;

    /// <summary>إضافة/تعديل/حذف إجراءات التنفيذ وإلغاء التذكير — المحامي فقط.</summary>
    public static bool CanManageExecutionActions(UserRole role) => role == UserRole.Lawyer;

    /// <summary>تدوير أرقام أساس الملفات السنوي — المحامي فقط (على ملفاته).</summary>
    public static bool CanRotate(UserRole role) => role == UserRole.Lawyer;

    /// <summary>رؤية عدادات المشاهدة/الطباعة — رئيس قسم وشعبة (نطاقه)/مدير/مشرف.</summary>
    public static bool CanViewCounters(UserRole role) =>
        IsHeadOrSubHead(role) || role is UserRole.Manager or UserRole.Admin;

    /// <summary>وصول عام لكل الفروع (قراءة) — مدير/مشرف.</summary>
    public static bool HasFullAccess(UserRole role) => role is UserRole.Manager or UserRole.Admin;

    /// <summary>نقل ملفات بين المحامين — رئيس القسم والشعبة (نطاقه) فقط.</summary>
    public static bool CanTransferDocuments(UserRole role) => IsHeadOrSubHead(role);

    /// <summary>إدارة محامي الفرع (إضافة/تعطيل) — رئيس القسم والشعبة ومشرف.</summary>
    public static bool CanManageBranchLawyers(UserRole role) => IsHeadOrSubHead(role) || role == UserRole.Admin;

    /// <summary>إدارة المستخدمين — مشرف (الكل) ومدير (كل الأدوار عدا المشرف — `BQ-001د`؛ حد دور المشرف يُفرَض في الخدمة).</summary>
    public static bool CanManageUsers(UserRole role) => role is UserRole.Manager or UserRole.Admin;

    /// <summary>إدارة الفروع والشعب (إضافة/تعديل/حذف) — المشرف والمدير (قرار §2.15: توسيع مقصود).</summary>
    public static bool CanManageBranches(UserRole role) => role is UserRole.Manager or UserRole.Admin;

    /// <summary>إصدار تنبيهات للمحامين — رئيس القسم والشعبة (نطاقه) فقط.</summary>
    public static bool CanCreateAlerts(UserRole role) => IsHeadOrSubHead(role);

    /// <summary>
    /// تسطير/تعديل/حذف الإنابات على ملف يملكه المحامي (الملف المنيب) — المحامي فقط،
    /// وقبل اعتماد رئيس القسم.
    /// </summary>
    public static bool CanManageDelegations(UserRole role) => role == UserRole.Lawyer;

    /// <summary>
    /// اعتماد الإنابات واختيار المحامي المختص (نافذة «طلبات الإنابة») — رئيس
    /// القسم والشعبة لنطاقه فقط.
    /// </summary>
    public static bool CanApproveDelegations(UserRole role) => IsHeadOrSubHead(role);

    /// <summary>
    /// تسطير الاستئنافات على ملفات المحامي وإدخال إجراءاتها وتغيير حالتها
    /// (حسم/شطب) وتدوير رقم أساسها — المحامي فقط.
    /// </summary>
    public static bool CanManageAppeals(UserRole role) => role == UserRole.Lawyer;

    /// <summary>
    /// إسناد الاستئنافات إلى محامي الفرع ونقلها بينهم — رئيس القسم والشعبة لنطاقه فقط.
    /// </summary>
    public static bool CanAssignAppeals(UserRole role) => IsHeadOrSubHead(role);

    /// <summary>رؤية عمود «فرع الإدارة» — مدير/مشرف فقط.</summary>
    public static bool CanSeeAdministrativeBranch(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin;

    /// <summary>تسطير كتب المطالعة وإضافة اللاحقات — المحامي فقط.</summary>
    public static bool CanCreateReviewLetters(UserRole role) => role == UserRole.Lawyer;

    /// <summary>الرد على كتب المطالعة — رئيس القسم والشعبة لنطاقه فقط.</summary>
    public static bool CanReplyReviewLetters(UserRole role) => IsHeadOrSubHead(role);

    /// <summary>
    /// تسطير المراسلات واللاحقات والردود — محامٍ/رئيس قسم وشعبة/مندوب جهة
    /// (كتابة المندوب حصرًا عبر مسارات البوابة المخصصة).
    /// </summary>
    public static bool CanCreateCorrespondences(UserRole role) =>
        role is UserRole.Lawyer || IsHeadOrSubHead(role) || role == UserRole.EntityManager;

    /// <summary>رؤية عمود «المحامي المختص» — رئيس قسم وشعبة/مدير/مشرف.</summary>
    public static bool CanSeeAssignedLawyer(UserRole role) =>
        IsHeadOrSubHead(role) || role is UserRole.Manager or UserRole.Admin;

    /// <summary>البحث/الفلترة باسم المحامي — رئيس قسم وشعبة/مدير/مشرف.</summary>
    public static bool CanSearchByLawyer(UserRole role) =>
        IsHeadOrSubHead(role) || role is UserRole.Manager or UserRole.Admin;

    /// <summary>قراءة مطلقة على الملفات (بلا إدخال/تعديل/حالة) — مدير/مشرف.</summary>
    public static bool IsReadOnlyOnDocuments(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin;

    /// <summary>
    /// إدارة سجل الجهات العامة (إنشاء/تعديل/أسماء بديلة/استيراد) —
    /// مدير/مشرف على كل السجل، ورئيس القسم والشعبة مقصورًا على محافظة فرعه عند التنفيذ (د3/د5).
    /// </summary>
    public static bool CanManageEntityRegistry(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin || IsHeadOrSubHead(role);

    /// <summary>بوابة مندوب الجهة العامة: قراءة + تصدير إكسل + مراسلات المندوب كطرف (الاستثناء الكتابي الوحيد).</summary>
    public static bool CanUseDelegatePortal(UserRole role) => role == UserRole.EntityManager;

    /// <summary>إضافة/تعديل حسابات مندوبي الجهات وربط نطاقهم — مدير/مشرف/رئيس قسم وشعبة (د11).</summary>
    public static bool CanManageDelegates(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin || IsHeadOrSubHead(role);

    /// <summary>دمج جهات عامة متعددة في هوية واحدة — مدير/مشرف فقط (د5 §4).</summary>
    public static bool CanMergeEntities(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin;

    /// <summary>إدارة التذكيرات الشخصية الحرة (بلا ملف) — المحامي لملكه فقط.</summary>
    public static bool CanManagePersonalReminders(UserRole role) => role == UserRole.Lawyer;

    /// <summary>
    /// قراءة كل اقتراحات التطوير وتعليمها مقروءة — مشرف ومدير (`BQ-001`).
    /// </summary>
    public static bool CanViewAppSuggestions(UserRole role) => role is UserRole.Manager or UserRole.Admin;

    /// <summary>إرسال اقتراح تطوير — المحامي ورئيس القسم والشعبة (صندوق المشرف).</summary>
    public static bool CanSuggestApp(UserRole role) =>
        role == UserRole.Lawyer || IsHeadOrSubHead(role);

    /// <summary>
    /// إدارة سجل دوائر التنفيذ (إدخال/تسمية/تعطيل/حذف-إفراغ/إحالة) — رئيس القسم
    /// والشعبة لنطاقه فقط.
    /// </summary>
    public static bool CanManageExecutionCircuits(UserRole role) => IsHeadOrSubHead(role);
}
