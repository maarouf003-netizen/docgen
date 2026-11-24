using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService : IDocumentService
{
    public async Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var currentYear = CurrentYear();
        var (total, docs) = await _documents.GetRotationCandidatesAsync(
            userId, currentYear, page, perPage, _numberRegistry.AllowsCircuitlessLegacy, ct);
        var items = docs
            .Select(d => new RotationDocumentDto(
                d.Id,
                d.Court,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.FileNumber,
                d.FileType,
                EffectiveFileIdentity.Latest(d, currentYear)?.BaseNumber,
                DocumentText.RotationDisplayName(d),
                EffectiveFileIdentity.Number(d, currentYear),
                EffectiveFileIdentity.Year(d, currentYear)))
            .ToList();

        return new PagedResult<RotationDocumentDto>
        {
            Items = items,
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
    {
        var history = await _numberRegistry.GetHistoryAsync(documentId, includeDeletedDocument: false, ct);
        return history.ToList();
    }

    public async Task<List<BaseNumberHistoryDto>> GetDeletedBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
    {
        var history = await _numberRegistry.GetHistoryAsync(documentId, includeDeletedDocument: true, ct);
        return history.ToList();
    }
}
