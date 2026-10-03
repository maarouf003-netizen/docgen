namespace DocGenerator.Application.DTOs;

/// <summary>اقتراح تطوير — `SenderName` للمشرف والمدير (`BQ-001`)؛ سجل المرسل بلا أسماء الآخرين.</summary>
public record AppSuggestionDto(
    int Id,
    string Message,
    DateTime CreatedAt,
    bool IsRead,
    string? SenderName);

/// <summary>إرسال اقتراح — نص حر حتى 2000 حرف.</summary>
public record CreateAppSuggestionRequest(string Message);
