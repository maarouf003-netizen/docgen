namespace DocGenerator.Application.DTOs;

/// <summary>محامٍ ضمن فرع لإدارة محامي الفرع ونقل الملفات.</summary>
public record LawyerListItemDto(
    int Id,
    string Username,
    string FullName,
    bool IsActive,
    int? BranchId,
    string? BranchName,
    int? CreatedById = null);

/// <summary>إضافة محامٍ إلى فرع — رئيس القسم لمحامي فرعه، والمشرف لأي فرع (يحدد BranchId).</summary>
public record CreateLawyerRequest(
    string Username,
    string FullName,
    string Password,
    int? BranchId = null);

/// <summary>تعديل محامٍ في فرع — رئيس القسم لمحامي فرعه، والمشرف لأي فرع.
/// الاسم اختياري (يحدّث اسم الدخول تلقائياً)، وكلمة المرور اختيارية تُترك فارغة للإبقاء.</summary>
public record UpdateLawyerRequest(
    string? FullName,
    string? Password = null);

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
    bool IsActive,
    int? SectionId = null,
    string? SectionName = null);

/// <summary>إنشاء مستخدم — مشرف (أي دور) ومدير (عدا دور المشرف — `BQ-001د`). `SectionId` إلزامي لرئيس الشعبة فقط.</summary>
public record CreateUserRequest(
    string Username,
    string FullName,
    string Role,
    int? BranchId,
    string Password,
    int? SectionId = null);

/// <summary>تحديث مستخدم — مشرف (الكل) ومدير (عدا حسابات المشرف — `BQ-001د`)؛ كلمة المرور اختيارية لإعادة التعيين. `SectionId` لرئيس الشعبة فقط (نقل الرئيس بين الشعب). `SuccessorId` إجباري عند تعطيل رئيس (خلف يحل محله — قرار §2.18).</summary>
public record UpdateUserRequest(
    string? FullName,
    string? Role,
    int? BranchId,
    bool IsActive,
    string? Password,
    int? SectionId = null,
    int? SuccessorId = null);

/// <summary>صف سجل تعاقب رئاسة — للمدير والمشرف فقط (قرار §2.17).</summary>
public record HeadSuccessionDto(
    int Id,
    int BranchId,
    string? BranchName,
    int? SectionId,
    string? SectionName,
    int UserId,
    string? UserName,
    string Role,
    string Event,
    DateTime At,
    string? ActorName,
    string? Reason);

/// <summary>نقل ملف إلى محامٍ آخر — رئيس القسم (ضمن فرعه).</summary>
public record TransferDocumentRequest(int TargetLawyerId);

/// <summary>نقل كامل ملفات محامٍ إلى محامٍ آخر بجميع الحالات — رئيس القسم (ضمن فرعه).</summary>
public record TransferAllRequest(int SourceLawyerId, int TargetLawyerId);
