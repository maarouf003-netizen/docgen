namespace DocGenerator.Application.DTOs;

/// <summary>
/// فرع للإدارة والعرض: مع الهاتف والمحافظة وحالة التفعيل وأعداد الاستخدام
/// (المستخدمون/المستندات المرتبطة) لاتخاذ قرار الحذف/التعطيل بصورة مطلعة.
/// </summary>
public record BranchDto(
    int Id,
    string Name,
    string Code,
    string? Address,
    string? Phone,
    string? Governorate,
    bool IsActive,
    int UserCount,
    int DocumentCount);

public record DashboardStatsDto(
    int TotalDocuments,
    int TotalDrafts,
    int TotalExecuted,
    int TotalDeferred,
    int TotalActive,
    int TotalBorrowers,
    decimal TotalAmount,
    decimal TotalCollectedAmount);

/// <summary>
/// تذكير إجراء/ملاحظة مرتبط بمستند، مع تاريخ الاستحقاق المحسوب
/// (تاريخ الإجراء + مدة التذكير، وإن غاب التاريخ فتاريخ الإنشاء + المدة).
/// `DueDateSuspect` وسم جودة: تاريخ/مدة قديمة لا تُحلَّل فسقط الحساب إلى احتياط —
/// يُعرض للمستخدم موسومًا لا تاريخًا عاديًا.
/// </summary>
public record ReminderDto(
    int ActionId,
    int DocumentId,
    string? DocumentType,
    string? BorrowerName,
    string? BorrowerFather,
    string? BorrowerFamily,
    string ActionText,
    string? ActionDate,
    string? ReminderDuration,
    string? ReminderColor,
    DateTime DueDate,
    bool DueDateSuspect);

/// <summary>شهر إحصائي (تاريخ القيد، وإن غاب أو تعذّر تحليله فشهر الإدخال).</summary>
public record MonthlyStatDto(int Year, int Month, int Count);

/// <summary>
/// نطاق الفترة الزمنية في إحصاءات المدير:
/// عام = السنة الحالية، ربعي = الربع الحالي، شهري = الشهر الحالي.
/// </summary>
public enum StatsPeriod
{
    Yearly = 1,
    Quarterly = 2,
    Monthly = 3,
}

/// <summary>مبلغ مجمّع بعملة محددة (ليرة سورية / دولار أمريكي / يورو / «أخرى» لغير المعروفة)، بقيمة غير صفرية.</summary>
public record CurrencyAmountDto(string Currency, decimal Amount);

/// <summary>
/// توزيع حالة على نوعَي العقد (مصرفي/عادي) مع مبالغ كل نوع مجمّعة حسب العملة الفعلية.
/// كل ملف ذو مبلغين (المطالب به بعمله والمبلغ الثاني بعمله) يُسجَّل كل مبلغ في سلة عملته،
/// والسلات مرتبة ترتيبًا ثابتًا: ليرة سورية، دولار أمريكي، يورو (تُستبعد العملات صفرية القيمة).
/// </summary>
public record ManagerContractSplitDto(
    int BankingCount,
    int OrdinaryCount,
    List<CurrencyAmountDto> BankingAmounts,
    List<CurrencyAmountDto> OrdinaryAmounts);

