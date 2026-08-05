namespace DocGenerator.Application.DTOs;

/// <summary>محامٍ ضمن فرع لإدارة محامي الفرع ونقل الملفات.</summary>
public record LawyerListItemDto(
    int Id,
    string Username,
    string FullName,
    bool IsActive,
    int? BranchId,
    string? BranchName);

/// <summary>إضافة محامٍ إلى فرع — رئيس القسم لمحامي فرعه، والمشرف لأي فرع (يحدد BranchId).</summary>
public record CreateLawyerRequest(
    string Username,
    string FullName,
    string Password,
    int? BranchId = null);

/// <summary>تفعيل/إيقاف حساب (تُستخدم لإيقاف/إعادة تفعيل المحامي أو أي مستخدم).</summary>
public record SetUserActiveRequest(bool IsActive);

/// <summary>مستخدم كامل لعرض/إدارة المشرف على كل الحسابات.</summary>
public record UserListItemDto(
    int Id,
    string Username,
    string FullName,
    string Role,
    int? BranchId,
    string? BranchName,
    bool IsActive);

/// <summary>إنشاء مستخدم بأي دور — المشرف فقط.</summary>
public record CreateUserRequest(
    string Username,
    string FullName,
    string Role,
    int? BranchId,
    string Password);

/// <summary>تحديث مستخدم — المشرف فقط (كلمة المرور اختيارية لإعادة التعيين).</summary>
public record UpdateUserRequest(
    string? FullName,
    string? Role,
    int? BranchId,
    bool IsActive,
    string? Password);

/// <summary>نقل ملف إلى محامٍ آخر — رئيس القسم (ضمن فرعه).</summary>
public record TransferDocumentRequest(int TargetLawyerId);
