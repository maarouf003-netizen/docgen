namespace DocGenerator.Application.DTOs;

/// <summary>نطاق مندوب الجهة كما يُسمح له برؤيته في البوابة.</summary>
public record PortalScopeDto(
    /// <summary>نوع النطاق: group (هوية أم بكل قيودها) أو entry (قيد بعينه).</summary>
    string ScopeType,
    int GroupId,
    string CanonicalName,
    string EntityType,
    IReadOnlyList<PortalScopeEntryDto> Entries);

/// <summary>قيد ضمن نطاق المندوب مع حالته (الانتظار لا يُدرج أصلًا في نطاق الرؤية).</summary>
public record PortalScopeEntryDto(
    int Id,
    string Governorate,
    string BranchName,
    bool IsActive);

/// <summary>
/// ملف في قائمة البوابة — قراءة فقط، بنفس غنى قائمة الملفات دون أي حقول داخلية
/// للمحامين (عدّادات/محامي مختص/فرع إدارة).
/// </summary>
public record PortalFileListItemDto(
    int Id,
    string DocumentType,
    bool IsDraft,
    string? BorrowerName,
    string? Applicant,
    string ExecutedEntitiesSummary,
    decimal AmountNumeric,
    string? Currency,
    string? ExecStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>استئناف قرائي على بطاقة استئنافات البوابة.</summary>
public record PortalAppealDto(
    int Id,
    string Direction,
    string Status,
    string? AppealTypeLabel,
    string? AppealBaseNumber,
    string? AppealYear,
    DateTime CreatedAt,
    DateTime? DecisionDate,
    string? DecisionRuling);

/// <summary>إنشاء حساب مندوب جهة مربوط بنطاقه — مدير/مشرف/رئيس قسم (د11).</summary>
public record CreateDelegateRequest(
    string Username,
    string FullName,
    string Password,
    int? PortalGroupId,
    int? PortalEntryId);

/// <summary>تعديل حساب مندوب: أي حقل يُترك null يبقى كما هو؛ كلمة المرور اختيارية لإعادة الضبط.</summary>
public record UpdateDelegateRequest(
    string? FullName,
    bool? IsActive,
    string? NewPassword,
    int? PortalGroupId,
    int? PortalEntryId);

/// <summary>حساب مندوب كما يظهر في شاشة الإدارة.</summary>
public record DelegateDto(
    int Id,
    string Username,
    string FullName,
    bool IsActive,
    int? PortalGroupId,
    string? PortalGroupName,
    int? PortalEntryId,
    string? PortalEntryLabel,
    DateTime CreatedAt);
