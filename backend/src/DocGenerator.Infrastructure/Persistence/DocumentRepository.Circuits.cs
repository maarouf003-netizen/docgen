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
    public async Task<List<Document>> GetByIdsAsync(List<int> ids, CancellationToken ct = default)
    {
        return await Db.Documents
            .AsNoTracking()
            .Include(d => d.BaseNumbers)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);
    }

    /// <summary>
    /// تحميل مجموعة معرّفات بشجرة `WithStandardIncludes` المعتمدة (لعرض `DocumentResponse`
    /// كاملًا في قوائم تُفلتر/تُرقَّم في SQL أولًا ثم تُحمَّل بالترتيب المطلوب في الخدمة).
    /// </summary>
    public async Task<List<Document>> GetByIdsWithStandardIncludesAsync(List<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new List<Document>();

        return await WithStandardIncludes(Db.Documents.AsNoTracking())
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);
    }

    public async Task<List<int>> ListOwnerIdsByCircuitIdsAsync(
        IReadOnlyCollection<int> circuitIds,
        CancellationToken ct = default)
    {
        if (circuitIds.Count == 0)
            return new List<int>();
        return await Db.Documents
            .AsNoTracking()
            .Where(d => d.ExecutionCircuitId != null && circuitIds.Contains(d.ExecutionCircuitId.Value))
            .Select(d => d.CreatedById)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<List<Document>> ListByCircuitAsync(int circuitId, bool includeDeleted, CancellationToken ct = default)
    {
        // شمول كامل (لا BaseNumbers فقط): التسمية الجماعية تعيد بناء الثلاثية المشتقة
        // (CourtNorm + SearchText + FullData) — وبناء SearchText من كيان ناقص التحميل
        // يُسقط شروط الورثة/الكفلاء صامتًا. التسمية نادرة إداريًا فالتكلفة مقبولة.
        IQueryable<Document> q = WithStandardIncludes(Db.Documents);
        if (includeDeleted)
            q = q.IgnoreQueryFilters();
        return await q.Where(d => d.ExecutionCircuitId == circuitId).ToListAsync(ct);
    }

    public async Task<List<Document>> ListCircuitFileSummariesAsync(int circuitId, CancellationToken ct = default)
    {
        // إسقاط خفيف لنافذة الإفراغ: الحقول المعروضة كلها أعمدة مباشرة على Document —
        // بلا WithStandardIncludes عمدًا (لا إعادة بناء مشتقات هنا)، والفلتر العام
        // يخفي المحذوفة منطقيًا تلقائيًا، وترتيب ثابت بالمعرف لعرض مستقر.
        return await Db.Documents
            .AsNoTracking()
            .Where(d => d.ExecutionCircuitId == circuitId)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<List<Document>> ListByIdsForUpdateAsync(List<int> ids, CancellationToken ct = default)
    {
        // شمول كامل لذات سبب ListByCircuitAsync: الإحالة/إعادة القيد تعيدان بناء
        // الثلاثية المشتقة — والبناء من كيان ناقص يُسقط الشروط صامتًا.
        return await WithStandardIncludes(Db.Documents)
            .Where(d => ids.Contains(d.Id))
            .ToListAsync(ct);
    }

    public async Task<List<Document>> ListPendingForLawyerAsync(int lawyerId, int? circuitId, CancellationToken ct = default)
    {
        IQueryable<Document> q = Db.Documents
            .AsNoTracking()
            .Include(d => d.ExecutionCircuit)
            .Where(d => d.CreatedById == lawyerId && d.NeedsRegistration && !d.IsDeleted);
        if (circuitId is not null)
            q = q.Where(d => d.ExecutionCircuitId == circuitId);
        return await q.OrderBy(d => d.Id).ToListAsync(ct);
    }

    public async Task<Dictionary<int, (int FileCount, int PendingCount, int DeletedCount)>> CountByCircuitsAsync(List<int> circuitIds, CancellationToken ct = default)
    {
        var result = circuitIds.Distinct().ToDictionary(id => id, _ => (FileCount: 0, PendingCount: 0, DeletedCount: 0));
        if (result.Count == 0) return result;
        var ids = result.Keys.ToList();
        // IgnoreQueryFilters دفعة واحدة ثم تصنيف في الذاكرة: حار/معلق/محذوف —
        // حجم حالة الاستعلام واحدة وعدد الصفوف = صفوف الدوائر فقط.
        var rows = await Db.Documents
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.ExecutionCircuitId != null && ids.Contains(d.ExecutionCircuitId.Value))
            .Select(d => new { d.ExecutionCircuitId, d.NeedsRegistration, d.IsDeleted })
            .ToListAsync(ct);
        foreach (var g in rows.GroupBy(r => r.ExecutionCircuitId!.Value))
        {
            result[g.Key] = (
                g.Count(r => !r.IsDeleted),
                g.Count(r => !r.IsDeleted && r.NeedsRegistration),
                g.Count(r => r.IsDeleted));
        }
        return result;
    }

    public async Task<List<Document>> ListDeletedByCircuitAsync(int circuitId, CancellationToken ct = default)
    {
        // صفوف سلة المحذوفات لدائرة (بترتيب ثابت) — متتبَّعة قصدًا (بلا AsNoTracking)
        // كي تُحذف عبر Remove، وبلا WithStandardIncludes: الحذف التسلسلي تكفله
        // قيود FK في القاعدة (CASCADE/SET NULL) والكيان المحمَّل لا يحتاج نوافيجيشن.
        return await Db.Documents
            .IgnoreQueryFilters()
            .Where(d => d.ExecutionCircuitId == circuitId && d.IsDeleted)
            .OrderBy(d => d.Id)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<int, int>> MapMirrorsBySourceDelegationIdsAsync(List<int> delegationIds, CancellationToken ct = default)
    {
        var ids = (delegationIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<int, int>();
        // المناب صف واحد لكل إنابة (فهرس SourceDelegationId الوحيد) — الفلتر العام
        // `!IsDeleted` يخفي منابًا محذوفًا منطقيًا (غير مرئي أصلًا فلا توثيق نقله)؛
        // ولتكرار حدوث ما يمكن (احترازًا نظريًا فقط) يُختار أقدم Id.
        var rows = await Db.Documents
            .AsNoTracking()
            .Where(d => d.SourceDelegationId != null && ids.Contains(d.SourceDelegationId.Value))
            .Select(d => new { DelegationId = d.SourceDelegationId!.Value, MirrorId = d.Id })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.DelegationId)
            .ToDictionary(g => g.Key, g => g.Min(r => r.MirrorId));
    }

    public async Task<List<DocumentOccurrence>> ListLastCircuitReferralsAsync(List<int> documentIds, CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return new List<DocumentOccurrence>();
        var rows = await Db.DocumentOccurrences
            .AsNoTracking()
            .Where(o => documentIds.Contains(o.DocumentId) && o.OccurrenceType == OccurrenceTypeCatalog.CircuitReferred)
            .OrderByDescending(o => o.Id)
            .ToListAsync(ct);
        return rows.GroupBy(o => o.DocumentId).Select(g => g.First()).ToList();
    }

    public async Task<Dictionary<int, int>> CountLawyersByCircuitsAsync(List<int> circuitIds, CancellationToken ct = default)
    {
        var ids = circuitIds.Distinct().ToList();
        var result = ids.ToDictionary(id => id, _ => 0);
        if (ids.Count == 0) return result;
        var rows = await Db.Documents
            .AsNoTracking()
            .Where(d => d.ExecutionCircuitId != null && ids.Contains(d.ExecutionCircuitId.Value) && !d.IsDeleted)
            .GroupBy(d => d.ExecutionCircuitId!.Value)
            .Select(g => new { CircuitId = g.Key, LawyerCount = g.Select(d => d.CreatedById).Distinct().Count() })
            .ToListAsync(ct);
        foreach (var r in rows)
            result[r.CircuitId] = r.LawyerCount;
        return result;
    }

    public async Task<(int FileCount, int PendingCount, int LawyerCount)> CountWithoutCircuitAsync(int? branchId, CancellationToken ct = default)
    {
        // F4: ملفات بلا دائرة حصرًا (`ExecutionCircuitId == null`) — دوائر `SectionId == null`
        // لها صفوفها الخاصة فلا تُجمع هنا (منع ازدواج العدّ)؛ غير المحذوفة مرآةً لعدادات الدوائر.
        // استعلام تجميعي واحد (D2): `COUNT(CASE…)` للمعلقات و`COUNT(DISTINCT…)` للمحامين —
        // نفس بنية `CountLawyersByCircuitsAsync` المثبتة الترجمة على SQLite/PostgreSQL.
        var q = Db.Documents
            .AsNoTracking()
            .Where(d => d.ExecutionCircuitId == null && !d.IsDeleted);
        if (branchId.HasValue)
            q = q.Where(d => d.BranchId == branchId.Value);
        var row = await q
            .GroupBy(_ => 1)
            .Select(g => new
            {
                FileCount = g.Count(),
                PendingCount = g.Count(d => d.NeedsRegistration),
                LawyerCount = g.Select(d => d.CreatedById).Distinct().Count(),
            })
            .SingleOrDefaultAsync(ct);
        return row is null ? (0, 0, 0) : (row.FileCount, row.PendingCount, row.LawyerCount);
    }

    // ملاحظة (M12): التنفيذ النصي القديم ExistsActiveWithNumberAsync أُسقط مع ترحيل
    // آخر مناديه إلى التوقيع الدائري أدناه — تاريخه في Git فقط.

    public async Task<bool> ExistsActiveWithNumberByCircuitAsync(
        int? excludeDocumentId,
        int? circuitId,
        string? courtNormFallback,
        string? number,
        string? type,
        string? year,
        CancellationToken ct = default)
    {
        var n = number?.Trim();
        var y = year?.Trim();
        if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(y))
            return false;
        var t = type?.Trim() ?? string.Empty;
        var yearInt = int.TryParse(y, out var parsed) ? parsed : (int?)null;
        if (circuitId is not null)
        {
            // مطابقة FK مباشرة (البند 28) — تشمل أرقام الأساس الدورية.
            return await Db.Documents
                .AsNoTracking()
                .Where(d => !d.IsDeleted)
                .Where(d => excludeDocumentId == null || d.Id != excludeDocumentId)
                .Where(d => d.ExecutionCircuitId == circuitId
                    && (d.FileType ?? string.Empty).Trim() == t)
                .Where(d => (d.FileNumber != null && d.FileNumber.Trim() == n
                        && d.FileYear != null && d.FileYear.Trim() == y)
                    || (yearInt != null && d.BaseNumbers.Any(b =>
                        b.ExecutionCircuitId == circuitId
                        && b.VoidedAt == null
                        && b.Year == yearInt
                        && b.NumberKey == n)))
                .AnyAsync(ct);
        }
        // صفوف NULL (المناب الخارجي): الوحدانية النصية القديمة.
        var normC = courtNormFallback ?? string.Empty;
        return await Db.Documents
            .AsNoTracking()
            .Where(d => !d.IsDeleted)
            .Where(d => excludeDocumentId == null || d.Id != excludeDocumentId)
            .Where(d => d.ExecutionCircuitId == null
                && (d.CourtNorm ?? string.Empty) == normC
                && (d.FileType ?? string.Empty).Trim() == t)
            .Where(d => (d.FileNumber != null && d.FileNumber.Trim() == n
                    && d.FileYear != null && d.FileYear.Trim() == y)
                || (yearInt != null && d.BaseNumbers.Any(b => b.Year == yearInt && b.BaseNumber.Trim() == n)))
            .AnyAsync(ct);
    }

    public async Task<(int TotalCount, List<Document> Items)> GetRotationCandidatesAsync(
        int userId, int currentYear, int page, int perPage,
        bool allowCircuitlessLegacy = false, CancellationToken ct = default)
    {
        // المؤهل للتدوير: مقيد برقم ملف (ليس تحت رفع) وغير منفَّذ وغير محذوف
        // (Query Filter مطبق تلقائيًا)، ولم يُدوَّر في السنة الحالية (لا يملك رقم أساس لها).
        // قاعدة القيد: رقم الملف الأصلي هو نفسه رقم أساس سنة قيده، فالملف المقيد في السنة
        // الحالية يملك رقم أساس لها بالفعل (رقم ملفه) فلا يُدوَّر، والملفات من سنوات سابقة فقط
        // هي المؤهلة للتدوير في السنة الحالية.
        // عائلتا وضع «منفذ عليه» (Executed + Deposit) مؤهلتان متداولتين فقط (لا منفذ ولا مشطوب)
        // وبشرط وجود رقم أساس من سنة سابقة — التدوير فيهما استمرار لدوران سبق أن بدأ.
        // عدد الصفحات يُحسب على مستوى قاعدة البيانات لتجنب جلب آلاف الصفوف دفعة واحدة.
        IQueryable<Document> q = Db.Documents
            .AsNoTracking()
            .Where(d => d.CreatedById == userId)
            .Where(d => !d.IsDraft)
            // الملفات المعلقة (NULL الرقم آمن فريدًا): تُستبعد من التدوير حتى إعادة القيد.
            .Where(d => !d.NeedsRegistration);
        // v2/V-12: لا أهلية بلا صف سابق غير ملغى في دائرة الملف الحالية.
        // استثناء بلا دائرة مخصّص للاختبارات القائمة فقط (AllowCircuitlessLegacy).
        if (allowCircuitlessLegacy)
        {
            q = q.Where(d => d.BaseNumbers.Any(b => b.VoidedAt == null
                    && b.ExecutionCircuitId == d.ExecutionCircuitId
                    && b.Year < currentYear)
                || (d.ExecutionCircuitId == null
                    && d.GeneralEntitySide != GeneralEntitySideCatalog.Executed
                    && d.GeneralEntitySide != GeneralEntitySideCatalog.Deposit));
        }
        else
        {
            q = q.Where(d => d.BaseNumbers.Any(b => b.VoidedAt == null
                && b.ExecutionCircuitId == d.ExecutionCircuitId
                && b.Year < currentYear));
        }
        q = q
            .Where(d => (d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                ? d.ExecutedStatus == ExecutedStatusCatalog.None
                : d.ExecStatus != ExecutionStatusCatalog.ExecutedBySettlement
                    && d.ExecStatus != ExecutionStatusCatalog.DelegationExecuted
                    && d.ExecStatus != ExecutionStatusCatalog.Recovered
                    // الصيغة الصريحة نفسها (H4 — مكافئة سلوكيًا، ومقصودة قراءةً).
                    && !(d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && (d.ExecSubStatus == null
                            || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted))
                    && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff)
            .Where(d => !d.BaseNumbers.Any(b => b.VoidedAt == null
                && b.ExecutionCircuitId == d.ExecutionCircuitId
                && b.Year == currentYear))
            // توافق انتقالي: ملف بلا دائرة/بلا سجل أرقام ومسجّل لسنة القيد الحالية لا يُدوَّر.
            .Where(d => d.FileYear != currentYear.ToString(CultureInfo.InvariantCulture)
                || (d.ExecutionCircuitId != null
                    && d.BaseNumbers.Any(b => b.VoidedAt == null
                        && b.ExecutionCircuitId == d.ExecutionCircuitId)));

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

}
