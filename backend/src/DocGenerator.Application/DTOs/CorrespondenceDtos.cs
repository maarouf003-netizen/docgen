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
    /// <summary>
    /// حالة اطلاع الطرف المستلم: <c>seen</c> أو <c>pending</c> — من الخادم لا من
    /// مقارنة المعرّفات، فيصل لكل قارئ مصرَّح له (المدير يرى حالة مستلمه لا حالته).
    /// التوثيق مقصور على المستلم وحده، فإجماله — لا «هل شاهدته أنا».
    /// </summary>
    string ViewStatus,
    /// <summary>
    /// هل لهذا القارئ حق التوثيق؟ (المستلم وحده — قرار خادم لا مقارنة في الواجهة).
    /// يقابل <c>CorrespondenceListItemDto.CanMarkSeen</c>؛ فصلُه عن صلاحية الرد
    /// يمنع الواجهة من تمرير «يمكنني الرد» إلى «يمكنني التوثيق» بالتصادم.
    /// </summary>
    bool CanMarkSeen,
    /// <summary>
    /// هل لهذا القارئ حق الرد؟ (المستلم وحده — قرار خادم لا مقارنة في الواجهة).
    /// صلاحية الرد مستقلة عن صلاحية التوثيق: تطابقهما اليوم صدفة نموذج لا قاعدة،
    /// فلا تُستنتج إحداهما من الأخرى في أي عميل.
    /// </summary>
    bool CanReply,
    IReadOnlyList<CorrespondenceMessageDto> Messages,
    /// <summary>توثيق مشاهدة المستلم فقط (لا مرسل ولا رئيس ولا مدير).</summary>
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
    /// <summary>
    /// حالة اطلاع الطرف المستلم: <c>seen</c> أو <c>pending</c>.
    /// ⚠️ مقياسها المراسلة لا الرسالة: التوثيق يُسجَّل على المراسلة كلها، فإضافةُ
    /// لاحق بعد اطلاع المستلم تُبقيها <c>seen</c>، وجعلها تُقاس على كل رسالة على
    /// حدة يحتاج توثيقًا لكل رسالة (تغيير مخطط + هجرة) — خارج هذا العقد.
    /// </summary>
    string ViewStatus,
    /// <summary>هل لهذا القارئ حق التوثيق؟ (المستلم وحده — لا يُشتقّ من معرّفات في الواجهة).</summary>
    bool CanMarkSeen,
    /// <summary>هل لهذا القارئ حق الرد؟ (المستلم وحده — قرار خادم).</summary>
    bool CanReply,
    int MessagesCount,
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