/// <summary>
/// بطاقات إحصاءات المدير على الملفات المقيَّدة في نطاق الفترة.
/// إجمالي الملفات = متداول + تحت رفع + تريث (دون المنفذ).
/// مبالغ كل حالة (للصالح/تحت رفع/تريث) مجمّعة حسب العملة الفعلية ومفصولة مصرفي/عادي:
/// المتداول بطاقة مركبة عددها الكبير = Active + TradingAgainstCount وداخلها صفّا
/// «متداول للصالح» (ActiveSplit) و«متداول للضد» (TradingAgainstCount + TradingAgainstAmounts
/// حيث المبالغ المطلوبة الثلاثة كلٌّ بعملتها)، وبطاقة «التريث» مفصولة مصرفي/عادي (DeferredSplit).
/// بطاقتا وضع «الجهة العامة منفذ عليها» معزولتان عن نظام «طالبة التنفيذ»:
/// «متداول للضد» = ملفات متداول فقط (المنفذ/المشطوب مستبعدان)،
/// «منفذ للضد» = ملفات منفذ فقط ومبلغها الذي دفعته الجهة العامة، وفترة البطاقتين من تاريخ ورود الاخطار.
/// صفة «عرض وايداع» تُحتسب «للصالح» كأسطر فرعية داخل بطاقتي متداول/منفذ:
/// DepositTradingCount = عدد ملفات العرض المتداولة، DepositExecutedCount + DepositExecutedAmount
/// = عدد ملفات العرض المنفذة ومجموع المبالغ المودعة بالليرة (وDepositExecutedAmounts فصلها لكل عملة)،
/// وفترة العرض من تاريخ ورود الاخطار أيضًا.
/// «منفذ للضد» مفصول لكل عملة (ExecutedAgainstAmounts) بموجب `BQ-029`؛ وحقلا `ExecutedAgainstAmount`
/// و`DepositExecutedAmount` هما سلة «ليرة سورية» فقط للتوافق (لا مجموعًا مختلطًا).
/// العملات خارج الثلاث المعروفة تُجمَّع في سلة «أخرى» مع تنبيه مراجعة بموجب `BQ-030`.
/// حقول الفترة توضح النطاق المعروض فعليًا على الخادم:
/// شهريًا: PeriodMonth مع PeriodYear، ربعيًا: PeriodQuarter مع PeriodYear، سنويًا: PeriodYear فقط.
/// </summary>
public record ManagerStatsDto(
    int TotalFiles,
    int Active,
    int Drafts,
    int Deferred,
    ManagerContractSplitDto ActiveSplit,
    ManagerContractSplitDto DraftsSplit,
    ManagerContractSplitDto DeferredSplit,
    List<CurrencyAmountDto> TotalAmounts,
    List<CurrencyAmountDto> TradingAgainstAmounts,
    int SettledCount,
    decimal SettledCollected,
    List<CurrencyAmountDto> SettledCollectedAmounts,
    int ForcibleCount,
    decimal ForcibleCollected,
    List<CurrencyAmountDto> ForcibleCollectedAmounts,
    int TradingAgainstCount,
    int ExecutedAgainstCount,
    decimal ExecutedAgainstAmount,
    List<CurrencyAmountDto> ExecutedAgainstAmounts,
    int DepositTradingCount,
    int DepositExecutedCount,
    decimal DepositExecutedAmount,
    List<CurrencyAmountDto> DepositExecutedAmounts,
    int PeriodYear,
    int? PeriodQuarter,
    int? PeriodMonth,
    /// <summary>
    /// إحصاء استئنافات المحامي في نطاق الفترة (للمحامي فقط عبر /stats/me):
    /// المنظورة قيد النظر، والمحسومة للصالح وللضد. null عند غياب أي استئناف
    /// لِيُخفى البطاقة من اللوحة.
    /// </summary>
    AppealsStatsDto? Appeals = null,
    /// <summary>
    /// ملفات «محال الى البداية» (خطة RTS) في نطاق الفترة: عدد + توزيع مصرفي/عادي
    /// (ReferredSplit) مرآة بطاقة التريث — ومنها من دخل من «منفذ جزئيا» فيُنقل عدّه
    /// ومبالغه من المتداول هنا، وTotalFiles يشمله (متداول + تحت رفع + تريث + محال).
    /// </summary>
    int ReferredToStartCount = 0,
    ManagerContractSplitDto? ReferredSplit = null,
    /// <summary>
    /// وسم مصدر الفترة (سبب القيد فقط): عدد ملفات النطاق التي حُسبت بتاريخ إدخالها (CreatedAt)
    /// لغياب تاريخ قيدها أو تعذّر تحليله (DateParsed) — يُعرض معلنًا لا مدمجًا بصمت.
    /// لا يشمل جهتي «منفذ عليها/إيداع» عند غياب إخطار ورودهما (راجع `PeriodDateFromReceiptCount`).
    /// </summary>
    int PeriodDateFallbackCount = 0,
    /// <summary>
    /// وسم مصدر الفترة (سبب الإخطار فقط): عدد ملفات جهتي «منفذ عليها/إيداع» التي حُسبت بتاريخ
    /// إدخالها لغياب تاريخ ورود الإخطار (`FileReceiptDate`) — تاريخ القيد ليس تاريخهما المعتمد
    /// أصلًا، فيُفصل عدّاده وتصريحه عن سبب القيد أعلاه.
    /// </summary>
    int PeriodDateFromReceiptCount = 0);

/// <summary>عدادات بطاقة «الاستئنافات» في لوحة المحامي وفق فلاتر الفترة.</summary>
public record AppealsStatsDto(
    int PendingCount,
    int DecidedInFavor,
    int DecidedAgainst);

/// <summary>نقطة زمنية داخل نطاق الفترة (شهر) لجدول محامي الفرع، مع وسم المصدر: ملفات النقطة المحسوبة بتاريخ إدخالها لغياب تاريخ قيدها أو تعذّر تحليله.</summary>
public record ManagerPeriodPointDto(int Year, int Month, int Count, int FromCreatedAtCount = 0);

/// <summary>
/// إحصاء محامي الفرع في نطاق الفترة،
/// مع توزيعه الشهري داخل النطاق (شهر واحد للفترة الشهرية، وثلاثة للربعية، واثنا عشر للسنوية).
/// </summary>
public record ManagerLawyerStatDto(
    int LawyerId,
    string LawyerName,
    int TotalCount,
    List<ManagerPeriodPointDto> Points);

public record BranchSummaryDto(
    int BranchId,
    string BranchName,
    int TotalDocuments,
    int TotalDrafts,
    decimal TotalAmount);

public record UserActivityDto(
    string Username,
    string FullName,
    int DocumentCount,
    int ViewCount);
