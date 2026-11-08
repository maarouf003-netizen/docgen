using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات سجل أرقام الأساس فوق <see cref="DocGeneratorDbContext"/>.
/// <c>IgnoreQueryFilters</c> مقيّد بمواضع صريحة (سجل ملف محذوف/فحص تحذير)
/// ويُفحص النطاق في طبقة الخدمة دائمًا.
/// </summary>
public sealed class DocumentBaseNumberRepository : Repository<DocumentBaseNumber>, IDocumentBaseNumberRepository
{
    public DocumentBaseNumberRepository(DocGeneratorDbContext db) : base(db) { }

    private DbSet<DocumentBaseNumber> BaseNumbers => Db.BaseNumbers;

    public Task<List<DocumentBaseNumber>> ListByDocumentAsync(
        int documentId, bool includeDeletedDocument, CancellationToken ct = default)
    {
        var query = includeDeletedDocument
            ? BaseNumbers.IgnoreQueryFilters()
            : BaseNumbers;
        return query
            .Include(b => b.Document)
            .Where(b => b.DocumentId == documentId)
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .ToListAsync(ct);
    }

    public Task<List<DocumentBaseNumber>> ListActiveByCircuitYearAsync(
        int circuitId, int year, CancellationToken ct = default)
        => BaseNumbers
            .Include(b => b.Document)
            .Where(b => b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId
                && b.Year == year)
            .ToListAsync(ct);

    public Task<List<DocumentBaseNumber>> ListActiveByDocumentCircuitAsync(
        int documentId, int circuitId, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId
                && b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId)
            .ToListAsync(ct);

    public Task<DocumentBaseNumber?> FindLatestActiveRegistrationAsync(
        int documentId, int circuitId, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId
                && b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId
                && b.Source == NumberSources.Registration)
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    public Task<DocumentBaseNumber?> FindActiveRegistrationAsync(
        int documentId, int circuitId, int year, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId
                && b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId
                && b.Year == year
                && b.Source == NumberSources.Registration)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    public Task<DocumentBaseNumber?> FindLegacyRegistrationFallbackAsync(
        int documentId, int circuitId, int year, string numberKey, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId
                && b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId
                && b.Year == year
                && b.Source == null
                && b.NumberKey == numberKey)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    public Task<DocumentBaseNumber?> FindActiveRowForYearAsync(
        int documentId, int circuitId, int year, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId
                && b.VoidedAt == null
                && b.ExecutionCircuitId == circuitId
                && b.Year == year)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .FirstOrDefaultAsync(ct);

    public Task<DocumentBaseNumber?> FindRowWithDocumentAsync(int rowId, CancellationToken ct = default)
        => BaseNumbers
            .Include(b => b.Document)
            .FirstOrDefaultAsync(b => b.Id == rowId, ct);

    public Task<List<DocumentBaseNumber>> FindConflictCandidatesAsync(
        string typeKey, int year, string numberKey, CancellationToken ct = default)
        => BaseNumbers
            .IgnoreQueryFilters()
            .Include(b => b.Document)
            .Where(b => b.Year == year
                && b.TypeKey == typeKey
                && b.NumberKey == numberKey)
            .ToListAsync(ct);

    public Task<List<DocumentBaseNumber>> ListActiveByDocumentAsync(
        int documentId, CancellationToken ct = default)
        => BaseNumbers
            .Where(b => b.DocumentId == documentId && b.VoidedAt == null)
            .ToListAsync(ct);
}
