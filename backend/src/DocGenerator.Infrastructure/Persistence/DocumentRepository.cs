using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// بحث ترحّلي يُنفَّذ في قاعدة البيانات (Count + Skip/Take) بدل تحميل كل السجلات.
/// </summary>
public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(DocGeneratorDbContext db) : base(db) { }

    public async Task<(int TotalCount, List<Document> Items)> SearchAsync(
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, visibleBranchId, visibleUserId);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<List<Document>> ExportAsync(
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, visibleBranchId, visibleUserId);

        return await q
            .OrderByDescending(d => d.CreatedAt)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .ToListAsync(ct);
    }

    private static IQueryable<Document> ApplySearchFilters(
        IQueryable<Document> q,
        string? query,
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId)
    {
        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);
        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);
        // ملفات وضع «منفذ عليه» المشطوبة تُخفى من القوائم والتصدير العام:
        // تظهر فقط في صفحة «الملفات المشطوبة» عبر SearchStruckOffAsync.
        q = q.Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == ExecutionStatusCatalog.ExecutedFilter)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    || d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement);
            else if (status == ExecutionStatusCatalog.Deferred)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.Deferred);
            else
                q = q.Where(d =>
                    string.IsNullOrEmpty(d.ExecStatus) &&
                    d.IsDraft == (status == ExecutionStatusCatalog.DraftFilter));
        }

        if (!string.IsNullOrWhiteSpace(applicant))
        {
            var term = applicant.Trim();
            q = q.Where(d => d.Applicant != null && d.Applicant == term);
        }

        if (!string.IsNullOrWhiteSpace(court))
        {
            var term = court.Trim();
            q = q.Where(d => d.Court != null && d.Court == term);
        }

        if (!string.IsNullOrWhiteSpace(lawyer))
        {
            var term = lawyer.Trim();
            q = q.Where(d => d.Lawyer != null && d.Lawyer == term);
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            var term = branch.Trim();
            q = q.Where(d => d.BranchName != null && d.BranchName == term);
        }

        if (!string.IsNullOrWhiteSpace(administrativeBranch))
        {
            var term = administrativeBranch.Trim();
            q = q.Where(d => d.Branch != null && d.Branch.Name == term);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d =>
                (d.SearchText != null && d.SearchText.Contains(term)) ||
                (d.BorrowerName != null &&
                    ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                     (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term))) ||
                d.Guarantors.Any(g =>
                    g.GuarantorName != null &&
                    ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))));
        }

        return q;
    }

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default)
    {
        // كل قائمة تُقيَّد بباقي الفلاتر النشطة ما عدا فلتر الحقل نفسه،
        // فيلتزم الاختيار اللاحق بنتائج الفلتر السابق بأسلوب إكسل.
        IQueryable<Document> Base(string? st, string? ap, string? co, string? lw, string? br, string? ab) =>
            ApplySearchFilters(Db.Documents.AsNoTracking(), null, st, ap, co, lw, br, ab, visibleBranchId, visibleUserId);

        var applicants = await Base(status, null, court, lawyer, branch, administrativeBranch)
            .Where(d => d.Applicant != null && d.Applicant != string.Empty)
            .Select(d => d.Applicant!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

        var courts = await Base(status, applicant, null, lawyer, branch, administrativeBranch)
            .Where(d => d.Court != null && d.Court != string.Empty)
            .Select(d => d.Court!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        var lawyers = await Base(status, applicant, court, null, branch, administrativeBranch)
            .Where(d => d.Lawyer != null && d.Lawyer != string.Empty)
            .Select(d => d.Lawyer!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);

        var branches = await Base(status, applicant, court, lawyer, null, administrativeBranch)
            .Where(d => d.BranchName != null && d.BranchName != string.Empty)
            .Select(d => d.BranchName!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        var administrativeBranches = await Base(status, applicant, court, lawyer, branch, null)
            .Where(d => d.Branch != null && d.Branch.Name != null && d.Branch.Name != string.Empty)
            .Select(d => d.Branch!.Name!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        return new DocumentFilterOptions(applicants, courts, lawyers, administrativeBranches, branches);
    }

    public async Task<Document?> GetDeletedByIdAsync(int id, CancellationToken ct = default)
    {
        return await Db.Documents
            .IgnoreQueryFilters()
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchDeletedAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.IsDeleted);

        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);

        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d =>
                (d.SearchText != null && d.SearchText.Contains(term)) ||
                (d.BorrowerName != null &&
                    ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                     (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term))) ||
                d.Guarantors.Any(g =>
                    g.GuarantorName != null &&
                    ((g.GuarantorName + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term) ||
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(d => d.DeletedAt)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<Document?> TransferOwnerAsync(
        int id,
        int expectedCreatedById,
        int targetId,
        string targetFullName,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await Db.Documents
            .Where(d => d.Id == id && d.CreatedById == expectedCreatedById)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.UpdatedAt, now), ct);

        if (rows == 0)
            return null;

        return await Db.Documents
            .AsNoTracking()
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<int> CountByOwnerAsync(int ownerId, CancellationToken ct = default)
    {
        // Query Filter (!IsDeleted) مطبق تلقائياً فيستثني المحذوف.
        return await Db.Documents.CountAsync(d => d.CreatedById == ownerId, ct);
    }

    public async Task<List<Document>> ListByOwnerAsync(int ownerId, CancellationToken ct = default)
    {
        // إسقاط على الحقول اللازمة لسجل التدقيق فقط (رقم/نوع/اسم المنفذ عليه)
        // لتفادي تحميل بيانات الملفات كاملة أثناء النقل الجماعي.
        return await Db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == ownerId)
            .OrderBy(d => d.Id)
            .Select(d => new Document
            {
                Id = d.Id,
                DocumentType = d.DocumentType,
                BorrowerName = d.BorrowerName,
                BorrowerFather = d.BorrowerFather,
                BorrowerFamily = d.BorrowerFamily,
            })
            .ToListAsync(ct);
    }

    public async Task<int> TransferAllOwnerAsync(
        int sourceOwnerId,
        int targetId,
        string targetFullName,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Query Filter (!IsDeleted) مطبق تلقائياً على ExecuteUpdate فيستثني المحذوف.
        return await Db.Documents
            .Where(d => d.CreatedById == sourceOwnerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.UpdatedAt, now), ct);
    }

    public async Task<List<Document>> GetByIdsAsync(List<int> ids, CancellationToken ct = default)
    {
        return await Db.Documents
            .AsNoTracking()
            .Include(d => d.BaseNumbers)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);
    }

    public async Task<(int TotalCount, List<Document> Items)> GetRotationCandidatesAsync(
        int userId, int page, int perPage, CancellationToken ct = default)
    {
        // المؤهل للتدوير: مقيد برقم ملف (ليس تحت رفع) وغير منفَّذ (لا منفَّذ بالتسوية ولا
        // منفذ جبريا منفذًا كاملًا — أما «منفذ جزئيا» فما زال متداولًا)، وغير محذوف
        // (Query Filter مطبق تلقائيًا)، ولم يُدوَّر في السنة الحالية (لا يملك رقم أساس لها).
        // قاعدة القيد: رقم الملف الأصلي هو نفسه رقم أساس سنة قيده، فالملف المقيد في السنة
        // الحالية يملك رقم أساس لها بالفعل (رقم ملفه) فلا يُدوَّر، والملفات من سنوات سابقة فقط
        // هي المؤهلة للتدوير في السنة الحالية.
        // عدد الصفحات يُحسب على مستوى قاعدة البيانات لتجنب جلب آلاف الصفوف دفعة واحدة.
        var currentYear = DateTime.Today.Year;
        IQueryable<Document> q = Db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Where(d => d.GeneralEntitySide != GeneralEntitySideCatalog.Executed)
            .Where(d => !d.IsDraft)
            .Where(d => d.ExecStatus != ExecutionStatusCatalog.ExecutedBySettlement
                && !(d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    && d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted))
            .Where(d => !d.BaseNumbers.Any(b => b.Year == currentYear))
            .Where(d => d.FileYear != currentYear.ToString());

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.Court)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.BaseNumbers)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchStruckOffAsync(
        string? query,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        // ملفات وضع «منفذ عليه» المشطوبة فقط، غير المحذوفة (Query Filter مطبق تلقائيًا):
        // مكتملة الاستبعاد من البحث العام والتصدير، ويُعرض سجلها في صفحة «الملفات المشطوبة».
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d => d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                && d.ExecutedStatus == ExecutedStatusCatalog.StruckOff);

        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);

        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);

        if (!string.IsNullOrWhiteSpace(applicant))
        {
            var term = applicant.Trim();
            q = q.Where(d => d.Applicant != null && d.Applicant == term);
        }

        if (!string.IsNullOrWhiteSpace(court))
        {
            var term = court.Trim();
            q = q.Where(d => d.Court != null && d.Court == term);
        }

        if (!string.IsNullOrWhiteSpace(lawyer))
        {
            var term = lawyer.Trim();
            q = q.Where(d => d.Lawyer != null && d.Lawyer == term);
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            var term = branch.Trim();
            q = q.Where(d => d.BranchName != null && d.BranchName == term);
        }

        if (!string.IsNullOrWhiteSpace(administrativeBranch))
        {
            var term = administrativeBranch.Trim();
            q = q.Where(d => d.Branch != null && d.Branch.Name == term);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            q = q.Where(d =>
                (d.SearchText != null && d.SearchText.Contains(term)) ||
                (d.BorrowerName != null &&
                    ((d.BorrowerName + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term) ||
                     (d.BorrowerName + " " + (d.BorrowerFather ?? string.Empty) + " " + (d.BorrowerFamily ?? string.Empty)).Contains(term))) ||
                d.ExecutionApplicants.Any(a =>
                    (a.Name != null &&
                        ((a.Name + " " + (a.Family ?? string.Empty)).Contains(term) ||
                         (a.Name + " " + (a.Father ?? string.Empty) + " " + (a.Family ?? string.Empty)).Contains(term))) ||
                    ((a.DeceasedName ?? string.Empty) + " " + (a.DeceasedFamily ?? string.Empty)).Contains(term)) ||
                d.ExecutedNaturalPersons.Any(p =>
                    p.Name != null &&
                    ((p.Name + " " + (p.Family ?? string.Empty)).Contains(term) ||
                     (p.Name + " " + (p.Father ?? string.Empty) + " " + (p.Family ?? string.Empty)).Contains(term))) ||
                d.ExecutedPublicEntities.Any(e => e.EntityName != null && e.EntityName.Contains(term)));
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(d => d.StruckOffDate)
            .ThenByDescending(d => d.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.Guarantors)
            .Include(d => d.RealEstates)
            .ThenInclude(r => r.Owners)
            .Include(d => d.Heirs)
            .Include(d => d.ExecutionActions)
            .Include(d => d.RegistrationDate)
            .Include(d => d.CreatedBy)
            .Include(d => d.Branch)
            .Include(d => d.BaseNumbers)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .ToListAsync(ct);

        return (total, items);
    }
}
