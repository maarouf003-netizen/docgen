using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// صفوف «معلقات التسجيل» للمحامي — المعروض حاليًا (من آخر إحالة) مع سقوط آمن
/// لبيانات الملف الأصلية. قراءة صرفة بلا كتابة.
/// </summary>
public sealed record ListMyPendingRegistrationsQuery(int LawyerId, int? CircuitId)
    : IQuery<List<PendingRegistrationDto>>;

internal sealed class ListMyPendingRegistrationsQueryHandler
    : QueryHandlerBase<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IReferenceDataCache? _cache;

    public ListMyPendingRegistrationsQueryHandler(
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IEnumerable<IPipelineBehavior<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _documents = documents;
        _circuits = circuits;
        _cache = cache;
    }

    protected override async Task<List<PendingRegistrationDto>> HandleCoreAsync(
        ListMyPendingRegistrationsQuery query, CancellationToken ct)
    {
        var docs = await _documents.ListPendingForLawyerAsync(query.LawyerId, query.CircuitId, ct);
        if (docs.Count == 0) return new List<PendingRegistrationDto>();
        var referrals = await _documents.ListLastCircuitReferralsAsync(docs.Select(d => d.Id).ToList(), ct);
        var referralByDoc = referrals
            .GroupBy(o => o.DocumentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Id).First());
        var circuits = await ReferenceDataQueries.ListCircuitsAsync(_circuits, _cache, ct);
        var circuitNames = circuits.ToDictionary(c => c.Id, c => c.Name);
        return docs.OrderBy(d => d.Id).Select(d =>
        {
            // المعروض حاليًا (قديم — من EffectiveFileIdentity).
            referralByDoc.TryGetValue(d.Id, out var lastReferred);
            string? circuitName = null;
            if (d.ExecutionCircuitId is not null)
                circuitNames.TryGetValue(d.ExecutionCircuitId.Value, out circuitName);
            return new PendingRegistrationDto(
                d.Id,
                d.ExecutionCircuitId ?? 0,
                circuitName ?? d.Court,
                d.BorrowerName,
                lastReferred?.FileNumber ?? d.FileNumber,
                lastReferred?.FileType ?? d.FileType,
                lastReferred?.Year?.ToString(CultureInfo.InvariantCulture) ?? d.FileYear);
        }).ToList();
    }
}
