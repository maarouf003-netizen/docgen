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

        var ordered = filtered.OrderByDescending(s => s.CreatedAtUtc).ToList();
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * perPage).Take(perPage).Select(ToParentEditSuggestionDto).ToList();
        return new PagedResult<ParentEditSuggestionDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto?> ReviewParentEditSuggestionAsync(
        int suggestionId,
        ReviewParentEditSuggestionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role is not (UserRole.Manager or UserRole.Admin))
            throw new UnauthorizedAccessException("القرار على اقتراحات الجهة الأم متاح للمدير/المشرف فقط");

        var suggestion = await _suggestions.GetByIdAsync(suggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا");

        var status = Required(request.Status, "الحالة مطلوبة", 20);
        if (status is not (ParentEditSuggestionStatusCatalog.Approved or ParentEditSuggestionStatusCatalog.Rejected))
            throw new ArgumentException("الحالة يجب أن تكون approved أو rejected");
        var reviewReason = NormalizeOptional(request.ReviewReason);
        if (status == ParentEditSuggestionStatusCatalog.Rejected)
            reviewReason = Required(reviewReason, "سبب الرفض مطلوب", 500);

        // حوكمة S1: لا يُعتمد اسم مقترح لم يُطبَّق بعد — مدراء/مشرفون يجيزون التغيير المنجز
        // فعليًا على الأرض، والواجهة تطبّقه قبل المراجعة (needsRename)؛ المسار المباشر يُجبَر هنا.
        var proposedName = NormalizeOptional(suggestion.ProposedCanonicalName);
        if (status == ParentEditSuggestionStatusCatalog.Approved && proposedName is not null)
        {
            var liveGroup = await _entities.GetGroupAsync(suggestion.GroupId, ct)
                ?? throw new ArgumentException("المجموعة غير موجودة");
            if (ArabicNameNormalizer.Normalize(proposedName) != ArabicNameNormalizer.Normalize(liveGroup.CanonicalName))
                throw new ArgumentException("اعتماد اقتراح باسم غير مُطبَّق مرفوض — طبّق إعادة التسمية أولًا ثم أعد المراجعة");
        }

        suggestion.Status = status;
        suggestion.ReviewedById = actor.UserId;
        suggestion.ReviewReason = reviewReason;
        suggestion.ReviewedAtUtc = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "review_parent_edit_suggestion",
                details: $"{(status == ParentEditSuggestionStatusCatalog.Approved ? "قبل" : "رفض")} اقتراح تعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»{($" — {reviewReason}")}",
                ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
    }
    /// <inheritdoc/>
    public async Task<ParentEditSuggestionDto?> WithdrawParentEditSuggestionAsync(
        int suggestionId,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (actor.Role is not (UserRole.Head or UserRole.SubHead))
            throw new UnauthorizedAccessException("سحب اقتراح الجهة الأم متاح لمنشئه رئيس القسم أو الشعبة فقط");

        var suggestion = await _suggestions.GetByIdAsync(suggestionId, ct);
        if (suggestion is null)
            return null;
        if (suggestion.CreatedById != actor.UserId)
            throw new UnauthorizedAccessException("لا يمكنك سحب اقتراح منشأ من رئيس آخر");
        if (suggestion.Status != ParentEditSuggestionStatusCatalog.Pending)
            throw new ArgumentException("الاقتراح لم يعد معلّقًا فلا يُسحب");

        suggestion.Status = ParentEditSuggestionStatusCatalog.Withdrawn;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "withdraw_parent_edit_suggestion",
                details: $"سحب اقتراحه لتعديل الجهة الأم «{suggestion.Group?.CanonicalName ?? suggestion.Entry?.Group?.CanonicalName}»",
                ct: token);
        }, ct);

        return ToParentEditSuggestionDto(suggestion);
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
