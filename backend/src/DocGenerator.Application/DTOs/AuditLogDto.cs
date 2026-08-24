namespace DocGenerator.Application.DTOs;

public record AuditLogDto(
    int Id,
    DateTime Timestamp,
    string? UserName,
    string? ActionType,
    string? Details,
    int? DocumentId,
    string? DocumentType);

/// <summary>تغيّر حقل واحد: القيمة قبل التعديل وبعده بتسمية عربية مجمّدة.</summary>
public record DocumentFieldChangeDto(
    string FieldLabel,
    string FieldKey,
    string? OldValue,
    string? NewValue);

/// <summary>
/// مجموعة تعديلات واحدة (إدخال تدقيق): الفاعل والوقت ونوع الإجراء، وكل الحقول
/// التي تغيّرت في هذه العملية.
/// </summary>
public record DocumentChangeGroupDto(
    int AuditLogId,
    string ActionType,
    string? UserName,
    DateTime Timestamp,
    IReadOnlyList<DocumentFieldChangeDto> Changes);
