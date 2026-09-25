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
/// الحقول الجديدة اختيارية بقيَم افتراضية حفاظًا على توافق البناء الموضعي القائم.
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
    DateTime UpdatedAt,
    /// <summary>مكوّنا الاسم الثلاثي للمنفذ عليه (الأب والعائلة) — يُركَّبان مع BorrowerName في العرض.</summary>
    string? BorrowerFather = null,
    /// <summary>العائلة من الاسم الثلاثي للمنفذ عليه.</summary>
    string? BorrowerFamily = null,
    /// <summary>نوع الملف (FileType: سند مصارف، تأمين، …) — نص حر كما أدخله المحامي.</summary>
    string? FileType = null,
    /// <summary>دائرة التنفيذ المختصة (Document.Court) — تُخفى عرضيًا عند فراغها.</summary>
    string? Court = null,
    /// <summary>أحدث رقم أساس دائمًا (غير مقيّد بسنة الفحص) وإلا رقم الملف الأصلي.</summary>
    string? DisplayBaseNumber = null,
    /// <summary>سنة رقم الأساس المعروض وإلا سنة قيد الملف الأصلية.</summary>
    string? DisplayBaseYear = null,
    /// <summary>قيود النطاق المطابقة لهذا الملف (للسطر الثاني «فرع الجهة العامة»).</summary>
    IReadOnlyList<PortalScopeEntryDto>? MatchedEntries = null,
    /// <summary>
    /// حالة العرض من المصدر الوحيد (`DocumentStatusResolver`) — تشتق منها الواجهة
    /// الشارة مباشرة بلا إعادة تصنيف (الخام `ExecStatus` للفلاتر فقط).
    /// </summary>
    string? DisplayStatus = null);

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

/// <summary>إجراء تنفيذي قرائي على بطاقة «الإجراءات التنفيذية» (النوع action فقط — بلا تذكير).</summary>
public record PortalExecutionActionDto(
    int Id,
    string Text,
    string? ActionDate,
    string? CreatedByName,
    DateTime CreatedAt);

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
/// إحصاءات قرائية لنطاق مندوب الجهة: تصنيف الحالة يطابق فلاتر القائمة حرفيًا
/// (منفذ/تريث/محال الى البداية/تحت رفع/متداول — بما فيها طيّ الإرثي والجزئي في
/// المتداول)، والمشطوب مستبعد دائمًا كما في القائمة.
/// أساس المبالغ (قرار المالك): أزواج مبالغ الدين الستة كما سُجّلت (المبلغ×3 +
/// مبلغ الإدراج×3 بعملاتها) بعد إسقاط الصفري — لا خلط بين العملات إطلاقًا.
/// ملاحظة مقياس: هنا مبالغ الدين، بينما ب6/لوحة المدير تجمع المحصّل — الفرق
/// موثّق قصدًا لا تطابقًا مُدَّعى.
/// </summary>
public record PortalStatsDto(
    int TotalFiles,
    int DraftFiles,
    int CirculatingFiles,
    int ExecutedFiles,
    int DeferredFiles,
    /// <summary>عدد ملفات «محال الى البداية» في النطاق (فلتر مستقل، ولا يشملها فلتر «منفذ»).</summary>
    int ReferredToStartFiles,
    int PendingAppeals,
    int ClosedAppeals,
    /// <summary>آخر 12 شهرًا متصلة حتى الشهر الحالي (UTC) شاملة الأشهر الصفرية.</summary>
    IReadOnlyList<PortalMonthlyCountDto> Monthly,
    /// <summary>توزيع الارتباط على قيود النطاق؛ قد يُحتسب الملف تحت أكثر من قيد.</summary>
    IReadOnlyList<PortalEntryStatDto> PerEntry,
    /// <summary>أعلى العملات بعدد الملفات مع مجموع مبالغها ضمن العملة نفسها (مهمل عرضيًا — يُخفى من الواجهة).</summary>
    IReadOnlyList<PortalCurrencyStatDto> TopCurrencies,
    /// <summary>إجمالي المبالغ لكل النطاق (أو القيد المختار) مكسّرًا حسب العملة.</summary>
    IReadOnlyList<PortalCurrencyStatDto>? AmountTotals = null,
    /// <summary>المبالغ لكل سلّة حالة (متداول/تريث/منفذ/محال/تحت رفع) مكسّرة حسب العملة.</summary>
    IReadOnlyList<PortalStatusAmountDto>? AmountByStatus = null);

/// <summary>عدد ملفات شهر محدد في السلسلة الشهرية.</summary>
public record PortalMonthlyCountDto(int Year, int Month, int Files);

/// <summary>عدد الملفات المرتبطة بقيد بعينه من قيود النطاق.</summary>
public record PortalEntryStatDto(
    int EntryId,
    string Governorate,
    string BranchName,
    int Files);

/// <summary>
/// عملة مجمّعة: عدد الملفات **المتميزة** الحاملة لمبلغ غير صفري بهذه العملة
/// ومجموع مبالغها فيها فقط — ليس عدّاد السلّة (الصفري والإنابة عددًا خارج المجاميع).
/// </summary>
public record PortalCurrencyStatDto(string Currency, int Files, decimal TotalAmount);

/// <summary>
/// مبالغ سلّة حالة واحدة (تسمية السلّة كما في فلاتر القائمة) مكسّرة حسب العملة.
/// `Files` = عدّاد السلّة الكامل (مطابق رقم الفلتر)؛ بينما `Totals[].Files` =
/// الحاملون لمبالغ بهذه العملة فقط — الفرق مقصود (إنابة/صفرية عددًا دون مبالغ).
/// </summary>
public record PortalStatusAmountDto(
    string Status,
    int Files,
    IReadOnlyList<PortalCurrencyStatDto> Totals);
