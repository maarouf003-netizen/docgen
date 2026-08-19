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
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId);

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * perPage)
                .Take(perPage))
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
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId);

        return await WithStandardIncludes(q.OrderByDescending(d => d.CreatedAt))
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
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId,
        int? visibleUserId)
    {
        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);
        if (visibleUserId.HasValue)
            q = q.Where(d => d.CreatedById == visibleUserId);
        // ملفات وضع «منفذ عليه» المشطوبة وملفات «طالبة تنفيذ» المشطوبة تُخفى من القوائم
        // والتصدير العام: تظهر فقط في صفحة «الملفات المشطوبة» عبر SearchStruckOffAsync.
        q = q.Where(d => d.ExecutedStatus != ExecutedStatusCatalog.StruckOff
            && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff);
        // الملفات «المنفذة» (عائلة «منفذ عليه»/«عرض وايداع» بحالة «منفذ»، وملفات «طالبة تنفيذ»
        // المنفذة بالتسوية أو الجبري الكامل) تُخفى من القائمة والتصدير العام إلا عند البحث النصي
        // عنها (query)، فتظهر للعثور عليها. التعريف مطابق تمامًا لصفحة «الملفات المنفذة»
        // (SearchExecutedAsync) فلا يتسرب أي ملف منفذ إلى القائمة في غير ذلك.
        if (string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(d =>
                !((d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.ExecutedStatus == ExecutedStatusCatalog.Executed)
                && !(d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && (d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                        || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                        || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                            && d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted))));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == ExecutionStatusCatalog.ExecutedFilter)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    || d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted);
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

        // فلتر «المنفذ عليه» يقتصر على الجهات العامة فقط: مطابقة دقيقة لاسم الجهة
        // على قائمة ExecutedPublicEntities (تطابق تام، وليس بحثًا نصيًا جزئيًا).
        if (!string.IsNullOrWhiteSpace(executedEntity))
        {
            var term = executedEntity.Trim();
            q = q.Where(d => d.ExecutedPublicEntities.Any(e =>
                e.EntityName != null && e.EntityName == term));
        }

        // فلتر «الفرع» (فرع الجهة العامة): في عائلة وضع «منفذ عليه» يُطابق فرع أول جهة عامة
        // (طبيعة public) منفذ عليها، وفي نظام «طالبة تنفيذ» فرع أول جهة عامة طالبة للتنفيذ —
        // بنفس منطق عمود «الفرع» في قائمة الملفات (تطابق تام، وليس بحثًا نصيًا جزئيًا).
        if (!string.IsNullOrWhiteSpace(publicEntityBranch))
        {
            var term = publicEntityBranch.Trim();
            q = q.Where(d =>
                d.ExecutedPublicEntities.Any(e =>
                    e.EntityNature == PartyNatureCatalog.PublicEntity &&
                    e.EntityBranch != null && e.EntityBranch == term) ||
                d.ApplicantPublicEntities.Any(a =>
                    a.Branch != null && a.Branch == term));
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
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))) ||
                // البحث بأسماء الورثة: ورثة المقترض/الكفلاء في وضع «طالب تنفيذ» (اسم ثلاثي كالكفلاء)،
                // وورثة المورثين المتوفين في وضع «منفذ عليه» (اسم ثلاثي).
                d.Heirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))) ||
                d.ExecutedHeirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        }

        return q;
    }

    /// <summary>
    /// شجرة التحميل المسبق (Include) المعتمدة لكائن Document كاملًا،
    /// تُوحَّد هنا لتجنب تكرار نفس السلسلة في كل استعلام يعيد المستند ببياناته.
    /// </summary>
    private static IQueryable<Document> WithStandardIncludes(IQueryable<Document> q) =>
        q.Include(d => d.Guarantors)
            .Include(d => d.Assets)
            .ThenInclude(a => a.Owners)
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
            .Include(d => d.Occurrences)
            .ThenInclude(o => o.CreatedBy)
            .Include(d => d.ApplicantPublicEntities)
            .Include(d => d.Assignments);

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        string? executedEntity,
        string? publicEntityBranch,
        int? visibleBranchId,
        int? visibleUserId,
        CancellationToken ct = default)
    {
        // كل قائمة تُقيَّد بباقي الفلاتر النشطة ما عدا فلتر الحقل نفسه،
        // فيلتزم الاختيار اللاحق بنتائج الفلتر السابق بأسلوب إكسل.
        IQueryable<Document> Base(string? st, string? ap, string? co, string? lw, string? br, string? ab, string? ee, string? peb) =>
            ApplySearchFilters(Db.Documents.AsNoTracking(), null, st, ap, co, lw, br, ab, ee, peb, visibleBranchId, visibleUserId);

        var applicants = await Base(status, null, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch)
            .Where(d => d.Applicant != null && d.Applicant != string.Empty)
            .Select(d => d.Applicant!)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);

        var courts = await Base(status, applicant, null, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch)
            .Where(d => d.Court != null && d.Court != string.Empty)
            .Select(d => d.Court!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        var lawyers = await Base(status, applicant, court, null, branch, administrativeBranch, executedEntity, publicEntityBranch)
            .Where(d => d.Lawyer != null && d.Lawyer != string.Empty)
            .Select(d => d.Lawyer!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);

        var branches = await Base(status, applicant, court, lawyer, null, administrativeBranch, executedEntity, publicEntityBranch)
            .Where(d => d.BranchName != null && d.BranchName != string.Empty)
            .Select(d => d.BranchName!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        var administrativeBranches = await Base(status, applicant, court, lawyer, branch, null, executedEntity, publicEntityBranch)
            .Where(d => d.Branch != null && d.Branch.Name != null && d.Branch.Name != string.Empty)
            .Select(d => d.Branch!.Name!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        var executedEntities = await Base(status, applicant, court, lawyer, branch, administrativeBranch, null, publicEntityBranch)
            .Where(d => d.ExecutedPublicEntities.Any(e => e.EntityName != null && e.EntityName != string.Empty))
            .SelectMany(d => d.ExecutedPublicEntities.Select(e => e.EntityName!))
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(ct);

        // خيارات فلتر «الفرع»: فرع أول جهة عامة منفذ عليها (طبيعة public) في عائلة وضع
        // «منفذ عليه»، وفرع أول جهة عامة طالبة للتنفيذ في نظام «طالبة تنفيذ».
        var executedEntityBranches = await Base(status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, null)
            .SelectMany(d => d.ExecutedPublicEntities)
            .Where(e => e.EntityNature == PartyNatureCatalog.PublicEntity
                && e.EntityBranch != null && e.EntityBranch != string.Empty)
            .Select(e => e.EntityBranch!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        var applicantEntityBranches = await Base(status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, null)
            .SelectMany(d => d.ApplicantPublicEntities)
            .Where(a => a.Branch != null && a.Branch != string.Empty)
            .Select(a => a.Branch!)
            .Distinct()
            .OrderBy(b => b)
            .ToListAsync(ct);

        var publicEntityBranches = executedEntityBranches
            .Union(applicantEntityBranches)
            .OrderBy(b => b)
            .ToList();

        return new DocumentFilterOptions(applicants, courts, lawyers, administrativeBranches, branches, executedEntities, publicEntityBranches);
    }

    public async Task<Document?> GetDeletedByIdAsync(int id, CancellationToken ct = default)
    {
        return await WithStandardIncludes(Db.Documents.IgnoreQueryFilters())
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
                     (g.GuarantorName + " " + (g.GuarantorFather ?? string.Empty) + " " + (g.GuarantorFamily ?? string.Empty)).Contains(term))) ||
                d.Heirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))) ||
                d.ExecutedHeirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.DeletedAt)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<Document?> TransferOwnerAsync(
        int id,
        int expectedCreatedById,
        int targetId,
        string targetFullName,
        string referredFromLawyer,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = await Db.Documents
            .Where(d => d.Id == id && d.CreatedById == expectedCreatedById)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.ReferredFromLawyer, referredFromLawyer)
                .SetProperty(d => d.ReferredAt, now)
                .SetProperty(d => d.UpdatedAt, now), ct);

        if (rows == 0)
            return null;

        return await WithStandardIncludes(Db.Documents.AsNoTracking())
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task AddAssignmentAsync(int documentId, string kind, string lawyerName, string? assignedByName, DateTime assignedAt, CancellationToken ct = default)
    {
        await Db.DocumentAssignments.AddAsync(new DocumentAssignment
        {
            DocumentId = documentId,
            Kind = kind,
            LawyerName = lawyerName,
            AssignedByName = assignedByName,
            AssignedAt = assignedAt,
        }, ct);
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
        string referredFromLawyer,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Query Filter (!IsDeleted) مطبق تلقائياً على ExecuteUpdate فيستثني المحذوف.
        return await Db.Documents
            .Where(d => d.CreatedById == sourceOwnerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.ReferredFromLawyer, referredFromLawyer)
                .SetProperty(d => d.ReferredAt, now)
                .SetProperty(d => d.UpdatedAt, now), ct);
    }

    public async Task<int> IncrementViewCountAsync(int documentId, CancellationToken ct = default)
    {
        // تحديث ذرّي على مستوى القاعدة: ViewCount = ViewCount + 1 بدون تحميل المستند.
        return await Db.Documents
            .Where(d => d.Id == documentId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.ViewCount, d => d.ViewCount + 1), ct);
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
        // المؤهل للتدوير: مقيد برقم ملف (ليس تحت رفع) وغير منفَّذ وغير محذوف
        // (Query Filter مطبق تلقائيًا)، ولم يُدوَّر في السنة الحالية (لا يملك رقم أساس لها).
        // قاعدة القيد: رقم الملف الأصلي هو نفسه رقم أساس سنة قيده، فالملف المقيد في السنة
        // الحالية يملك رقم أساس لها بالفعل (رقم ملفه) فلا يُدوَّر، والملفات من سنوات سابقة فقط
        // هي المؤهلة للتدوير في السنة الحالية.
        // عائلتا وضع «منفذ عليه» (Executed + Deposit) مؤهلتان متداولتين فقط (لا منفذ ولا مشطوب)
        // وبشرط وجود رقم أساس من سنة سابقة — التدوير فيهما استمرار لدوران سبق أن بدأ.
        // عدد الصفحات يُحسب على مستوى قاعدة البيانات لتجنب جلب آلاف الصفوف دفعة واحدة.
        var currentYear = DateTime.Today.Year;
        IQueryable<Document> q = Db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Where(d => !d.IsDraft)
            .Where(d => (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                ? d.ExecutedStatus == ExecutedStatusCatalog.None
                    && d.BaseNumbers.Any(b => b.Year < currentYear)
                : d.ExecStatus != ExecutionStatusCatalog.ExecutedBySettlement
                    && d.ExecStatus != ExecutionStatusCatalog.DelegationExecuted
                    && !(d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)
                    && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Where(d => !d.BaseNumbers.Any(b => b.Year == currentYear))
            .Where(d => d.FileYear != currentYear.ToString());

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderBy(d => d.Court)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * perPage)
            .Take(perPage)
            .Include(d => d.BaseNumbers)
            // عائلتا وضع «منفذ عليه» بلا مقترض، فيُشتق اسم العرض من طالب العرض
            // (ExecutionApplicants / ExecutedPublicEntities / ExecutedNaturalPersons).
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
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
        // ملفات «منفذ عليها»/«عرض وايداع» المشطوبة وملفات «طالبة تنفيذ» المشطوبة فقط،
        // غير المحذوفة (Query Filter مطبق تلقائيًا): مكتملة الاستبعاد من البحث العام والتصدير،
        // ويُعرض سجلها في صفحة «الملفات المشطوبة».
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d => (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                && d.ExecutedStatus == ExecutedStatusCatalog.StruckOff
                || (d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && d.ExecStatus == ExecutionStatusCatalog.StateStruckOff));

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
                d.ExecutedPublicEntities.Any(e => e.EntityName != null && e.EntityName.Contains(term)) ||
                d.ExecutedHeirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.StruckOffDate)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchExecutedAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default)
    {
        // ملفات «منفذ عليها»/«عرض وايداع» بحالة «منفذ» فقط، وملفات «طالبة تنفيذ» المنفذة
        // (بالتسوية أو الجبري الكامل) — تُخفى من البحث العام إلا عند البحث النصي عنها،
        // فيُعرض سجلها في صفحة «الملفات المنفذة». غير المحذوفة (Query Filter مطبق تلقائيًا)
        // وتُستبعد المشطوبة لأن حالتها «مشطوب» تُبقيها خارج شرط «منفذ» (فهي في صفحة
        // «الملفات المشطوبة»).
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d =>
                ((d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.ExecutedStatus == ExecutedStatusCatalog.Executed)
                || (d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && (d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                        || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                        || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                            && d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted))));

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
                d.ExecutionApplicants.Any(a =>
                    (a.Name != null &&
                        ((a.Name + " " + (a.Family ?? string.Empty)).Contains(term) ||
                         (a.Name + " " + (a.Father ?? string.Empty) + " " + (a.Family ?? string.Empty)).Contains(term))) ||
                    ((a.DeceasedName ?? string.Empty) + " " + (a.DeceasedFamily ?? string.Empty)).Contains(term)) ||
                d.ExecutedNaturalPersons.Any(p =>
                    p.Name != null &&
                    ((p.Name + " " + (p.Family ?? string.Empty)).Contains(term) ||
                     (p.Name + " " + (p.Father ?? string.Empty) + " " + (p.Family ?? string.Empty)).Contains(term))) ||
                d.ExecutedPublicEntities.Any(e => e.EntityName != null && e.EntityName.Contains(term)) ||
                d.ExecutedHeirs.Any(h =>
                    h.HeirName != null &&
                    ((h.HeirName + " " + (h.HeirFamily ?? string.Empty)).Contains(term) ||
                     (h.HeirName + " " + (h.HeirFather ?? string.Empty) + " " + (h.HeirFamily ?? string.Empty)).Contains(term))));
        }

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.UpdatedAt)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }
}
