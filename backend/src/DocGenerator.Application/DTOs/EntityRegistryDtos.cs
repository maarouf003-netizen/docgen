namespace DocGenerator.Application.DTOs;

/// <summary>إنشاء قيد جهة نهائي مباشر (مدير/مشرف، أو رئيس قسم ضمن محافظته) — د2/د5.</summary>
public record CreatePublicEntityRequest(
    string CanonicalName,
    string EntityType,
    string Governorate,
    string BranchName,
    string? CitationFormula = null,
    IReadOnlyList<string>? Aliases = null);

/// <summary>
/// تعديل قيد/هوية: أي حقل يُترك null يبقى كما هو. تغيير CanonicalName يعني
/// إعادة تسمية جماعية تُزامن الأعمدة النصية في كل الصفوف المرتبطة (د5).
/// </summary>
public record UpdatePublicEntityRequest(
    string? CanonicalName,
    string? EntityType,
    string? Governorate,
    string? BranchName,
    string? CitationFormula,
    string? Status,
    bool? IsActive);

/// <summary>إضافة اسم كتابي بديل لقيد.</summary>
public record AddPublicEntityAliasRequest(string AliasText);

/// <summary>قيد في السجل — لشاشة الإدارة ونتائج البحث.</summary>
public record PublicEntityEntryDto(
    int Id,
    int GroupId,
    string CanonicalName,
    string EntityType,
    string Governorate,
    string BranchName,
    string CitationFormula,
    string Status,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<string> Aliases,
    string? CreatedByName = null);

/// <summary>اقتراح محامٍ لإضافة جهة جديدة (د4/د7/د8).</summary>
public record CreatePublicEntityProposalRequest(
    string ProposedName,
    string EntityType,
    string Governorate,
    string BranchName,
    string CitationFormula,
    int? SourceDocumentId = null);

/// <summary>اقتراح لعرض نافذة انتظار الاعتماد لدى رئيس القسم.</summary>
public record PublicEntityProposalDto(
    int Id,
    string ProposedName,
    string EntityType,
    string Governorate,
    string BranchName,
    string CitationFormula,
    int ProposedById,
    string ProposedByName,
    int? SourceDocumentId,
    string Status,
    DateTime CreatedAt,
    string? RejectionReason = null,
    int? CreatedPublicEntityId = null);

/// <summary>رفض اقتراح — السبب إلزامي ويُعرض لمقدم الاقتراح.</summary>
public record RejectPublicEntityProposalRequest(string Reason);

/// <summary>كتابة متمايزة واحدة لنص جهة مع عدّاد ملفاتها وجهتها في الاستيراد.</summary>
public record ImportVariantDto(
    string Text,
    string Side,
    string? Governorate,
    int DocumentCount);

/// <summary>
/// مرشّح استيراد: كل نصوص متطابقة بعد التطبيع تجتمع تحت مرشح واحد مع اقتراح
/// الكتابة الأكثر تكرارًا كاسم معتمد والمحافظات المرصودة.
/// </summary>
public record ImportPreviewItemDto(
    string NormalizedName,
    string SuggestedCanonicalName,
    int TotalDocuments,
    IReadOnlyList<string> Governorates,
    IReadOnlyList<ImportVariantDto> Variants);

/// <summary>نتيجة المعاينة قبل الاعتماد — تُمرَّر معدَّلة إلى import-commit.</summary>
public record ImportPreviewResponse(
    DateTime GeneratedAtUtc,
    IReadOnlyList<ImportPreviewItemDto> Items);

/// <summary>بند معتمد في الاستيراد: ينشئ هوية أم + قيدًا نهائيًا Final مباشرة (د12).</summary>
public record ImportCommitItemRequest(
    string NormalizedName,
    string CanonicalName,
    string EntityType,
    string Governorate,
    string BranchName,
    string? CitationFormula = null,
    bool AddVariantsAsAliases = true);

/// <summary>اعتماد الربط الجماعي لنصوص تاريخية مختارة من المعاينة.</summary>
public record ImportCommitRequest(IReadOnlyList<ImportCommitItemRequest> Items);

/// <summary>حصيلة الاستيراد لأغراض التدقيق والعرض.</summary>
public record ImportCommitResultDto(
    int GroupsCreated,
    int EntriesCreated,
    int AliasesAdded);
