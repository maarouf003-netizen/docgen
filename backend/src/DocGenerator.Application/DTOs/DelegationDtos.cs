namespace DocGenerator.Application.DTOs;

/// <summary>
/// أصلٌ موضوع إنابة في طلب/استجابة الإنابة: وصف قراءة (النوع + وصفه) وبدل المبيع عند البيع،
/// وعلم «عُدّلت بياناته بعد التسطير» (تُحدَّث اللقطة تلقائيًا ويظهر تنبيه للمستخدم).
/// </summary>
public record DelegationAssetDto(
    int Id,
    string AssetKind,
    string AssetLabel,
    decimal? SalePrice,
    bool SnapshotAdjusted);

/// <summary>
/// إنشاء/تعديل إنابة (تسطير من محامي الملف المنيب). التواريخ نصوص حرة تُفسَّر وتُخزَّن زمنيًا
/// كباقي تواريخ الملفات؛ الفارغ يعني null وغير الصالح يُرفض.
/// AssetIds: معرفات أصول الملف المنيب موضوع الإنابة (تُلتقط بلقطة وصف قراءة عند الحفظ).
/// </summary>
public record UpsertDelegationRequest(
    string? DelegatedCourt,
    bool IsExternal,
    int? ExternalBranchId,
    string? DelegationDate,
    string? DelegationText,
    string? DepositBookNumber,
    string? DepositBookDate,
    List<int>? AssetIds,
    int? DelegatedCircuitId = null);

/// <summary>
/// تعيين المحامي المختص للإنابة من رئيس القسم (الدائرة المنابة): يُنشأ الملف المناب تلقائيًا.
/// </summary>
public record AssignDelegationRequest(
    int AssignedLawyerId,
    /// <summary>رمز التزامن التفاؤلي (من `DelegationDto`) — اختياري، غيابه يعني فحص القاعدة فقط.</summary>
    long? Version = null);

/// <summary>توجيه إنابة خارجية معلّقة لشعبة في الفرع المناب — رئيس قسم الفرع المناب فقط (§7.3).</summary>
public record RedirectDelegationRequest(
    int SectionId,
    /// <summary>رمز التزامن التفاؤلي — اختياري، غيابه يعني فحص القاعدة فقط.</summary>
    long? Version = null);

/// <summary>رفض الدائرة الخطأ برسالة تُعيد المحامي للتصحيح (§7.4) — السبب إلزامي.</summary>
public record RejectDelegationRequest(
    string? Reason,
    /// <summary>رمز التزامن التفاؤلي — اختياري، غيابه يعني فحص القاعدة فقط.</summary>
    long? Version = null);

/// <summary>
/// تسجيل الإنابة أصولًا من محامي الفرع المناب: رقم أساس الإنابة وتاريخ قيدها (بيانات الملف المناب).
/// </summary>
public record RegisterDelegationRequest(
    string? FileNumber,
    string? FileYear,
    string? FileRegistrationDate);

/// <summary>
/// إتمام الإنابة من محامي الملف المناب: بيع الأموال موضوع الإنابة بالمزاد العلني،
/// مع بدل المبيع لكل أصل (بالليرة السورية) وتاريخ إعادة الملف إلى الدائرة المنيبة،
/// وتاريخ «قرار الإحالة القطعية» (إلزامي — يُحفظ على الملف المنيب عند تفعيله «منفذ جبريا»)،
/// وهل غطى بدل المبيع كامل المديونية (يحدده محامي المناب عند الإتمام).
/// </summary>
public record CompleteDelegationRequest(
    string? ReturnDate,
    List<DelegationSaleDto>? Sales,
    string? ForcedExecutionDate = null,
    bool? SaleCoversFullDebt = null);

/// <summary>بدل المبيع لأصل مباعٍ بالمزاد ضمن إتمام الإنابة (بالليرة السورية).</summary>
public record DelegationSaleDto(
    int DelegationAssetId,
    decimal SalePrice);

