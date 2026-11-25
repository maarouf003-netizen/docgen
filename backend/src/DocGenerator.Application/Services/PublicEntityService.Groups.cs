using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── أداة إعادة تسمية الهوية الأم (المدير/المشرف — مستوى المجموعة) ──
    // القاعدة: إعادة التسمية على مستوى المجموعة لا تمس Governorate/BranchName/CitationFormula/CoverageLabel
    // — تغيّر Group.CanonicalName فقط، وتُحفظ الأسماء القديمة أسماءً بديلة (حجّة قانونية د5).

    /// <inheritdoc/>
    public async Task<RenameGroupPreviewResponse> PreviewRenameGroupAsync(
        RenameGroupPreviewRequest request, CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(request.GroupId, ct)
            ?? throw new ArgumentException("الهوية الأم غير موجودة");
        var newName = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        if (group.Entries.Any(e => e.NeedsReview))
            throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

        var affected = await CountDocumentsForGroupAsync(request.GroupId, ct);
        var branches = await BranchNamesForGroupAsync(request.GroupId, ct);
        return new RenameGroupPreviewResponse(group.CanonicalName, newName, affected, branches);
    }
    // ── أداة الحلول (إلغاء عدة هويات أم واستبدالها بهوية جديدة) ──

    /// <inheritdoc/>
    public async Task<AbolishReplacePreviewResponse> PreviewAbolishAndReplaceAsync(
        AbolishReplacePreviewRequest request, CancellationToken ct = default)
    {
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var names = new List<string>();
        var affectedDocs = 0;
        var branches = new HashSet<string>(StringComparer.Ordinal);
        var abolishedGroupIds = new HashSet<int>(request.AbolishedGroupIds);

        foreach (var id in request.AbolishedGroupIds.Distinct())
        {
            var group = await _entities.GetGroupAsync(id, ct)
                ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException($"الهوية الأم «{group.CanonicalName}» غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{group.CanonicalName}» قبل الإلغاء");
            names.Add(group.CanonicalName);
            affectedDocs += await CountDocumentsForGroupAsync(id, ct);
            foreach (var b in await BranchNamesForGroupAsync(id, ct))
                branches.Add(b);
        }

        var delegates = await _users.ListEntityManagersByGroupIdsAsync(abolishedGroupIds, ct);
        return new AbolishReplacePreviewResponse(
            names, request.AbolishedGroupIds.Count,
            await CountActiveEntriesForGroupsAsync(abolishedGroupIds, ct),
            affectedDocs, delegates.Count, branches.OrderBy(x => x).ToList());
    }
    private async Task<int> CountDocumentsForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var ids = new HashSet<int>();
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            foreach (var d in await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token))
                ids.Add(d.Id);
        }
        return ids.Count;
    }
    private async Task<List<string>> BranchNamesForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var branches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(entry.BranchName) && entry.BranchName != DefaultBranchName)
                branches.Add(entry.BranchName);
        }
        return branches.OrderBy(x => x).ToList();
    }
    private async Task<int> CountActiveEntriesForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token)
    {
        var count = 0;
        foreach (var id in groupIds)
            count += (await _entities.ListEntriesByGroupAsync(id, token)).Count(e => e.IsActive);
        return count;
    }
}
