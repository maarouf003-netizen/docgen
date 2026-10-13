using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// مسجل التدقيق الزائف المشترك (R1-4): نُقل حرفيًا من `ServiceTests.cs` —
/// كل اختبار يملك نسخته الخاصة (`new()`) فلا تلوث عبر المشاركة.
/// </summary>
public class FakeAuditLogger : IAuditLogger
{
    public List<string> Actions { get; } = new();
    /// <summary>نصوص التفاصيل (`details`) لكل حدث — للتحقق من توثيق القديم/الجديد في أحداث النقل.</summary>
    public List<string?> Details { get; } = new();
    public List<(string ActionType, int? DocumentId, IReadOnlyList<DocumentFieldChange> Changes)> ChangeLogs { get; } = new();

    public Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default)
    {
        Actions.Add(actionType);
        Details.Add(details);
        return Task.CompletedTask;
    }

    public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            Actions.Add(entry.ActionType);
            Details.Add(entry.Details);
        }
        return Task.CompletedTask;
    }

    public Task LogDocumentChangeAsync(string? userName, string actionType, int documentId,
        string? documentType, string details, IReadOnlyList<DocumentFieldChange> changes,
        CancellationToken ct = default)
    {
        Actions.Add(actionType);
        ChangeLogs.Add((actionType, documentId, changes));
        return Task.CompletedTask;
    }
}
