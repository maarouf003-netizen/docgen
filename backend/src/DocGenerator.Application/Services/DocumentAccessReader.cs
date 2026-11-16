using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ <see cref="IDocumentAccessReader"/> فوق المستودعات القائمة: لا منطق خاص،
/// ولا قراءة خارج ما تسمح به عقود المستودع (إسقاط F-01 وسؤالا الاستئناف).
/// </summary>
public sealed class DocumentAccessReader(
    IDocumentRepository documents,
    IAppealRepository appeals) : IDocumentAccessReader
{
    public Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default)
        => documents.GetAccessInfoAsync(documentId, ct);

    public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
        => appeals.HasForwardedAppealAsync(documentId, ct);

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => appeals.IsAssignedFollowerAsync(documentId, userId, ct);
}
