namespace DocGenerator.Application.DTOs;

/// <summary>إنشاء فرع جديد — مشرف النظام فقط.</summary>
public record CreateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone);

/// <summary>تحديث فرع — مشرف النظام فقط (IsActive لتفعيل/تعطيل الفرع بدل الحذف عند الاستخدام).</summary>
public record UpdateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone,
    bool IsActive);
