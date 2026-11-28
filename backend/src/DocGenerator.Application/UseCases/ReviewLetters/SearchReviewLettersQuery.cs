using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.ReviewLetters;

/// <summary>
/// قائمة كتب المطالعة بحسب الدور: المحامي كتبه، ورئيس القسم كتب قسمه
/// (دوائر القسم وبلا دائرة)، ورئيس الشعبة كتب شعبته، والمدير/المشرف كتب
/// فرع الإدارة المنتقى (لا عرض قبل اختيار الفرع).
/// </summary>
public sealed record SearchReviewLettersQuery(
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    string? Q,
    int Page,
    int PerPage,
    string? AdministrativeBranch,
    int? ActorSectionId = null) : IQuery<PagedResult<ReviewLetterListItemDto>>;

internal sealed class SearchReviewLettersQueryHandler
    : QueryHandlerBase<SearchReviewLettersQuery, PagedResult<ReviewLetterListItemDto>>
{
    private readonly IReviewLetterRepository _letters;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchReviewLettersQueryHandler(
        IReviewLetterRepository letters,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchReviewLettersQuery, PagedResult<ReviewLetterListItemDto>>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<PagedResult<ReviewLetterListItemDto>> HandleCoreAsync(
        SearchReviewLettersQuery query, CancellationToken ct)
    {
        var (page, perPage) = PageRequest.Normalize(query.Page, query.PerPage);

        // المدير/المشرف لا يرى أي كتاب قبل اختيار فرع الإدارة (حجب تام على مستوى الاستعلام).
        if (query.Role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(query.AdministrativeBranch))
        {
            return new PagedResult<ReviewLetterListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = query.Role switch
        {
            UserRole.Lawyer => await _letters.SearchForLawyerAsync(
                query.ActorUserId, query.Q, page, perPage, ct),
            // نطاق المالك (§10): القسم لدوائر القسم وبلا دائرة، والشعبة لدوائر شعبته.
            UserRole.Head when query.ActorBranchId is not null
                => await _letters.SearchForScopeAsync(
                    query.ActorBranchId.Value, null, query.Q, page, perPage, ct),
            UserRole.Head
                => throw new UnauthorizedAccessException("رئيس القسم دون فرع لا يمكنه عرض كتب المطالعة"),
            UserRole.SubHead when query.ActorBranchId is not null && query.ActorSectionId is not null
                => await _letters.SearchForScopeAsync(
                    query.ActorBranchId.Value, query.ActorSectionId, query.Q, page, perPage, ct),
            UserRole.SubHead
                => throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(query.AdministrativeBranch, query.Q, page, perPage, ct),
            _ => throw new UnauthorizedAccessException("الدور غير مخوّل لعرض كتب المطالعة"),
        };

        // حالة الإطلاع (هل طالع محامي الكتاب الرد؟) معلومة خاصة بمحامي الكتاب نفسه،
        // فلا تُكشف لرئيس القسم ولا للإدارة حفاظًا على خصوصية المحامي.
        var revealUnseen = query.Role == UserRole.Lawyer;

        return new PagedResult<ReviewLetterListItemDto>
        {
            Items = items
                .Select(l => ReviewLetterService.ToListItem(l, revealUnseen, _clock, _timeZone))
                .ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }
}
