using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>
/// كتب ملف محدد — لمالك الملف ومتابعيه (إحالة/إنابة/استئناف) والمدير/المشرف،
/// ولرئيس نطاق الملف (قسمه لدوائر القسم وبلا دائرة، وشعبته لدوائر شعبته).
/// </summary>
public sealed record ListReviewLettersByDocumentQuery(
    int DocumentId,
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : IQuery<List<ReviewLetterListItemDto>>;

internal sealed class ListReviewLettersByDocumentQueryHandler
    : QueryHandlerBase<ListReviewLettersByDocumentQuery, List<ReviewLetterListItemDto>>
{
    private readonly IReviewLetterRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IAppealRepository _appeals;
    private readonly IDelegationRepository _delegations;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ListReviewLettersByDocumentQueryHandler(
        IReviewLetterRepository letters,
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IAppealRepository appeals,
        IDelegationRepository delegations,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ListReviewLettersByDocumentQuery, List<ReviewLetterListItemDto>>> behaviors)
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

    protected override async Task<List<ReviewLetterListItemDto>> HandleCoreAsync(
        ListReviewLettersByDocumentQuery query, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(query.DocumentId, ct)
            ?? throw new KeyNotFoundException("الملف غير موجود");

        var isOwnerOrSupervisor = query.Role is UserRole.Manager or UserRole.Admin
            || document.CreatedById == query.ActorUserId;

        if (!isOwnerOrSupervisor &&
            !await ReviewLetterService.FollowsDocumentAsync(
                _appeals, _delegations, query.DocumentId, query.ActorUserId, ct) &&
            !await InFileScopeAsync(document, query.Role, query.ActorBranchId, query.ActorSectionId, ct))
        {
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على كتب هذا الملف");
        }

        var letters = await _letters.ListByDocumentAsync(query.DocumentId, ct);
        return letters
            .Select(l => ReviewLetterService.ToListItem(
                l,
                revealUnseen: query.Role == UserRole.Lawyer && l.CreatedById == query.ActorUserId,
                _clock,
                _timeZone))
            .ToList();
    }

    /// <summary>
    /// نطاق الملف (§10): القسم لدوائر القسم وبلا دائرة، والشعبة لدوائر شعبته —
    /// بلا تدهور (رمز بلا شعبة مرفوض).
    /// </summary>
    private async Task<bool> InFileScopeAsync(Document document, UserRole role,
        int? actorBranchId, int? actorSectionId, CancellationToken ct)
    {
        // حارس null صريح: مقارنة int?==int? تعدّ null==null صوابًا، فيرى رئيس
        // بلا فرع ملفًا إرثيًا بلا فرع — ثغرة حسابات مشوّهة (مرآة نمط المراسلات).
        if (actorBranchId is null || actorBranchId != document.BranchId)
            return false;
        var ownerSection = await ReviewLetterService.FileOwnerSectionAsync(document, _circuits, ct);
        return role switch
        {
            UserRole.Head => ownerSection is null,
            UserRole.SubHead => actorSectionId is not null && ownerSection == actorSectionId,
            _ => false,
        };
    }
}
