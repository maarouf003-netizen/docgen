using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── اقتراح تعديل الجهة الأم (رئيس القسم → تبويب الإدارة) ──

    internal static ParentEditSuggestionDto ToParentEditSuggestionDto(ParentEditSuggestion s)
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
