using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── اقتراح تعديل الجهة الأم (رئيس القسم → تبويب الإدارة) ──

    /// <inheritdoc/>
    public async Task<PagedResult<ParentEditSuggestionDto>> ListParentEditSuggestionsAsync(
        ParentEditSuggestionListQuery query,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var all = await _suggestions.ListAsync(ct);
        var status = NormalizeOptional(query.Status);
        IEnumerable<ParentEditSuggestion> filtered = all
            .Where(s => status is null || s.Status == status)
            .Where(s => query.GroupId is null || s.GroupId == query.GroupId);

        // نطاق الرئيس: اقتراحاته هو فقط (لحالة المعلّق في نافذة فروع جهة محافظته).
        if (actor.Role is UserRole.Head or UserRole.SubHead)
            filtered = filtered.Where(s => s.CreatedById == actor.UserId);

        var ordered = filtered
            .OrderByDescending(s => s.CreatedAtUtc)
            // فاصل حتمي لحدود الصفحات الإزاحية عند تساوي زمن الإنشاء.
            .ThenByDescending(s => s.Id)
            .ToList();
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * perPage).Take(perPage).Select(ToParentEditSuggestionDto).ToList();
        return new PagedResult<ParentEditSuggestionDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
    private static ParentEditSuggestionDto ToParentEditSuggestionDto(ParentEditSuggestion s)
    {
        var canonical = s.Entry?.Group?.CanonicalName ?? s.Group?.CanonicalName ?? string.Empty;
        return new ParentEditSuggestionDto(
            s.Id,
            s.GroupId,
            s.EntryId,
            canonical,
            s.Entry?.Group?.EntityType ?? s.Group?.EntityType ?? string.Empty,
            s.ProposedCanonicalName,
            s.ProposedEntityType,
            s.ProposedCitationFormula,
            s.Reason,
            s.Status,
            s.CreatedById,
            s.CreatedBy?.FullName ?? s.CreatedBy?.Username ?? string.Empty,
            s.CreatedBranchId,
            s.ReviewedById,
            s.ReviewReason,
            s.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            s.ReviewedAtUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture));
    }
}
