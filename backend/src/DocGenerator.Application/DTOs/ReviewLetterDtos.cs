namespace DocGenerator.Application.DTOs;

/// <summary>تسطير كتاب مطالعة — المحامي فقط. DocumentId فارغ يعني كتابًا عامًا غير مرتبط بملف.</summary>
public record CreateReviewLetterRequest(int? DocumentId, string BodyHtml);

/// <summary>إضافة لاحق إلى كتاب مطالعة — محامي الكتاب فقط. يعيد الكتاب إلى «بانتظار رد».</summary>
public record AddReviewLetterAddendumRequest(string BodyHtml);

/// <summary>رد رئيس القسم على كتاب المطالعة أو أحد اللاحقات — رئيس قسم الفرع فقط.</summary>
public record ReplyReviewLetterRequest(string BodyHtml);

/// <summary>رسالة واحدة ضمن كتاب المطالعة (الأصل أو لاحق أو رد).</summary>
public record ReviewLetterMessageDto(
    int Id,
    string Kind,
    string BodyHtml,
    string MessageNumber,
    DateTime MessageDate,
    int AuthorId,
    string AuthorName,
    string AuthorRole);

/// <summary>سياق الملف المرتبط بصيغة العرض: مطالعة بملف (الاسم الثلاثي) رقم.. نوع.. لعام.. دائرة تنفيذ..</summary>
public record ReviewLetterFileContextDto(
    string ExecutedName,
    string? FileNumber,
    string? FileType,
    string? FileYear,
    string? Court);

/// <summary>كتاب مطالعة كامل مع رسائله مرتبة زمنيًا.</summary>
public record ReviewLetterDto(
    int Id,
    string LetterNumber,
    DateTime LetterDate,
    bool IsAnswered,
    int? DocumentId,
    ReviewLetterFileContextDto? FileContext,
    int BranchId,
    string LawyerName,
    /// <summary>هل فيه ردّ رئيس قسم لم يطّلع عليه محامي الكتاب؟ (لأتمتة تعليم الإطلاع عند الفتح).</summary>
    bool HasUnseenReply,
    IReadOnlyList<ReviewLetterMessageDto> Messages,
    DateTime CreatedAt);

/// <summary>
/// سطر كتاب في القائمة: للكتاب المربوط بالملف تُملأ FileContext،
/// وللعام تبقى null (يُعرض: كتاب مطالعة عام غير مرتبط بملف).
/// Snippet مقتطف نص الكتاب الأصلي، وHasUnseenReply تعني وجود ردّ رئيس قسم لم يطّلع عليه المحامي.
/// </summary>
public record ReviewLetterListItemDto(
    int Id,
    string LetterNumber,
    DateTime LetterDate,
    bool IsAnswered,
    int? DocumentId,
    ReviewLetterFileContextDto? FileContext,
    string LawyerName,
    string Snippet,
    string LastKind,
    bool HasUnseenReply,
    int MessagesCount,
    DateTime UpdatedAt);
