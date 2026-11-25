using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>عدد ملفات المحامي غير المحذوفة (للمعاينة قبل النقل الجماعي).</summary>
public sealed record CountFilesByOwnerQuery(
    int OwnerId,
    int? ScopeBranchId,
    int? OwnerSectionId = null) : IQuery<int>;

internal sealed class CountFilesByOwnerQueryHandler
    : QueryHandlerBase<CountFilesByOwnerQuery, int>
{
    private readonly IUserRepository _users;
    private readonly IDocumentRepository _documents;

    public CountFilesByOwnerQueryHandler(
        IUserRepository users,
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<CountFilesByOwnerQuery, int>> behaviors)
        : base(behaviors)
    {
        _users = users;
        _documents = documents;
    }

    protected override async Task<int> HandleCoreAsync(
        CountFilesByOwnerQuery query, CancellationToken ct)
    {
        var owner = await _users.GetByIdAsync(query.OwnerId, ct);
        if (owner is null || owner.Role != UserRole.Lawyer)
            throw new ArgumentException("المحامي غير موجود");
        if (query.ScopeBranchId.HasValue && owner.BranchId != query.ScopeBranchId)
            throw new ArgumentException("لا يمكن عرض ملفات محامٍ من فرع آخر");
        // المعاينة بالنطاق نفسه (§5.5): تطابق المنقول فعلًا.
        return await _documents.CountByOwnerInScopeAsync(
            query.OwnerId, query.ScopeBranchId, query.OwnerSectionId, ct);
    }
}
