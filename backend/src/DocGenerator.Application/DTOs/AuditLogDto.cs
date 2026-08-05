namespace DocGenerator.Application.DTOs;

public record AuditLogDto(
    int Id,
    DateTime Timestamp,
    string? UserName,
    string? ActionType,
    string? Details,
    int? DocumentId,
    string? DocumentType);
