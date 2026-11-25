using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>
/// قائمة المراسلات بحسب الدور: الطرف (منشئ/مستلم) مراسلاته، ورئيس القسم مراسلات
/// محافظته (فرعه + العامة من مندوبيها)، ورئيس الشعبة مراسلات نطاقه (طرفٌ فيها
/// أو ملك دائرة شعبته)، والمدير/المشرف بمحافظة منتقاة إجباريًا.
/// </summary>
public sealed record SearchCorrespondencesQuery(
    int ActorUserId, UserRole Role, int? ActorBranchId, string? Q, string? Governorate,
    string? Importance, int Page, int PerPage, int? ActorSectionId = null)
    : IQuery<PagedResult<CorrespondenceListItemDto>>;

internal sealed class SearchCorrespondencesQueryHandler
    : QueryHandlerBase<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>>
{
    private readonly ICorrespondenceRepository _letters;
    private readonly IBranchRepository _branches;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public SearchCorrespondencesQueryHandler(
        ICorrespondenceRepository letters,
        IBranchRepository branches,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<SearchCorrespondencesQuery, PagedResult<CorrespondenceListItemDto>>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _branches = branches;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override Task<PagedResult<CorrespondenceListItemDto>> HandleCoreAsync(
        SearchCorrespondencesQuery query, CancellationToken ct)
        => SearchCoreAsync(
            query.ActorUserId, query.Role, query.ActorBranchId, query.Q, query.Governorate,
            query.Importance, query.Page, query.PerPage, ct, query.ActorSectionId);

    private async Task<PagedResult<CorrespondenceListItemDto>> SearchCoreAsync(
        int actorUserId, UserRole role, int? actorBranchId, string? q, string? governorate,
        string? importance, int page, int perPage, CancellationToken ct,
        int? actorSectionId = null)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);
        var importanceFilter = NormalizeImportanceFilter(importance);

        // المدير/المشرف لا يرى أي مراسلة قبل اختيار المحافظة (حجب تام على مستوى الخدمة).
        if (role is UserRole.Manager or UserRole.Admin
            && string.IsNullOrWhiteSpace(governorate))
        {
            return new PagedResult<CorrespondenceListItemDto>
            {
                Page = page,
                PerPage = perPage,
            };
        }

        var (items, totalCount) = role switch
        {
            UserRole.Lawyer or UserRole.EntityManager
                => await _letters.SearchForPartyAsync(actorUserId, q, importanceFilter, page, perPage, ct),
            UserRole.Head when actorBranchId is not null
                => await _letters.SearchForBranchAsync(
                    actorBranchId.Value,
                    await CorrespondenceService.ResolveHeadGovernorateAsync(_branches, actorBranchId.Value, ct),
                    q, importanceFilter, page, perPage, ct),
            UserRole.Head
                => throw new UnauthorizedAccessException("رئيس القسم دون فرع لا يمكنه عرض المراسلات"),
            // نطاق الشعبة (§10): طرفٌ فيها أو ملك دائرة شعبته — بلا تدهور.
            UserRole.SubHead when actorBranchId is not null && actorSectionId is not null
                => await _letters.SearchForScopeAsync(
                    actorUserId, actorBranchId.Value, actorSectionId.Value,
                    q, importanceFilter, page, perPage, ct),
            UserRole.SubHead
                => throw new UnauthorizedAccessException("حسابك بلا شعبة — أعد الدخول"),
            UserRole.Manager or UserRole.Admin
                => await _letters.SearchAllAsync(governorate, q, importanceFilter, page, perPage, ct),
            _ => throw new UnauthorizedAccessException("الدور غير مخوّل لعرض المراسلات"),
        };

        return new PagedResult<CorrespondenceListItemDto>
        {
            Items = items
                .Select(l => CorrespondenceService.ToListItem(l, actorUserId, _clock, _timeZone))
                .ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
        };
    }

    private static string? NormalizeImportanceFilter(string? importance)
    {
        if (string.IsNullOrWhiteSpace(importance))
            return null;
        var term = importance.Trim().ToLowerInvariant();
        if (!Correspondence.IsValidImportance(term))
            throw new ArgumentException("فلتر الأهمية غير صالح");
        return term;
    }
}
