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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        var q = BuildStruckOffQuery(query, applicant, court, lawyer, branch, administrativeBranch,
            visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.StruckOffDate)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    /// <summary>
    /// قائمة المشطوبة بإسقاط صف القائمة (P3-04c): نفس الباني والترتيب والترقيم حرفيًا،
    /// بلا `WithStandardIncludes`.
    /// </summary>
    public async Task<(int TotalCount, List<DocumentListRow> Items, bool HasMore)> SearchStruckOffListAsync(
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
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        var q = BuildStruckOffQuery(query, applicant, court, lawyer, branch, administrativeBranch,
            visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        if (cursor is not null)
        {
            if (cursor.SortValue is { } value)
                q = q.Where(d => d.StruckOffDate < value || (d.StruckOffDate == value && d.Id < cursor.Id));
            else
                q = q.Where(d => d.StruckOffDate == null && d.Id < cursor.Id);
        }

        var items = await q.OrderByDescending(d => d.StruckOffDate)
            .ThenByDescending(d => d.Id)
            .Skip(cursor is null ? (page - 1) * perPage : 0)
            .Take(perPage + 1)
            .Select(DocumentListRowProjection.Selector)
            .ToListAsync(ct);

        var hasMore = items.Count > perPage;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        await DocumentListRowProjection.LoadChildrenAsync(Db, items, ct);
        return (total, items, hasMore);
    }

    /// <summary>
    /// باني استعلام المشطوبة المشترك: ملفات «منفذ عليها»/«عرض وايداع» المشطوبة وملفات
    /// «طالبة تنفيذ» المشطوبة فقط، غير المحذوفة (Query Filter مطبق تلقائيًا)، بنفس
    /// النطاق والفلاتر النصية للصفحة. البحث النصي عبر الكتلة الموحدة نفسها المستخدمة
    /// في القائمة الرئيسية فتتكافأ التغطية.
    /// </summary>
    private IQueryable<Document> BuildStruckOffQuery(
        string? query,
        string? applicant,
        string? court,
        string? lawyer,
        string? branch,
        string? administrativeBranch,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d => (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                && d.ExecutedStatus == ExecutedStatusCatalog.StruckOff
                || (d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && d.ExecStatus == ExecutionStatusCatalog.StateStruckOff));

        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);

        if (!string.IsNullOrWhiteSpace(applicant))
        {
            var term = applicant.Trim();
            q = q.Where(d => d.Applicant != null && d.Applicant == term);
        }

        if (!string.IsNullOrWhiteSpace(court))
        {
            var term = court.Trim();
            // `PB-001`: الدقة الخام أو المعيارية (مجموعة عليا — بلا انحدار مطابقات دقيقة).
            var normTerm = ArabicNameNormalizer.Normalize(term);
            q = q.Where(d => d.Court != null && (d.Court == term || d.CourtNorm == normTerm));
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
            q = ApplyPersonNameSearch(q, query.Trim());
        }

        return q;
    }

    public async Task<(int TotalCount, List<(int Id, DateTime LastActivityUtc)> Items, bool HasMore)> SearchStrikeOffReminderIdsAsync(
        DateTime cutoffUtc,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        // ملفات «طالبة تنفيذ» النشطة فقط: المسودات والمشطوبة والمنفذة (بالتسوية/الإنابة/المسترد/
        // الجبري الكامل) و«محال الى البداية» خارج التذكير — والجزئي/المتريث داخلان (متداولان).
        // التعريف مطابق لمفهوم «الملف المخفي» في ApplySearchFilters، ولا يشمل عائلة «منفذ عليه».
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d => d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant)
            .Where(d => !d.IsDraft)
            .Where(d => d.ExecStatus != ExecutionStatusCatalog.StateStruckOff
                && d.ExecStatus != ExecutionStatusCatalog.ReferredToStart
                && d.ExecStatus != ExecutionStatusCatalog.ExecutedBySettlement
                && d.ExecStatus != ExecutionStatusCatalog.DelegationExecuted
                && d.ExecStatus != ExecutionStatusCatalog.Recovered
                && !(d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                    && (d.ExecSubStatus == null
                        || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)));

        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);

        // آخر نشاط: أحدث إجراء محفوظ (الملاحظات مستثناة) → تاريخ القيد المحلول → تاريخ الإدخال.
        // يُحسب في SQL داخل إسقاط واحد ثم يُفلتر عليه (المصدر الوحيد للقاعدة في
        // StrikeOffReminderCalculator، وتطابقه مع الحساب المعروض مُختبَر صراحةً).
        var projected = q.Select(d => new
        {
            d.Id,
            LastActivity = d.ExecutionActions
                .Where(a => a.Type == "action")
                .Max(a => (DateTime?)(a.ActionDateParsed ?? a.CreatedAt))
                ?? (d.RegistrationDate != null ? d.RegistrationDate.DateParsed : null)
                ?? d.CreatedAt,
            d.StrikeOffReminderDismissedAt,
        });

        // الإلغاء صالح ما دام لا يوجد إجراء أحدث منه؛ وأي إجراء جديد يُعيد الملف للدورة بعد العتبة.
        var due = projected.Where(x =>
            x.LastActivity <= cutoffUtc
            && (x.StrikeOffReminderDismissedAt == null || x.StrikeOffReminderDismissedAt < x.LastActivity));

        var total = await due.CountAsync(ct);

        if (cursor is not null)
        {
            var value = cursor.SortValue
                ?? throw new ArgumentException("مؤشر الصفحة غير صالح — أعد تحميل القائمة");
            // ترتيب تصاعدي (الأقدم ركودًا أولًا): ما بعد المؤشر أكبر، والفاصل `Id` تصاعدي.
            due = due.Where(x => x.LastActivity > value || (x.LastActivity == value && x.Id > cursor.Id));
        }

        var items = await due
            .OrderBy(x => x.LastActivity)
            .ThenBy(x => x.Id)
            .Skip(cursor is null ? (page - 1) * perPage : 0)
            .Take(perPage + 1)
            .Select(x => new { x.Id, x.LastActivity })
            .ToListAsync(ct);

        var hasMore = items.Count > perPage;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        return (total, items.Select(x => (x.Id, x.LastActivity)).ToList(), hasMore);
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchExecutedAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        var q = BuildExecutedQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.UpdatedAt)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    /// <summary>
    /// قائمة المنفذة بإسقاط صف القائمة (P3-04c): نفس الباني والترتيب والترقيم حرفيًا،
    /// بلا `WithStandardIncludes`.
    /// </summary>
    public async Task<(int TotalCount, List<DocumentListRow> Items, bool HasMore)> SearchExecutedListAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        var q = BuildExecutedQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        if (cursor is not null)
        {
            var value = cursor.SortValue
                ?? throw new ArgumentException("مؤشر الصفحة غير صالح — أعد تحميل القائمة");
            q = q.Where(d => d.UpdatedAt < value || (d.UpdatedAt == value && d.Id < cursor.Id));
        }

        var items = await q.OrderByDescending(d => d.UpdatedAt)
            .ThenByDescending(d => d.Id)
            .Skip(cursor is null ? (page - 1) * perPage : 0)
            .Take(perPage + 1)
            .Select(DocumentListRowProjection.Selector)
            .ToListAsync(ct);

        var hasMore = items.Count > perPage;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        await DocumentListRowProjection.LoadChildrenAsync(Db, items, ct);
        return (total, items, hasMore);
    }

    /// <summary>
    /// باني استعلام «الملفات المنفذة» المشترك: ملفات «منفذ عليها»/«عرض وايداع» بحالة
    /// «منفذ» فقط، وملفات «طالبة تنفيذ» المنفذة (بالتسوية أو الجبري الكامل). غير المحذوفة
    /// (Query Filter مطبق تلقائيًا) وتُستبعد المشطوبة لأن حالتها «مشطوب» تُبقيها خارج
    /// شرط «منفذ». البحث النصي عبر الكتلة الموحدة نفسها المستخدمة في القائمة الرئيسية.
    /// </summary>
    private IQueryable<Document> BuildExecutedQuery(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d =>
                ((d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.ExecutedStatus == ExecutedStatusCatalog.Executed)
                || (d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && (d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                        || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                        || d.ExecStatus == ExecutionStatusCatalog.Recovered
                        || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                            && (d.ExecSubStatus == null
                                || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)))));

        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = ApplyPersonNameSearch(q, query.Trim());
        }

        return q;
    }

    public async Task<(int TotalCount, List<Document> Items)> SearchReferredToStartAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        var q = BuildReferredToStartQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        var items = await WithStandardIncludes(
            // معالجة صريحة لقيم تاريخ الإحالة الغائبة (الحقل اختياري): الأحدث أولًا ثم
            // الغائبة آخرًا — ترتيب حتمي متطابق بين SQLite وPostgreSQL بدل الاعتماد
            // على سلوك NULL الافتراضي المختلف بينهما.
            q.OrderByDescending(d => d.StartReferralDate.HasValue)
                .ThenByDescending(d => d.StartReferralDate)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    /// <summary>
    /// قائمة «محال الى البداية» بإسقاط صف القائمة (P3-04c): نفس الباني والترتيب
    /// والترقيم حرفيًا، بلا `WithStandardIncludes`.
    /// </summary>
    public async Task<(int TotalCount, List<DocumentListRow> Items, bool HasMore)> SearchReferredToStartListAsync(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int page,
        int perPage,
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        var q = BuildReferredToStartQuery(query, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        if (cursor is not null)
        {
            // الترتيب: ذوو التاريخ أولًا تنازليًا ثم NULL؛ فمؤشر NULL يعني مجموعة
            // التاريخ الغائب نفسها، ومؤشر بقيمة يعني ما بعدها بما فيها NULL.
            if (cursor.SortValue is { } value)
                q = q.Where(d => d.StartReferralDate == null
                    || d.StartReferralDate < value
                    || (d.StartReferralDate == value && d.Id < cursor.Id));
            else
                q = q.Where(d => d.StartReferralDate == null && d.Id < cursor.Id);
        }

        var items = await q.OrderByDescending(d => d.StartReferralDate.HasValue)
            .ThenByDescending(d => d.StartReferralDate)
            .ThenByDescending(d => d.Id)
            .Skip(cursor is null ? (page - 1) * perPage : 0)
            .Take(perPage + 1)
            .Select(DocumentListRowProjection.Selector)
            .ToListAsync(ct);

        var hasMore = items.Count > perPage;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        await DocumentListRowProjection.LoadChildrenAsync(Db, items, ct);
        return (total, items, hasMore);
    }

    /// <summary>
    /// باني استعلام «محال الى البداية» المشترك: ملفات «طالبة تنفيذ» بحالة «محال الى البداية»
    /// فقط — ومنها القادم من «منفذ جبريا» المحال بجزئيته. البحث النصي عبر الكتلة المخصصة
    /// لهذه الصفحة (ApplyReferredPersonNameSearch: بلا فروع عائلة «منفذ عليه»).
    /// </summary>
    private IQueryable<Document> BuildReferredToStartQuery(
        string? query,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId)
    {
        IQueryable<Document> q = Db.Documents.AsNoTracking()
            .Where(d => d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                && d.ExecStatus == ExecutionStatusCatalog.ReferredToStart);

        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = ApplyReferredPersonNameSearch(q, query.Trim());
        }

        return q;
    }
}
