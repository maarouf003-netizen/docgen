using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>
/// أسماء فروع الإدارة المميزة لفلتر المدير/المشرف — قراءة صرفة بلا نطاق فاعل.
/// </summary>
public sealed record GetReviewLetterAdministrativeBranchesQuery : IQuery<List<string>>;

internal sealed class GetReviewLetterAdministrativeBranchesQueryHandler
    : QueryHandlerBase<GetReviewLetterAdministrativeBranchesQuery, List<string>>
{
    private readonly IReviewLetterRepository _letters;

    public GetReviewLetterAdministrativeBranchesQueryHandler(
        IReviewLetterRepository letters,
        IEnumerable<IPipelineBehavior<GetReviewLetterAdministrativeBranchesQuery, List<string>>> behaviors)
        : base(behaviors)
        => _letters = letters;

    protected override Task<List<string>> HandleCoreAsync(
        GetReviewLetterAdministrativeBranchesQuery query, CancellationToken ct)
        => _letters.GetAdministrativeBranchesAsync(ct);
}
