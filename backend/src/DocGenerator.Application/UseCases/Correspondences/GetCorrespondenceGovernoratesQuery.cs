using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>المحافظات المميزة لفلتر المدير/المشرف.</summary>
public sealed record GetCorrespondenceGovernoratesQuery : IQuery<List<string>>;

internal sealed class GetCorrespondenceGovernoratesQueryHandler
    : QueryHandlerBase<GetCorrespondenceGovernoratesQuery, List<string>>
{
    private readonly ICorrespondenceRepository _letters;

    public GetCorrespondenceGovernoratesQueryHandler(
        ICorrespondenceRepository letters,
        IEnumerable<IPipelineBehavior<GetCorrespondenceGovernoratesQuery, List<string>>> behaviors)
        : base(behaviors)
        => _letters = letters;

    protected override Task<List<string>> HandleCoreAsync(
        GetCorrespondenceGovernoratesQuery query, CancellationToken ct)
        => _letters.GetGovernoratesAsync(ct);
}
