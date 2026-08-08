using DocGenerator.Domain.Enums;

namespace DocGenerator.Api.Authorization;

/// <summary>
/// مصفوفة الصلاحيات المركزية: كل تحقق من الصلاحيات يمر عبر هذا الكتالوج
/// حتى لا تتفرق القواعد بين المتحكمات، ويكون أي تعديل لاحق في مكان واحد.
/// </summary>
public static class RolePermissions
{
    /// <summary>إدخال/تعديل مستند جديد — المحامي فقط (للملفات التي يملكها).</summary>
    public static bool CanEditDocuments(UserRole role) => role == UserRole.Lawyer;

    /// <summary>تغيير/إلغاء حالة مستند — المحامي فقط.</summary>
    public static bool CanChangeDocumentStatus(UserRole role) => role == UserRole.Lawyer;

    /// <summary>حذف/استعادة مستند منطقياً — المحامي فقط لملفاته.</summary>
    public static bool CanDeleteDocuments(UserRole role) => role == UserRole.Lawyer;

    /// <summary>
    /// رؤية قائمة المستندات المحذوفة —
    /// محامٍ (ملفاته) / رئيس قسم (فرعه) / مشرف (الكل)، والمدير لا يراها.
    /// </summary>
    public static bool CanViewDeletedDocuments(UserRole role) =>
        role is UserRole.Lawyer or UserRole.Head or UserRole.Admin;

    /// <summary>إضافة/تعديل/حذف إجراءات التنفيذ وإلغاء التذكير — المحامي فقط.</summary>
    public static bool CanManageExecutionActions(UserRole role) => role == UserRole.Lawyer;

    /// <summary>تدوير أرقام أساس الملفات السنوي — المحامي فقط (على ملفاته).</summary>
    public static bool CanRotate(UserRole role) => role == UserRole.Lawyer;

    /// <summary>رؤية عدادات المشاهدة/الطباعة — رئيس قسم/مدير/مشرف.</summary>
    public static bool CanViewCounters(UserRole role) =>
        role is UserRole.Head or UserRole.Manager or UserRole.Admin;

    /// <summary>وصول عام لكل الفروع (قراءة) — مدير/مشرف.</summary>
    public static bool HasFullAccess(UserRole role) => role is UserRole.Manager or UserRole.Admin;

    /// <summary>نقل ملفات بين المحامين — رئيس القسم (فرعه) فقط.</summary>
    public static bool CanTransferDocuments(UserRole role) => role == UserRole.Head;

    /// <summary>إدارة محامي الفرع (إضافة/تعطيل) — رئيس القسم ومشرف.</summary>
    public static bool CanManageBranchLawyers(UserRole role) => role is UserRole.Head or UserRole.Admin;

    /// <summary>إدارة المستخدمين بكاملها — المشرف فقط.</summary>
    public static bool CanManageUsers(UserRole role) => role == UserRole.Admin;

    /// <summary>إدارة الفروع (إضافة/تعديل/حذف) — المشرف فقط.</summary>
    public static bool CanManageBranches(UserRole role) => role == UserRole.Admin;

    /// <summary>إصدار تنبيهات للمحامين — رئيس القسم (فرعه) فقط.</summary>
    public static bool CanCreateAlerts(UserRole role) => role == UserRole.Head;

    /// <summary>رؤية عمود «فرع الإدارة» — مدير/مشرف فقط.</summary>
    public static bool CanSeeAdministrativeBranch(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin;

    /// <summary>رؤية عمود «المحامي المختص» — رئيس قسم/مدير/مشرف.</summary>
    public static bool CanSeeAssignedLawyer(UserRole role) =>
        role is UserRole.Head or UserRole.Manager or UserRole.Admin;

    /// <summary>البحث/الفلترة باسم المحامي — رئيس قسم/مدير/مشرف.</summary>
    public static bool CanSearchByLawyer(UserRole role) =>
        role is UserRole.Head or UserRole.Manager or UserRole.Admin;

    /// <summary>قراءة مطلقة على الملفات (بلا إدخال/تعديل/حالة) — مدير/مشرف.</summary>
    public static bool IsReadOnlyOnDocuments(UserRole role) =>
        role is UserRole.Manager or UserRole.Admin;
}
