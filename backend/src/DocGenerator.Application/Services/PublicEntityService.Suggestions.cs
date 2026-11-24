using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── اقتراح تعديل الجهة الأم (رئيس القسم → تبويب الإدارة) ──

    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto> SuggestParentEditAsync(
        int entryId,
        SuggestParentEditRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role is not (UserRole.Head or UserRole.SubHead))
            throw new UnauthorizedAccessException("اقتراح تعديل الجهة الأم متاح لرئيس القسم أو الشعبة فقط");
        if (!actor.BranchId.HasValue)
            throw new UnauthorizedAccessException("حسابك غير مرتبط بفرع لتقديم اقتراح");

        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct)
            ?? throw new ArgumentException("القيد غير موجود");
        if (!entry.IsParentEntity)
            throw new ArgumentException("الاقتراح يخص قيد «الجهة الأم» فقط");
        var group = entry.Group;
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        // شرط النطاق: للمجموعة فرع نشط في محافظة فرع الرئيس.
        var headBranch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var headGov = NormalizeOptional(headBranch?.Governorate);
        if (headGov is null || !group.Entries.Any(e => e.IsActive && e.Governorate == headGov))
            throw new UnauthorizedAccessException("لا توجد فروع نشطة لهذه الجهة في محافظة فرعك");

        var proposedCanonical = NormalizeOptional(request.ProposedCanonicalName);
        if (proposedCanonical is not null)
        {
            if (ArabicNameNormalizer.Normalize(proposedCanonical) == ArabicNameNormalizer.Normalize(group.CanonicalName))
                throw new ArgumentException("الاسم المقترح مطابق للاسم الحالي");
            await EnsureCanonicalAvailableAsync(proposedCanonical, group.Id, ct);
        }
        var proposedType = NormalizeOptional(request.ProposedEntityType) is { } pt ? ValidEntityType(pt) : null;
        var proposedCitation = NormalizeOptional(request.ProposedCitationFormula) is { } pc ? ValidCitationFormula(pc, CitationFormulaCatalog.AddToJob) : null;
        var reason = Required(request.Reason, "سبب الاقتراح مطلوب", 500);

        // منع المكرر المعلّق (GroupId × فرع الرئيس) — الانعكاس خادمي وفهرس فريد جزئي يعزّزه.
        var all = await _suggestions.ListAsync(ct);
        if (all.Any(s => s.Status == ParentEditSuggestionStatusCatalog.Pending
            && s.GroupId == group.Id
            && s.CreatedBranchId == actor.BranchId.Value))
            throw new ArgumentException("يوجد اقتراح معلّق بالفعل لهذه الجهة من فرعك");

        var suggestion = new ParentEditSuggestion
        {
            GroupId = group.Id,
            EntryId = entry.Id,
            ProposedCanonicalName = proposedCanonical,
            ProposedEntityType = proposedType,
            ProposedCitationFormula = proposedCitation,
            Reason = reason,
            Status = ParentEditSuggestionStatusCatalog.Pending,
            CreatedById = actor.UserId,
            CreatedBranchId = actor.BranchId.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _suggestions.AddAsync(suggestion, token);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Propose,
                groupId = group.Id,
                entryId = entry.Id,
                canonicalName = group.CanonicalName,
                proposedCanonicalName = proposedCanonical,
                proposedEntityType = proposedType,
                proposedCitationFormula = proposedCitation,
                reason,
                createdBranchId = actor.BranchId,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Propose,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "suggest_parent_edit",
                details: $"اقترح تعديل الجهة الأم «{group.CanonicalName}»{($" — {proposedCanonical}")} {reason}", ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
    }
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
