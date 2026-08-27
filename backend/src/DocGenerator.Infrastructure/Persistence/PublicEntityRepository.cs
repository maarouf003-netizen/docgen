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

    // ── تنبيه المراجعة: رؤساء الأقسام النشطون لمحافظة محددة ──

    public Task<List<User>> ListActiveHeadsByGovernorateAsync(string governorate, CancellationToken ct = default)
        => _db.Users.AsNoTracking()
            .Include(u => u.Branch)
            .Where(u => u.Role == UserRole.Head && u.IsActive
                && u.Branch != null && u.Branch.Governorate == governorate)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

    public Task<List<User>> ListActiveHeadsByBranchAsync(int branchId, CancellationToken ct = default)
        => _db.Users.AsNoTracking()
            .Include(u => u.Branch)
            .Where(u => u.Role == UserRole.Head && u.IsActive
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
    // عليهم/ورثة الجهات)، لذا يجب تحميلها كاملة مع الملف وإلا فقدت توكنات بحث غير
    // متأثرة بإعادة التسمية. SplitQuery مفعّل على السياق فيجعل التحميل المتعدد فعالًا.

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
        return await _db.Documents
            .Include(d => d.ApplicantPublicEntities).ThenInclude(a => a.Registry).ThenInclude(r => r!.Group)
            .Include(d => d.ExecutedPublicEntities).ThenInclude(e => e.Registry).ThenInclude(r => r!.Group)
            .Include(d => d.Heirs)
            .Include(d => d.Guarantors)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .Where(d => uniqueIds.Contains(d.Id))
            .ToListAsync(ct);
    }

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
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);
}
