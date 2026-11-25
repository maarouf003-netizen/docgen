using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// جسور اختبارية لمعالجات قراءات الإنابات (`P4-01ck`): تحافظ على توقيعات
/// الخدمة القديمة فوق معالجات الاستعلام — منطق الاختبارات كما هو.
/// </summary>
public static class DelegationReadQueryExtensions
{
    public static Task<List<DelegationDto>> ListForDocumentAsync(
        this IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> handler,
        int documentId,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetDocumentDelegationsQuery(documentId), ct);

    public static Task<List<DelegationDto>> ListPendingForHeadAsync(
        this IQueryHandler<GetPendingDelegationsQuery, List<DelegationDto>> handler,
        int branchId,
        int? ownerSectionId = null,
        bool rejectedOnly = false,
        CancellationToken ct = default)
        => handler.HandleAsync(new GetPendingDelegationsQuery(branchId, ownerSectionId, rejectedOnly), ct);

    public static Task<int> CountPendingForHeadAsync(
        this IQueryHandler<CountPendingDelegationsQuery, int> handler,
        int branchId,
        int? ownerSectionId = null,
        bool rejectedOnly = false,
        CancellationToken ct = default)
        => handler.HandleAsync(new CountPendingDelegationsQuery(branchId, ownerSectionId, rejectedOnly), ct);
}
