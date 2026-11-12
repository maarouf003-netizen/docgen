using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public class PublicEntityRepository : IPublicEntityRepository
{
    private readonly DocGeneratorDbContext _db;

    public PublicEntityRepository(DocGeneratorDbContext db) => _db = db;

    public Task<List<PublicEntityGroup>> ListGroupsWithEntriesAsync(CancellationToken ct = default)
        => _db.PublicEntityGroups.AsNoTracking()
            .Include(g => g.Entries).ThenInclude(e => e.Aliases)
            .Include(g => g.Entries).ThenInclude(e => e.CreatedBy)
            .OrderBy(g => g.CanonicalName)
            .ToListAsync(ct);

    public Task<int> CountNeedsReviewAsync(int? headBranchId, CancellationToken ct = default)
        => _db.PublicEntities.AsNoTracking()
            .Where(e => e.NeedsReview && (headBranchId == null
                || (e.CreatedBy != null && e.CreatedBy.BranchId == headBranchId)))
            .CountAsync(ct);

    public Task<List<PublicEntityGroup>> ListGroupsTrackedAsync(CancellationToken ct = default)
        => _db.PublicEntityGroups.OrderBy(g => g.Id).ToListAsync(ct);

    public async Task<PublicEntity?> GetEntryAsync(int entryId, CancellationToken ct = default)
        => await _db.PublicEntities.FirstOrDefaultAsync(e => e.Id == entryId, ct);

    public Task<PublicEntity?> GetEntryWithDetailsAsync(int entryId, CancellationToken ct = default)
        => _db.PublicEntities
            .Include(e => e.Group).ThenInclude(g => g.Entries).ThenInclude(x => x.Aliases)
            .Include(e => e.CreatedBy)
            .FirstOrDefaultAsync(e => e.Id == entryId, ct);

    public Task<PublicEntityGroup?> GetGroupAsync(int groupId, CancellationToken ct = default)
        => _db.PublicEntityGroups.FirstOrDefaultAsync(g => g.Id == groupId, ct);

    public Task<bool> EntryExistsAsync(int groupId, string governorate, string branchName, CancellationToken ct = default)
        => _db.PublicEntities.AsNoTracking()
            .AnyAsync(e => e.GroupId == groupId && e.Governorate == governorate && e.BranchName == branchName, ct);

    public async Task AddGroupAsync(PublicEntityGroup group, CancellationToken ct = default)
        => await _db.PublicEntityGroups.AddAsync(group, ct);

    public async Task AddEntryAsync(PublicEntity entry, CancellationToken ct = default)
        => await _db.PublicEntities.AddAsync(entry, ct);

    // ── تنبيه المراجعة: رؤساء الأقسام والشعب النشطون لمحافظة محددة (F3: تماثل §2) ──

    public Task<List<User>> ListActiveHeadsByGovernorateAsync(string governorate, CancellationToken ct = default)
        => _db.Users.AsNoTracking()
            .Include(u => u.Branch)
            .Where(u => (u.Role == UserRole.Head || u.Role == UserRole.SubHead) && u.IsActive
                && u.Branch != null && u.Branch.Governorate == governorate)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

    public Task<List<User>> ListActiveHeadsByBranchAsync(int branchId, CancellationToken ct = default)
        => _db.Users.AsNoTracking()
            .Include(u => u.Branch)
            .Where(u => (u.Role == UserRole.Head || u.Role == UserRole.SubHead) && u.IsActive
                && u.BranchId == branchId)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

    // ── الاستيراد (د12): نصوص متمايزة مع عدّاد ملفاتها، والتطبيع يجري في الذاكرة ──

    public async Task<List<(string Name, string? Governorate, int DocumentCount)>> ListDistinctApplicantTextsAsync(CancellationToken ct = default)
    {
        var rows = await _db.ApplicantPublicEntities.AsNoTracking()
            .Where(a => a.Name != null && a.Name != "")
            .GroupBy(a => new { a.Name, a.Governorate })
            .Select(g => new { g.Key.Name, g.Key.Governorate, DocumentCount = g.Select(x => x.DocumentId).Distinct().Count() })
            .ToListAsync(ct);
        return rows.Select(r => (r.Name!, r.Governorate, r.DocumentCount)).ToList();
    }

    public async Task<List<(string EntityName, string? Governorate, int DocumentCount)>> ListDistinctExecutedTextsAsync(CancellationToken ct = default)
    {
        var rows = await _db.ExecutedPublicEntities.AsNoTracking()
            .Where(e => e.EntityName != null && e.EntityName != ""
                && e.EntityNature == PartyNatureCatalog.PublicEntity)
            .GroupBy(e => new { e.EntityName, e.Governorate })
            .Select(g => new { g.Key.EntityName, g.Key.Governorate, DocumentCount = g.Select(x => x.DocumentId).Distinct().Count() })
            .ToListAsync(ct);
        return rows.Select(r => (r.EntityName!, r.Governorate, r.DocumentCount)).ToList();
    }

    // ── مزامنة النصوص عند إعادة التسمية (د5) ──
    //
    // إعادة بناء SearchText تمرّ على كل مجموعات الملف (ورثة/كفلاء/طالبو تنفيذ/منفذ
    // عليهم/ورثة الجهات/المتدخلون)، لذا يجب تحميلها كاملة مع الملف وإلا فقدت توكنات
    // بحث غير متأثرة بإعادة التسمية. SplitQuery مفعّل على السياق فيجعل التحميل المتعدد فعالًا.

    public Task<List<ApplicantPublicEntity>> ListApplicantRowsByNamesAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default)
        => _db.ApplicantPublicEntities
            .Include(a => a.Document).ThenInclude(d => d.ApplicantPublicEntities)
            .Include(a => a.Document).ThenInclude(d => d.Heirs)
            .Include(a => a.Document).ThenInclude(d => d.Guarantors)
            .Include(a => a.Document).ThenInclude(d => d.ExecutionApplicants)
            .Include(a => a.Document).ThenInclude(d => d.ExecutedPublicEntities)
            .Include(a => a.Document).ThenInclude(d => d.ExecutedNaturalPersons)
            .Include(a => a.Document).ThenInclude(d => d.ExecutedHeirs)
            .Include(a => a.Document).ThenInclude(d => d.Interventions).ThenInclude(i => i.Heirs)
            .Where(a => a.Name != null && names.Contains(a.Name))
            .ToListAsync(ct);

    public Task<List<ExecutedPublicEntity>> ListExecutedRowsByNamesAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default)
        => _db.ExecutedPublicEntities
            .Include(e => e.Document).ThenInclude(d => d.ApplicantPublicEntities)
            .Include(e => e.Document).ThenInclude(d => d.Heirs)
            .Include(e => e.Document).ThenInclude(d => d.Guarantors)
            .Include(e => e.Document).ThenInclude(d => d.ExecutionApplicants)
            .Include(e => e.Document).ThenInclude(d => d.ExecutedPublicEntities)
            .Include(e => e.Document).ThenInclude(d => d.ExecutedNaturalPersons)
            .Include(e => e.Document).ThenInclude(d => d.ExecutedHeirs)
            .Include(e => e.Document).ThenInclude(d => d.Interventions).ThenInclude(i => i.Heirs)
            .Where(e => e.EntityName != null && e.EntityNature == PartyNatureCatalog.PublicEntity
                && names.Contains(e.EntityName))
            .ToListAsync(ct);

    // ── نقل القيد (د3) ──

    public async Task<List<Document>> ListDocumentsLinkedToEntryAsync(int entryId, CancellationToken ct = default)
    {
        var ids = new List<int>();
        ids.AddRange(await _db.ApplicantPublicEntities.AsNoTracking()
            .Where(a => a.RegistryId == entryId)
            .Select(a => a.DocumentId)
            .Distinct()
            .ToListAsync(ct));
        ids.AddRange(await _db.ExecutedPublicEntities.AsNoTracking()
            .Where(e => e.RegistryId == entryId)
            .Select(e => e.DocumentId)
            .Distinct()
            .ToListAsync(ct));
        var uniqueIds = ids.Distinct().ToList();
        if (uniqueIds.Count == 0)
            return new List<Document>();
        return await WithSearchTextCollections(_db.Documents)
            .Where(d => uniqueIds.Contains(d.Id))
            .ToListAsync(ct);
    }

    public async Task<List<Document>> ListDocumentsByPublicInterventionNameAsync(
        string name, CancellationToken ct = default)
    {
        var norm = ArabicNameNormalizer.Normalize(name);
        if (norm.Length == 0)
            return new List<Document>();

        // اللقطة مجمّدة بلا عمود معياري: تُطبَّع في الذاكرة كما تفعل مزامنة صفوف الأطراف.
        // إعادة التسمية عملية نادرة، فلا يضر تحميل أسماء المتدخلين الجهات العامة فقط.
        var candidates = await _db.DocumentInterventions.AsNoTracking()
            .Where(i => i.Nature == PartyNatureCatalog.PublicEntity && i.EntityName != null)
            .Select(i => new { i.DocumentId, i.EntityName })
            .ToListAsync(ct);
        var ids = candidates
            .Where(i => ArabicNameNormalizer.Normalize(i.EntityName!) == norm)
            .Select(i => i.DocumentId)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
            return new List<Document>();

        return await WithSearchTextCollections(_db.Documents)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);
    }

    /// <summary>
    /// شجرة التحميل اللازمة لإعادة بناء SearchText بعد تغيير جهة: كل المجموعات التي
    /// يقرؤها الباني، ومنها المتدخلون وورثتهم — فلا تُفقد توكنات بحث غير متأثرة بالتغيير.
    /// </summary>
    private static IQueryable<Document> WithSearchTextCollections(IQueryable<Document> q) => q
        .Include(d => d.ApplicantPublicEntities).ThenInclude(a => a.Registry).ThenInclude(r => r!.Group)
        .Include(d => d.ExecutedPublicEntities).ThenInclude(e => e.Registry).ThenInclude(r => r!.Group)
        .Include(d => d.Heirs)
        .Include(d => d.Guarantors)
        .Include(d => d.ExecutionApplicants)
        .Include(d => d.ExecutedNaturalPersons)
        .Include(d => d.ExecutedHeirs)
        .Include(d => d.Interventions).ThenInclude(i => i.Heirs);

    public Task<PublicEntity?> FindEntryInGroupAsync(int groupId, string governorate, string branchName, CancellationToken ct = default)
        => _db.PublicEntities.FirstOrDefaultAsync(
            e => e.GroupId == groupId && e.Governorate == governorate && e.BranchName == branchName, ct);

    // ── الدمج (د5 §4) ──

    public async Task<List<PublicEntity>> ListEntriesByGroupAsync(int groupId, CancellationToken ct = default)
    {
        return await _db.PublicEntities
            .Include(e => e.Aliases)
            .Include(e => e.CreatedBy)
            .Where(e => e.GroupId == groupId)
            .ToListAsync(ct);
    }

    public Task<List<PublicEntityChangeEvent>> ListChangeEventsAsync(CancellationToken ct = default)
        => _db.PublicEntityChangeEvents.AsNoTracking()
            .Include(c => c.ActorUser)
            .Include(c => c.Entry)
            .Include(c => c.Group)
            .ThenInclude(g => g!.Entries)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Dictionary<int, int>> CountLinkedDocumentsByGroupIdsAsync(
        IReadOnlyCollection<int> groupIds, CancellationToken ct = default)
    {
        var ids = groupIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, int>();

        // كل صف ربط يحدد groupId عبر قيد الجهة (PublicEntities.GroupId) — نجمع عدّاد الملفات
        // المميّزة لكل مجموعة من جدولي الطرفين (طالبة/منفذ عليها) دفعة واحدة. تُترجم Contains إلى IN.
        var applicantGroupDocs = await _db.ApplicantPublicEntities.AsNoTracking()
            .Where(a => a.RegistryId != null && ids.Contains(a.Registry!.GroupId))
            .Select(a => new { a.DocumentId, a.Registry!.GroupId })
            .ToListAsync(ct);
        var executedGroupDocs = await _db.ExecutedPublicEntities.AsNoTracking()
            .Where(e => e.RegistryId != null && ids.Contains(e.Registry!.GroupId))
            .Select(e => new { e.DocumentId, e.Registry!.GroupId })
            .ToListAsync(ct);

        var countsByGroup = ids.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var row in applicantGroupDocs)
            if (countsByGroup.TryGetValue(row.GroupId, out var set))
                set.Add(row.DocumentId);
        foreach (var row in executedGroupDocs)
            if (countsByGroup.TryGetValue(row.GroupId, out var set))
                set.Add(row.DocumentId);

        return countsByGroup.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }

    public async Task<Dictionary<int, int>> CountLinkedDocumentsByEntryIdsAsync(
        IReadOnlyCollection<int> entryIds, CancellationToken ct = default)
    {
        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<int, int>();

        // كل صف ربط يحدد القيد عبر RegistryId مباشرة — نجمع عدّاد الملفات المميّزة لكل قيد
        // من جدولي الطرفين (طالبة/منفذ عليها) دفعة واحدة. تُترجم Contains إلى IN.
        var applicantEntryDocs = await _db.ApplicantPublicEntities.AsNoTracking()
            .Where(a => a.RegistryId != null && ids.Contains(a.RegistryId.Value))
            .Select(a => new { a.DocumentId, a.RegistryId })
            .ToListAsync(ct);
        var executedEntryDocs = await _db.ExecutedPublicEntities.AsNoTracking()
            .Where(e => e.RegistryId != null && ids.Contains(e.RegistryId.Value))
            .Select(e => new { e.DocumentId, e.RegistryId })
            .ToListAsync(ct);

        var countsByEntry = ids.ToDictionary(id => id, _ => new HashSet<int>());
        foreach (var row in applicantEntryDocs)
            if (countsByEntry.TryGetValue(row.RegistryId!.Value, out var set))
                set.Add(row.DocumentId);
        foreach (var row in executedEntryDocs)
            if (countsByEntry.TryGetValue(row.RegistryId!.Value, out var set))
                set.Add(row.DocumentId);

        return countsByEntry.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }
}
