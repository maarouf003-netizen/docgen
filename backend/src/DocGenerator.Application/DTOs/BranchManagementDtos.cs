namespace DocGenerator.Application.DTOs;

/// <summary>إنشاء فرع جديد — مشرف النظام والمدير (قرار §2.15). Governorate إجبارية وتحدد محافظة الفرع لنطاق رئيس القسم في سجل الجهات (د5).</summary>
public record CreateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone,
    string? Governorate = null);

/// <summary>تحديث فرع — مشرف النظام والمدير (قرار §2.15) (IsActive لتفعيل/تعطيل الفرع بدل الحذف عند الاستخدام).</summary>
public record UpdateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone,
    bool IsActive,
    string? Governorate = null);
