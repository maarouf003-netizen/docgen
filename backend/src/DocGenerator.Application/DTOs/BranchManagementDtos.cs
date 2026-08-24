namespace DocGenerator.Application.DTOs;

/// <summary>إنشاء فرع جديد — مشرف النظام فقط. Governorate تحدد محافظة الفرع لنطاق رئيس القسم في سجل الجهات (د5).</summary>
public record CreateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone,
    string? Governorate = null);

/// <summary>تحديث فرع — مشرف النظام فقط (IsActive لتفعيل/تعطيل الفرع بدل الحذف عند الاستخدام).</summary>
public record UpdateBranchRequest(
    string Name,
    string Code,
    string? Address,
    string? Phone,
    bool IsActive,
    string? Governorate = null);
