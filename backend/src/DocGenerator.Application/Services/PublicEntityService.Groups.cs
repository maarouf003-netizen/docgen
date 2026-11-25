using DocGenerator.Application.Common.Interfaces;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── مساعدات عدّ قراءة معاينات الهوية الأم (P4-01cg) ──
    // تبقى هنا مصدرًا واحدًا يستهلكه معالجا المعاينة، بلا أي كتابة ولا معاملة.

    internal static async Task<int> CountDocumentsForGroupAsync(
        IPublicEntityRepository entities, int groupId, CancellationToken token)
    {
        var entries = await entities.ListEntriesByGroupAsync(groupId, token);
        var ids = new HashSet<int>();
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            foreach (var d in await entities.ListDocumentsLinkedToEntryAsync(entry.Id, token))
                ids.Add(d.Id);
        }
        return ids.Count;
    }

    internal static async Task<List<string>> BranchNamesForGroupAsync(
        IPublicEntityRepository entities, int groupId, CancellationToken token)
    {
        var entries = await entities.ListEntriesByGroupAsync(groupId, token);
        var branches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(entry.BranchName) && entry.BranchName != DefaultBranchName)
                branches.Add(entry.BranchName);
        }
        return branches.OrderBy(x => x).ToList();
    }

    internal static async Task<int> CountActiveEntriesForGroupsAsync(
        IPublicEntityRepository entities, IReadOnlyCollection<int> groupIds, CancellationToken token)
    {
        var count = 0;
        foreach (var id in groupIds)
            count += (await entities.ListEntriesByGroupAsync(id, token)).Count(e => e.IsActive);
        return count;
    }
}
