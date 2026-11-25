using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>كتاب مطالعة برسائله — بعد التحقق من حق الوصول (نطاق المالك §10).</summary>
public sealed record GetReviewLetterQuery(
    int Id,
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : IQuery<ReviewLetterDto>;

internal sealed class GetReviewLetterQueryHandler
    : QueryHandlerBase<GetReviewLetterQuery, ReviewLetterDto>
{
    private readonly IReviewLetterRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetReviewLetterQueryHandler(
        IReviewLetterRepository letters,
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetReviewLetterQuery, ReviewLetterDto>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _documents = documents;
        _circuits = circuits;
        _appeals = appeals;
        _delegations = delegations;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<ReviewLetterDto> HandleCoreAsync(
        GetReviewLetterQuery query, CancellationToken ct)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(query.Id, ct)
            ?? throw new KeyNotFoundException("كتاب المطالعة غير موجود");

        if (!await CanViewAsync(letter, query.ActorUserId, query.Role, query.ActorBranchId,
                query.ActorSectionId, ct))
        {
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتاب المطالعة هذا");
        }

        return ReviewLetterService.ToDto(letter, _clock, _timeZone);
    }

    private async Task<bool> CanViewAsync(ReviewLetter letter, int actorUserId, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
    {
        switch (role)
        {
            case UserRole.Lawyer:
                // محامي الكتاب، أو أي محامٍ يتابع الملف المربوط (إحالة/إنابة/استئناف).
                if (letter.CreatedById == actorUserId)
                    return true;
                return letter.DocumentId is not null
                    && await ReviewLetterService.FollowsDocumentAsync(
                        _appeals, _delegations, letter.DocumentId.Value, actorUserId, ct);

            case UserRole.Head:
                if (letter.BranchId != actorBranchId)
                    return false;
                return await ReviewLetterService.OwnerSectionOfAsync(
                    letter, _documents, _circuits, ct) is null;

            case UserRole.SubHead:
                if (letter.BranchId != actorBranchId || actorSectionId is null)
                    return false;
                return await ReviewLetterService.OwnerSectionOfAsync(
                    letter, _documents, _circuits, ct) == actorSectionId;

            case UserRole.Manager or UserRole.Admin:
                return true;

            default:
                return false;
        }
    }
}