/// <summary>
/// إنابة للعرض (بطاقة «تشعبات الملف» في المنيب، و«معلومات الملف المنيب» في المناب،
/// وقائمة «طلبات الإنابة» لرئيس القسم). التواريخ نصية بصيغة yyyy-MM-dd.
/// </summary>
public record DelegationDto(
    int Id,
    int SourceDocumentId,
    string? SourceDocumentLabel,
    /// <summary>رقم أساس الملف المنيب الحالي (رقم أساس سنة التدوير إن وُجد وإلا رقم ملفه الأصلي).</summary>
    string? SourceFileNumber,
    /// <summary>سنة الرقم المعروض للملف المنيب (سنة التدوير إن وُجدت وإلا سنة ملفه الأصلي).</summary>
    string? SourceFileYear,
    int? TargetDocumentId,
    string? DelegatedCourt,
    bool IsExternal,
    int? ExternalBranchId,
    string? ExternalBranchName,
    string? DelegationDate,
    string? DelegationText,
    string? DepositBookNumber,
    string? DepositBookDate,
    int? AssignedLawyerId,
    string? AssignedLawyerName,
    string? ReturnDate,
    string Status,
    DateTime CreatedAt,
    string? CreatedByName,
    List<DelegationAssetDto> Assets,
    int CreatedById,
    /// <summary>هل غطى بدل المبيع كامل المديونية؟ يحدده محامي المناب عند الإتمام — null قبل الإتمام.</summary>
    bool? SaleCoversFullDebt = null,
    /// <summary>رقم أساس الملف المناب الحالي (رقم أساس سنة التدوير إن وُجد وإلا رقم ملفه الأصلي) — يُحسب مع TargetFileYear من السجل نفسه.</summary>
    string? TargetFileNumber = null,
    /// <summary>سنة الرقم المعروض للملف المناب (سنة التدوير إن وُجدت وإلا سنة ملفه الأصلي) — مرافقة لـ TargetFileNumber.</summary>
    string? TargetFileYear = null,
    /// <summary>نوع الملف المنيب (FileType) كما هو — يُعرض بجانب رقم أساس المنيب، والفارغ يُخفى.</summary>
    string? SourceFileType = null,
    /// <summary>
    /// حالة الملف المناب (ExecStatus) في سطر «تشعبات الملف» — تحمل «مسترد»/«تريث»/«منفذ إنابة»
    /// وغيرها لشارة حالة المناب في المنيب (L11/F1)، وفارغٌ قبل اعتماد الإنابة (بلا ملف مناب).
    /// </summary>
    string? TargetExecStatus = null,
    /// <summary>
    /// دائرة التنفيذ المختصة بالملف المنيب (الدائرة المنيبة) — تُعرض في بطاقة «معلومات
    /// الملف المنيب» بدل «الدائرة المنابة»، كما هي مخزنة بلا بادئات، والفارغ يُخفى.
    /// </summary>
    string? SourceCourt = null,
    /// <summary>
    /// هل تحجب هذه الإنابة أموالها عن أي تسطير جديد؟ — السارية (أي حال سوى «منفذ إنابة»)
    /// ما دام منابها غير نهائي (غير مشطوب/مسترد/منفذ إنابة)؛ المعلّقة بلا مناب حاجبة دائمًا.
    /// تُستخدم لترشيح قائمة الأموال في نافذة التسطير (إخفاء المحجوب).
    /// </summary>
    bool BlocksAssets = false,
    /// <summary>
    /// هل بلغ الملف المناب حالة نهائية (مشطوب بجهتيه/مسترد/منفذ إنابة)؟ — مرآة IsTargetTerminal
    /// للعرض (إخفاء سطر «بانتظار الإتمام» للمناب النهائي)؛ false قبل اعتماد الإنابة (بلا مناب).
    /// </summary>
    bool TargetTerminal = false,
    /// <summary>الدائرة المنابة المرجعية (للداخلية فقط).</summary>
    int? DelegatedCircuitId = null,
    /// <summary>
    /// بطاقة الملف المناب (§5.7 — قرار §2.25): فرع الملف المناب الحي (قد يعبر
    /// الفروع) — قراءة سياقية داخل عرض المنيب، بلا ملاحة مباشرة.
    /// </summary>
    int? TargetBranchId = null,
    /// <summary>اسم فرع الملف المناب الحي — للعرض فقط.</summary>
    string? TargetBranchName = null,
    /// <summary>المحامي المالك الحالي للملف المناب (مرآة `Lawyer`) — للعرض فقط.</summary>
    string? TargetLawyerName = null,
    /// <summary>سبب رفض الدائرة الخطأ (§7.4) — `null` قبل أي رفض وبعد تصحيح المحامي.</summary>
    string? RejectReason = null,
    /// <summary>الشعبة الموجَّه لها طلب الإنابة الخارجية (§7.3) — `null` قبل التوجيه.</summary>
    int? RedirectedToSectionId = null,
    /// <summary>اسم الشعبة الموجَّه لها الطلب — للعرض فقط.</summary>
    string? RedirectedToSectionName = null,
    /// <summary>
    /// رمز التزامن التفاؤلي للصف — يُعاد في القراءة ويُرسَل في طلبات الكتابة
    /// التي تدعمه (اعتماد/توجيه/رفض) لكشف السباق مبكرًا برسالة ودية.
    /// </summary>
    long Version = 0);
