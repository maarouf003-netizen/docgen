namespace DocGenerator.Application.DTOs;

/// <summary>
/// تسطير مراسلة — محامٍ أو رئيس قسم أو مندوب جهة. DocumentId فارغ يعني مراسلة عامة
/// غير مرتبطة بملف. TargetUserId إجباري: الطرف المستلم المعيَّن بالاسم.
/// </summary>
public record CreateCorrespondenceRequest(
    int? DocumentId,
    int TargetUserId,
    string Importance,
    string BodyHtml);

/// <summary>إضافة لاحق إلى مراسلة — منشئ المراسلة نفسه فقط.</summary>
public record AddCorrespondenceAddendumRequest(string BodyHtml);

/// <summary>رد الطرف المستلم على المراسلة أو أحد اللاحقات — الطرف المستلم فقط.</summary>
public record ReplyCorrespondenceRequest(string BodyHtml);

/// <summary>رسالة واحدة ضمن مراسلة (الأصل أو لاحق أو رد).</summary>
public record CorrespondenceMessageDto(
    int Id,
    string Kind,
    string BodyHtml,
    string MessageNumber,
    DateTime MessageDate,
    int AuthorId,
    string AuthorName,
    string AuthorRole);

/// <summary>سياق الملف المرتبط بصيغة العرض: مراسلة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ..</summary>
public record CorrespondenceFileContextDto(
    string ExecutedName,
    string? FileNumber,
    string? FileType,
    string? FileYear,
    string? Court);

/// <summary>توثيق مشاهدة واحدة: من شاهد ومتى.</summary>
public record CorrespondenceReceiptDto(
    int UserId,
    string UserName,
    DateTime SeenAt);

/// <summary>مراسلة كاملة مع رسائلها مرتبة زمنيًا وتوثيق مشاهداتها.</summary>
public record CorrespondenceDto(
    int Id,
    string CorrespondenceNumber,
    DateTime CorrespondenceDate,
    string Importance,
    int? DocumentId,
    CorrespondenceFileContextDto? FileContext,
    int? BranchId,
    string Governorate,
    string? AdministrativeBranchName,
    int CreatorId,
    string CreatorName,
    string CreatorRole,
    int TargetUserId,
    string TargetName,
    string TargetRole,
    /// <summary>هل أكّد القارئ الحالي مشاهدته؟ (لإظهار زر «تمت المشاهدة» أو حالته).</summary>
    bool SeenByMe,
    IReadOnlyList<CorrespondenceMessageDto> Messages,
    IReadOnlyList<CorrespondenceReceiptDto> Receipts,
    DateTime CreatedAt);

/// <summary>
/// سطر مراسلة في القائمة: للمرتبطة بالملف تُملأ FileContext، وللعامة تبقى null.
/// Snippet مقتطف نص المراسلة الأصلية.
/// </summary>
public record CorrespondenceListItemDto(
    int Id,
    string CorrespondenceNumber,
    DateTime CorrespondenceDate,
    string Importance,
    int? DocumentId,
    CorrespondenceFileContextDto? FileContext,
    string CreatorName,
    string TargetName,
    string Snippet,
    string LastKind,
    bool SeenByMe,
    /// <summary>عاجلة ولم يؤكد القارئ الحالي مشاهدتها — وقود الجرس.</summary>
    bool IsUrgentUnseen,
    int MessagesCount,
    int ReceiptsCount,
    string? AdministrativeBranchName,
    string Governorate,
    DateTime UpdatedAt);

/// <summary>مستلم مرشح لمراسلة جديدة (بحث بالاسم — محامٍ/رئيس قسم/مندوب جهة نشط).</summary>
public record CorrespondenceTargetDto(
    int UserId,
    string FullName,
    string Role,
    string? BranchName,
    string? Governorate);
