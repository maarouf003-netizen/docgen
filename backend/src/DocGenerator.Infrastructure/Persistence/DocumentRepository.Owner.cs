using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocGenerator.Infrastructure.Persistence;

public partial class DocumentRepository : Repository<Document>, IDocumentRepository
{
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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        var q = BuildDeletedQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        // فاصل حتمي كما في `SearchAsync`: حذف جماعي في اللحظة نفسها يجعل `DeletedAt` متساويًا.
        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.DeletedAt)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    /// <summary>
    /// قائمة المحذوفات بإسقاط صف القائمة (P3-04c): نفس باني الاستعلام والترتيب والترقيم
    /// حرفيًا، لكن بلا `WithStandardIncludes`.
    /// </summary>
    public async Task<(int TotalCount, List<DocumentListRow> Items, bool HasMore)> SearchDeletedListAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        var q = BuildDeletedQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        if (cursor is not null)
        {
            // ترتيب `DeletedAt DESC` في PostgreSQL يضع NULL أولًا؛ فمؤشر بقيمة غير
            // فارغة يعني أن صفوف NULL سابقة وقد مُرَّت، ومؤشر NULL يعني أننا داخل
            // مجموعة NULL نفسها فالفاصل `Id` وحده.
            if (cursor.SortValue is { } value)
                q = q.Where(d => d.DeletedAt < value || (d.DeletedAt == value && d.Id < cursor.Id));
            else
                q = q.Where(d => d.DeletedAt == null && d.Id < cursor.Id);
        }

        var items = await q.OrderByDescending(d => d.DeletedAt)
            .ThenByDescending(d => d.Id)
            .Skip(cursor is null ? (page - 1) * perPage : 0)
            .Take(perPage + 1)
            .Select(DocumentListRowProjection.Selector)
            .ToListAsync(ct);

        var hasMore = items.Count > perPage;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        await DocumentListRowProjection.LoadChildrenAsync(Db, items, ct, includeDeletedChildren: true);
        return (total, items, hasMore);
    }

    /// <summary>
    /// باني استعلام المحذوفات المشترك بين القراءة الكاملة والمُسقَطة: يتجاوز Query Filter،
    /// ويقصر على المحذوف منطقيًا، ويطبّق نطاق المالك والبحث النصي الموحد.
    /// </summary>
    private IQueryable<Document> BuildDeletedQuery(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.IsDeleted);

        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            // البحث النصي عبر الكتلة الموحدة نفسها المستخدمة في القائمة الرئيسية
            // (ApplyPersonNameSearch) — مع ملاحظة IgnoreQueryFilters الخاصة بهذه الصفحة.
            q = ApplyPersonNameSearch(q, query.Trim());
        }

        return q;
    }

    /// <summary>
    /// RF-010: تثبيت `OriginalValue` لعدّاد التزامن على الكيان المتتبَّع —
    /// أي حفظ لاحق يفشل `DbUpdateConcurrencyException` إن تغيّر الصف تحته.
    /// </summary>
    public void SetVersionOriginal(Document doc, long expected)
        => Db.Entry(doc).Property(d => d.Version).OriginalValue = expected;

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
                .SetProperty(d => d.Version, d => d.Version + 1)
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

    /// <summary>
    /// نقل جماعي ذرّي بالنطاق (§5.5): ملفات المصدر المتقاطعة مع نطاق المنفِّذ
    /// فقط (قسمه أو شعبته) — لا كل ملفات المحامي.
    /// </summary>
    public async Task<int> TransferAllOwnerAsync(
        int sourceOwnerId,
        int targetId,
        string targetFullName,
        string referredFromLawyer,
        int? scopeBranchId,
        int? ownerSectionId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        // Query Filter (!IsDeleted) مطبق تلقائياً على ExecuteUpdate فيستثني المحذوف.
        // (البند 56: رفع Version يدويًا — وإلا فُقدت حماية RF-010 بصمت.)
        IQueryable<Document> q = Db.Documents
            .Where(d => d.CreatedById == sourceOwnerId);
        q = ApplyOwnerScope(q, scopeBranchId, null, ownerSectionId);
        return await q.ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.CreatedById, targetId)
                .SetProperty(d => d.Lawyer, targetFullName)
                .SetProperty(d => d.ReferredFromLawyer, referredFromLawyer)
                .SetProperty(d => d.ReferredAt, now)
                .SetProperty(d => d.Version, d => d.Version + 1)
                .SetProperty(d => d.UpdatedAt, now), ct);
    }

    /// <summary>
    /// ملفات محامٍ بالنطاق (§5.5 للمعاينة والتدقيق): نفس قاعدة النقل الجماعي —
    /// المعاينة تطابق المنقول فعلًا.
    /// </summary>
    public async Task<List<Document>> ListByOwnerInScopeAsync(
        int ownerId,
        int? scopeBranchId,
        int? ownerSectionId,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == ownerId);
        q = ApplyOwnerScope(q, scopeBranchId, null, ownerSectionId);
        return await q
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

    public async Task<int> CountByOwnerInScopeAsync(
        int ownerId,
        int? scopeBranchId,
        int? ownerSectionId,
        CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents
            .Where(d => d.CreatedById == ownerId);
        q = ApplyOwnerScope(q, scopeBranchId, null, ownerSectionId);
        return await q.CountAsync(ct);
    }

}
