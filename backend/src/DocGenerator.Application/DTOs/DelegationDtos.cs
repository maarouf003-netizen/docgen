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
    List<int>? AssetIds);

/// <summary>
/// تعيين المحامي المختص للإنابة من رئيس القسم (الدائرة المنابة): يُنشأ الملف المناب تلقائيًا.
/// </summary>
public record AssignDelegationRequest(
    int AssignedLawyerId);

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
/// وتاريخ «قرار الإحالة القطعية» (إلزامي — يُحفظ على الملف المنيب عند تفعيله «منفذ جبريا»).
/// </summary>
public record CompleteDelegationRequest(
    string? ReturnDate,
    List<DelegationSaleDto>? Sales,
    string? ForcedExecutionDate = null);

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
    int CreatedById);
