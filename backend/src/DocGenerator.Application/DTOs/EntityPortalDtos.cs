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

/* ── إحصاءات الجهة (المرحلة 4) ── */

/// <summary>
/// إحصاءات قرائية لنطاق مندوب الجهة: تصنيف الحالة يطابق فلاتر القائمة
/// (منفذ/تريث/تحت رفع/متداول)، والمشطوب مستبعد دائمًا كما في القائمة.
/// </summary>
public record PortalStatsDto(
    int TotalFiles,
    int DraftFiles,
    int CirculatingFiles,
    int ExecutedFiles,
    int DeferredFiles,
    int PendingAppeals,
    int ClosedAppeals,
    /// <summary>آخر 12 شهرًا متصلة حتى الشهر الحالي (UTC) شاملة الأشهر الصفرية.</summary>
    IReadOnlyList<PortalMonthlyCountDto> Monthly,
    /// <summary>توزيع الارتباط على قيود النطاق؛ قد يُحتسب الملف تحت أكثر من قيد.</summary>
    IReadOnlyList<PortalEntryStatDto> PerEntry,
    /// <summary>أعلى العملات بعدد الملفات مع مجموع مبالغها ضمن العملة نفسها.</summary>
    IReadOnlyList<PortalCurrencyStatDto> TopCurrencies);

/// <summary>عدد ملفات شهر محدد في السلسلة الشهرية.</summary>
public record PortalMonthlyCountDto(int Year, int Month, int Files);

/// <summary>عدد الملفات المرتبطة بقيد بعينه من قيود النطاق.</summary>
public record PortalEntryStatDto(
    int EntryId,
    string Governorate,
    string BranchName,
    int Files);

/// <summary>عملة مجمّعة: عدد الملفات ومجموع مبالغها بالعملة نفسها فقط.</summary>
public record PortalCurrencyStatDto(string Currency, int Files, decimal TotalAmount);
