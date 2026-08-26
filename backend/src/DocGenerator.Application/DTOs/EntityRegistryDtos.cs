namespace DocGenerator.Application.DTOs;

/// <summary>إنشاء قيد جهة نهائي مباشر (مدير/مشرف، أو رئيس قسم ضمن محافظته) — د2/د5.</summary>
public record CreatePublicEntityRequest(
    string CanonicalName,
    string EntityType,
    string Governorate,
    string BranchName,
    string? CitationFormula = null,
    IReadOnlyList<string>? Aliases = null,
    string? CoverageLabel = null);

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
    bool? IsActive,
    string? CoverageLabel = null);

/// <summary>إضافة اسم كتابي بديل لقيد.</summary>
public record AddPublicEntityAliasRequest(string AliasText);

/// <summary>قيد في السجل — لشاشة الإدارة ونتائج البحث وقائمة المراجعة.</summary>
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
    string? CreatedByName = null,
    /// <summary>أدخلها محامٍ وهي بانتظار مراجعة رئيس القسم (النموذج الجديد).</summary>
    bool NeedsReview = false,
    /// <summary>تسمية التغطية الجغرافية (تظهر بدل المحافظة في البطاقات والبحث).</summary>
    string? CoverageLabel = null);

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

// ── النقل (MoveEntry) ──

/// <summary>طلب نقل قيد جهة من هوية إلى أخرى أو طيّه في قيد قائم.</summary>
public record MoveEntryRequest(
    int? TargetGroupId,
    int? TargetEntryId,
    string? DecreeKind,
    string? DecreeNumber,
    string? DecreeDate,
    string? Note);

/// <summary>طلب نقل جميع قيود مجموعة إلى مجموعة أخرى (تبعية كاملة).</summary>
public record MoveAllEntriesRequest(
    int SourceGroupId,
    int TargetGroupId,
    string? DecreeKind,
    string? DecreeNumber,
    string? DecreeDate,
    string? Note);

/// <summary>نتيجة نقل قيد واحد.</summary>
public record MoveEntryResponse(
    int EntryId,
    int FromGroupId,
    int ToGroupId,
    int AffectedDocuments,
    int ChangeEventId);

/// <summary>نتيجة نقل جميع قيود مجموعة.</summary>
public record MoveAllEntriesResponse(
    int SourceGroupId,
    int TargetGroupId,
    int EntriesMoved,
    int AffectedDocuments,
    int ChangeEventId);

// ── الدمج N←1 (د5 §4) ──

/// <summary>طلب معاينة الدمج قبل الاعتماد.</summary>
public record MergePreviewRequest(
    int SurvivorGroupId,
    IReadOnlyList<int> AbsorbedGroupIds);

/// <summary>قيد مُهمَل في المعاينة مع مسار امتصاصه.</summary>
public record AbsorbedEntryPreviewDto(
    int EntryId,
    string Governorate,
    string BranchName,
    int DocumentCount,
    /// <summary>القيد في الهوية الناجية المطابق (same gov+branch)، أو القيد الافتراضي إن لم يطابق.</summary>
    int MappedToEntryId,
    bool ConflictsWithSurvivor);

/// <summary>هوية أم مُهمَلة في المعاينة.</summary>
public record AbsorbedGroupPreviewDto(
    int GroupId,
    string Name,
    IReadOnlyList<AbsorbedEntryPreviewDto> Entries,
    int TotalDocuments,
    IReadOnlyList<string> Aliases);

/// <summary>نتيجة معاينة الدمج.</summary>
public record MergePreviewResponse(
    string SurvivorName,
    IReadOnlyList<AbsorbedGroupPreviewDto> AbsorbedGroups,
    int TotalAffectedDocuments,
    IReadOnlyList<string> Warnings);

/// <summary>طلب اعتماد الدمج.</summary>
public record MergeCommitRequest(
    int SurvivorGroupId,
    IReadOnlyList<int> AbsorbedGroupIds,
    bool UnifyTexts = false);

/// <summary>نتيجة الدمج.</summary>
public record MergeCommitResponse(
    int AbsorbedGroupsCount,
    int EntriesMigrated,
    int AliasesAdded,
    int TotalAffectedDocuments,
    int ChangeEventId);
