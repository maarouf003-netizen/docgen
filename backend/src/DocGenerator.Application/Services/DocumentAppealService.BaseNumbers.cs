using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentAppealService : IDocumentAppealService
{
    // ── أرقام الأساس الاستئنافية (قراءة) ─────────────────────────────────

    public async Task<List<AppealBaseNumberHistoryDto>> GetBaseNumberHistoryAsync(
        int appealId, CancellationToken ct = default)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(appealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        return AppealBaseNumberPolicy.BuildHistory(appeal, currentYear)
            .Select(h => new AppealBaseNumberHistoryDto(h.Year, h.Number, h.IsCurrent))
            .ToList();
    }

    public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
        => _appeals.IsAssignedFollowerAsync(documentId, userId, ct);

    public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
        => _appeals.HasForwardedAppealAsync(documentId, ct);
}
