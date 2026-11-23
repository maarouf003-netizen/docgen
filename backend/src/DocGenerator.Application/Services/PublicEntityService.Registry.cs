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
    // ── القراءة ──

    public async Task<PagedResult<PublicEntityEntryDto>> ListAsync(EntityRegistryListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(query.Q);
        var governorate = NormalizeOptional(query.Governorate);
        var status = NormalizeOptional(query.Status);
        var branchName = NormalizeOptional(query.BranchName);

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var entries = groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => query.IncludePending || x.Entry.Status != EntityStatusCatalog.Pending)
            .Where(x => query.IncludeInactive || x.Entry.IsActive)
            .Where(x => governorate is null || x.Entry.IsParentEntity || x.Entry.Governorate == governorate)
            .Where(x => status is null || x.Entry.Status == status)
            .Where(x => branchName is null || x.Entry.IsParentEntity || x.Entry.BranchName == branchName)
            .Where(x => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(x.Group.CanonicalName).Contains(qNorm)
                || x.Entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm)))
            .OrderBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.Governorate, StringComparer.Ordinal)
            .ThenBy(x => x.Entry.BranchName, StringComparer.Ordinal)
            // فاصل حتمي: مجموعتان بالاسم/المحافظة/الفرع نفسهما (أو حقول فارغة) تترك
            // حدود الصفحات غير حتمية بلا هذا الفاصل.
            .ThenBy(x => x.Entry.Id)
            .ToList();

        var result = new PagedResult<PublicEntityEntryDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = entries.Count,
        };
        result.Items = entries
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Select(x => ToEntryDto(x.Group, x.Entry))
            .ToList();
        return result;
    }
    /// <inheritdoc/>
    public async Task<PagedResult<PublicEntityGroupDto>> ListGroupsAsync(EntityGroupListQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var qNorm = ArabicNameNormalizer.Normalize(query.Q);
        var governorate = NormalizeOptional(query.Governorate);
        var excludeIds = query.ExcludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(query.ExcludeIds.Distinct());
        // معرّفات يجب ضمان ظهورها في النتيجة مهما كانت ترتيبها/صفحتها
        // (تُستخدم لنافذة توحيد التسمية لضمان تواجد «الهوية الهدف» السابقة الاختيار).
        var includeIds = query.IncludeIds is null
            ? new HashSet<int>()
            : new HashSet<int>(query.IncludeIds.Distinct());

        // نطاق الرئيس (قسم/شعبة): محافظة فرعه فقط — بلا فرع/محافظة لا يُعرض شيء
        string? headGovernorate = null;
        bool isHead = actor.Role is UserRole.Head or UserRole.SubHead;
        if (isHead)
        {
            if (!actor.BranchId.HasValue)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headGovernorate = NormalizeOptional(branch?.Governorate);
            if (headGovernorate is null)
                return new PagedResult<PublicEntityGroupDto> { Page = page, PerPage = perPage, TotalCount = 0, Items = new List<PublicEntityGroupDto>() };
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var filtered = groups
            .Where(g => g.IsActive)
            .Where(g => !excludeIds.Contains(g.Id))
            .Where(g => !isHead || g.Entries.Any(e => e.IsActive && e.Governorate == headGovernorate))
            .Where(g => governorate is null || g.Entries.Any(e => e.IsActive && e.Governorate == governorate))
            .Where(g => qNorm.Length == 0
                || ArabicNameNormalizer.Normalize(g.CanonicalName).Contains(qNorm)
                || g.Entries.Any(e => e.IsActive && e.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText).Contains(qNorm))))
            .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
            // فاصل حتمي لحدود الصفحات الإزاحية عند تساوي الاسم القانوني.
            .ThenBy(g => g.Id)
            .ToList();

        var totalCount = filtered.Count;
        var pageItems = filtered
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .ToList();

        // ضمان تواجد الهويات المطلوبة (IncludeIds) في النتيجة حتى لو كانت خارج نطاق الصفحة
        // الحالية بسبب الفرز/الترقيم — بشرط أن تكون مرّت من نفس الفلاتر (نشطة، نطاق، بحث).
        if (includeIds.Count > 0)
        {
            var included = filtered.Where(g => includeIds.Contains(g.Id)).ToList();
            if (included.Count > 0)
            {
                var presentIds = new HashSet<int>(pageItems.Select(g => g.Id));
                pageItems = pageItems
                    .Concat(included.Where(g => !presentIds.Contains(g.Id)))
                    .OrderBy(g => g.CanonicalName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        var pageGroupIds = pageItems.Select(g => g.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(pageGroupIds, ct);

        var dtos = pageItems.Select(g =>
        {
            var scoped = g.Entries.Where(e => e.IsActive);
            if (isHead) scoped = scoped.Where(e => e.Governorate == headGovernorate);
            var scopedList = scoped.ToList();
            return new PublicEntityGroupDto(
                g.Id,
                g.CanonicalName,
                g.EntityType,
                g.IsActive,
                scopedList.Count,
                scopedList.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(),
                linkedCounts.TryGetValue(g.Id, out var count) ? count : 0);
        }).ToList();

        return new PagedResult<PublicEntityGroupDto>
        {
            Page = page,
            PerPage = perPage,
            TotalCount = totalCount,
            Items = dtos,
        };
    }
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PublicEntityEntryDto>> ListEntriesByGroupAsync(int groupId, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive) throw new ArgumentException("المجموعة غير نشطة");

        var entries = await _entities.ListEntriesByGroupAsync(groupId, ct);
        var filtered = entries.Where(e => e.IsActive).ToList();

        // نطاق الرئيس (قسم/شعبة): محافظته فقط — مع ضمان ظهور قيد «الجهة الأم» دائمًا (F4).
        if (actor.Role is UserRole.Head or UserRole.SubHead && actor.BranchId.HasValue)
        {
            var branch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            var gov = NormalizeOptional(branch?.Governorate);
            if (gov is not null)
                filtered = filtered.Where(e => e.IsParentEntity || e.Governorate == gov).ToList();
            else
                filtered = new List<PublicEntity>();
        }

        return filtered
            .OrderBy(e => e.Governorate, StringComparer.Ordinal)
            .ThenBy(e => e.BranchName, StringComparer.Ordinal)
            .Select(e => ToEntryDto(group, e))
            .ToList();
    }
    /// <inheritdoc/>
    public async Task<SimilarToResponse> FindSimilarToGroupAsync(int groupId, double threshold, int maxResults, CancellationToken ct = default)
    {
        var target = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!target.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        var t = threshold <= 0 ? ArabicNameSimilarity.DefaultSimilarToThreshold : Math.Clamp(threshold, 0, 1);
        var max = maxResults <= 0 ? ArabicNameSimilarity.DefaultMaxSimilarResults : Math.Min(maxResults, 50);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);

        var ranked = groups
            .Where(g => g.IsActive && g.Id != groupId)
            .Select(g => (Group: g, Sim: ArabicNameSimilarity.Similarity(target.CanonicalName, g.CanonicalName)))
            .Where(x => x.Sim >= t)
            .OrderByDescending(x => x.Sim)
            .ThenBy(x => x.Group.CanonicalName, StringComparer.Ordinal)
            .Take(max)
            .ToList();

        var ids = ranked.Select(r => r.Group.Id).ToList();
        var linkedCounts = await _entities.CountLinkedDocumentsByGroupIdsAsync(ids, ct);

        var items = ranked.Select(r => new SimilarToItemDto(
            r.Group.Id,
            r.Group.CanonicalName,
            r.Group.EntityType,
            r.Group.Entries.Count(e => e.IsActive),
            linkedCounts.TryGetValue(r.Group.Id, out var c) ? c : 0,
            Math.Round(r.Sim, 3))).ToList();

        return new SimilarToResponse(target.Id, target.CanonicalName, items, t);
    }

    /// <summary>
    /// قائمة «بحاجة مراجعة»: الرئيس (قسم/شعبة) يرى ما أدخله محامو فرعه (بغض النظر عن محافظة
    /// الجهة نفسها — قد يُقيم محامٍ ملفًا تنفيذيًا على جهة تتبع محافظة أخرى)، والمدير/
    /// المشرف يرىان كل السجل. رئيس بلا فرع مضبوط تعني قائمة فارغة.
    /// </summary>
    public async Task<List<PublicEntityEntryDto>> ListNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return new List<PublicEntityEntryDto>();
        }

        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        return groups
            .SelectMany(g => g.Entries.Select(e => (Group: g, Entry: e)))
            .Where(x => x.Entry.NeedsReview)
            .Where(x => headBranchId is null
                || (x.Entry.CreatedBy != null && x.Entry.CreatedBy.BranchId == headBranchId))
            .OrderByDescending(x => x.Entry.CreatedAt)
            .Select(x => ToEntryDto(x.Group, x.Entry))
            .ToList();
    }
    public async Task<int> CountNeedsReviewAsync(EntityRegistryActor actor, CancellationToken ct = default)
    {
        int? headBranchId = null;
        if (actor.Role is UserRole.Head or UserRole.SubHead)
        {
            var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            headBranchId = branch?.Id;
            if (headBranchId is null)
                return 0;
        }

        return await _entities.CountNeedsReviewAsync(headBranchId, ct);
    }
}
