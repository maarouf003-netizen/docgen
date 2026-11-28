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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        // فاصل حتمي `ThenByDescending(Id)`: الترتيب بـ`CreatedAt` وحده غير حتمي عند التساوي
        // (إدخال دفعي/استيراد)، فتصبح حدود الصفحات الإزاحية قابلة للتكرار أو للفقدان بين
        // الجلبين — العيب نفسه الذي دفع قوائم التنبيهات إلى keyset (§9.1).
        var items = await WithStandardIncludes(
            q.OrderByDescending(d => d.CreatedAt)
                .ThenByDescending(d => d.Id)
                .Skip((page - 1) * perPage)
                .Take(perPage))
            .ToListAsync(ct);

        return (total, items);
    }

    /// <summary>
    /// قائمة الملفات بإسقاط صف القائمة (P3-04b): لا `WithStandardIncludes` ولا كيان
    /// كامل. تُجمع العناصر الفرعية اللازمة لبطاقة القائمة دفعةً واحدة لمعرّفات الصفحة
    /// ثم تُغذّي `DocumentResponse.FromEntity` على كيان مُجمَّع في الخدمة.
    /// </summary>
    public async Task<(int TotalCount, List<DocumentListRow> Items, bool HasMore)> SearchListAsync(
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
        CancellationToken ct = default,
        int? ownerSectionId = null,
        KeysetCursor? cursor = null)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ownerSectionId);

        var total = await q.CountAsync(ct);

        if (cursor is not null)
        {
            var value = cursor.SortValue
                ?? throw new ArgumentException("مؤشر الصفحة غير صالح — أعد تحميل القائمة");
            q = q.Where(d => d.CreatedAt < value || (d.CreatedAt == value && d.Id < cursor.Id));
        }

        var items = await q.OrderByDescending(d => d.CreatedAt)
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
    /// الصفوف المُسقَطة لمجموعة معرّفات محمَّلة سلفًا (P3-04c): يُستخدم في «تذكيرات الشطب»
    /// حيث تُختار المعرّفات والترتيب في SQL ثم تُحمَّل بطاقاتها بإسقاط خفيف — مطابقًا
    /// لدلالات `GetByIdsWithStandardIncludesAsync` لكن بلا شجرة تحميل كاملة.
    /// </summary>
    public async Task<List<DocumentListRow>> GetListRowsByIdsAsync(List<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return new List<DocumentListRow>();

        var items = await Db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(DocumentListRowProjection.Selector)
            .ToListAsync(ct);

        await DocumentListRowProjection.LoadChildrenAsync(Db, items, ct);
        return items;
    }

    public async Task<List<DocumentExportRow>> ExportRowsAsync(
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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        // الإسقاط العمودي للتصدير الكبير: نفس فلاتر البحث وترتيب القوائم، لكن
        // بإسقاط أعمدة الورقة فقط (بلا `WithStandardIncludes` للشجرة الكاملة
        // المكوّنة من ~16 ملحقًا) — فينهار استهلاك الذاكرة لكل صف من عشرات
        // الكيلوبايتات إلى حفنة حقول. القوائم الصغيرة (0-3 عناصر) تُسقط كاملة
        // مرتبة بـ`Id` ليطابق «الأول» سلوك التحميل العملي؛ `BaseNumbers` بذور
        // (سنة/زمن/رقم) ينتخب `LatestFrom` منها الرقم الفعّال في الخدمة.
        // `AsSplitQuery` يُبقيها حفنة استعلامات ثابتة العدد مهما بلغت الصفوف.
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ownerSectionId);

        return await q.OrderByDescending(d => d.CreatedAt)
            .AsSplitQuery()
            .Select(d => new DocumentExportRow
            {
                AdministrativeBranchName = d.Branch != null ? d.Branch.Name : null,
                BranchName = d.BranchName,
                Court = d.Court,
                ExecutionCircuitId = d.ExecutionCircuitId,
                // FR-013: فحص صريح للقيم الفارغة بدل تحذير `CS8602` — الدلالة مطابقة في `SQL`
                // (انتشار `NULL` في الإسقاط) ولا تغيير سلوكي.
                SectionName = d.ExecutionCircuit != null && d.ExecutionCircuit.Section != null
                    ? d.ExecutionCircuit.Section.Name
                    : null,
                IsDraft = d.IsDraft,
                FileNumber = d.FileNumber,
                FileType = d.FileType,
                FileYear = d.FileYear,
                AnnexNumber = d.AnnexNumber,
                Annexes = d.Annexes
                    .OrderBy(a => a.Sequence)
                    .Select(a => new DocumentAnnexDto(a.Id, a.AnnexType, a.AnnexNumber, a.AnnexDate, a.Sequence))
                    .ToList(),
                Lawyer = d.Lawyer,
                GeneralEntitySide = d.GeneralEntitySide,
                ExecutedStatus = d.ExecutedStatus,
                ExecStatus = d.ExecStatus,
                ExecSubStatus = d.ExecSubStatus,
                Applicant = d.Applicant,
                BorrowerName = d.BorrowerName,
                BorrowerFather = d.BorrowerFather,
                BorrowerFamily = d.BorrowerFamily,
                ExecutionApplicants = d.ExecutionApplicants
                    .OrderBy(a => a.Id)
                    .Select(a => new ExportApplicant { Name = a.Name, Father = a.Father, Family = a.Family })
                    .ToList(),
                ExecutedPublicEntities = d.ExecutedPublicEntities
                    .OrderBy(e => e.Id)
                    .Select(e => new ExportPublicEntity { EntityName = e.EntityName })
                    .ToList(),
                ExecutedNaturalPersons = d.ExecutedNaturalPersons
                    .OrderBy(p => p.Id)
                    .Select(p => new ExportNaturalPerson { Name = p.Name, Father = p.Father, Family = p.Family })
                    .ToList(),
                ExecutionActions = d.ExecutionActions
                    // أحدث إجراء/ملاحظة — مطابق لعرض التفاصيل (`OrderByDescending(CreatedAt)`)
                    // ولبوابة التصدير؛ `Id` فاصل حتمي عند تساوي الوقت.
                    .OrderByDescending(a => a.CreatedAt)
                    .ThenByDescending(a => a.Id)
                    .Take(1)
                    .Select(a => new ExportAction { Text = a.Text })
                    .ToList(),
                BaseNumbers = d.BaseNumbers
                    .Select(b => new DocumentBaseNumber
                    {
                        Year = b.Year,
                        CreatedAt = b.CreatedAt,
                        BaseNumber = b.BaseNumber,
                        ExecutionCircuitId = b.ExecutionCircuitId,
                        VoidedAt = b.VoidedAt,
                    })
                    .ToList(),
            })
            .ToListAsync(ct);
    }

    public async Task<int> CountExportAsync(
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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        IQueryable<Document> q = ApplySearchFilters(
            Db.Documents.AsNoTracking(), query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ownerSectionId);

        return await q.CountAsync(ct);
    }

    /// <summary>
    /// قاعدة نطاق الملفات (§5 — قرار §2.21): مدير/مشرف (بلا قيود)؛ محامٍ
    /// (ملفاته)؛ رئيس شعبة (ملفات دوائر شعبته حصرًا — بلا ملفات بلا دائرة، §5.6)؛
    /// رئيس قسم (ملفات دوائر القسم + ملفات بلا دائرة، §5.6). تُستدعى من كل
    /// مسارات القوائم والتصدير والعدّة — نقطة واحدة لا منطق متناثر.
    /// </summary>
    private static IQueryable<Document> ApplyOwnerScope(
        IQueryable<Document> q,
        int? visibleBranchId,
        int? visibleUserId,
        int? ownerSectionId)
    {
        if (visibleBranchId.HasValue)
            q = q.Where(d => d.BranchId == visibleBranchId);

        if (visibleUserId.HasValue)
            return q.Where(d => d.CreatedById == visibleUserId);

        if (ownerSectionId.HasValue)
            return q.Where(d => d.ExecutionCircuitId != null && d.ExecutionCircuit!.SectionId == ownerSectionId.Value);

        if (visibleBranchId.HasValue)
            return q.Where(d => d.ExecutionCircuitId == null || d.ExecutionCircuit!.SectionId == null);

        return q;
    }

    private IQueryable<Document> ApplySearchFilters(
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
        int? visibleUserId,
        int? ownerSectionId = null)
    {
        // صرامة فلتر «الحالة»: أي قيمة خارج الكتالوج تُرفض (400 عبر المعالج العام) بدل
        // السقوط الصامت في فرع «متداول» — وتُقلَّم القيمة أولًا فيُقبل المبطَّن بدلالته.
        // الموضع هنا (أول الدالة) يغطي البوابات الأربع المشتركة: البحث والتصدير
        // وعدّ التصدير وخيارات الفلاتر.
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        if (!ExecutionStatusCatalog.IsValidSearchFilter(status))
            throw new ArgumentException("قيمة فلتر الحالة غير صالح");
        q = ApplyOwnerScope(q, visibleBranchId, visibleUserId, ownerSectionId);
        // الملفات «المنفذة» (عائلة «منفذ عليه»/«عرض وايداع» بحالة «منفذ»، وملفات «طالبة تنفيذ»
        // المنفذة بالتسوية أو الجبري الكامل)، وملفات «طالبة تنفيذ» المشطوبة («مشطوب») أو
        // «محال الى البداية» تُخفى من القائمة والتصدير العام إلا عند البحث النصي عنها
        // (query)، فتظهر للعثور عليها — بحث شامل لكل الملفات غير المحذوفة أيا كانت حالتها.
        // التعريفات مطابقة تمامًا لصفحات «الملفات المنفذة» (SearchExecutedAsync) و«الملفات
        // المشطوبة» (SearchStruckOffAsync) و«محال الى البداية» (SearchReferredToStartAsync)
        // فلا يتسرب أي ملف منها إلى القائمة في غير ذلك.
        if (string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(d =>
                d.ExecutedStatus != ExecutedStatusCatalog.StruckOff
                && d.ExecStatus != ExecutionStatusCatalog.StateStruckOff
                && d.ExecStatus != ExecutionStatusCatalog.ReferredToStart
                && !((d.GeneralEntitySide == GeneralEntitySideCatalog.Executed
                    || d.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
                    && d.ExecutedStatus == ExecutedStatusCatalog.Executed)
                && !(d.GeneralEntitySide == GeneralEntitySideCatalog.Applicant
                    && (d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                        || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                        || d.ExecStatus == ExecutionStatusCatalog.Recovered
                        // صيغة صريحة آمنة لـ NULL (H4): الجزئي مطابقة تامة فقط —
                        // NULL منفذ كامل. (سلوكيًا مكافئ لمنطق SQL الثلاثي هنا،
                        // لكن القصد مقروء ولا يعتمد على مصادفة التقييم.)
                        || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                            && (d.ExecSubStatus == null
                                || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)))));
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            // «منفذ جبريا + منفذ جزئيا» متداول دائمًا بقرار المالك (مرآة `IsExecuted`
            // ولوحة المدير وآلة الحالات): يُستبعد من فلتر «منفذ» هنا وفي صفحة
            // «الملفات المنفذة» معًا فلا ينفصلان. الصيغة آمنة للقيم الفارغة
            // (`ExecSubStatus` قد تكون null في صفوف قديمة — تُعامل منفذًا كاملًا
            // مطابقةً لدلالة C# في `IsExecuted`، لا لمنطق SQL الثلاثي).
            if (status == ExecutionStatusCatalog.ExecutedFilter)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.ExecutedBySettlement
                    || d.ExecStatus == ExecutionStatusCatalog.DelegationExecuted
                    || d.ExecStatus == ExecutionStatusCatalog.Recovered
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && (d.ExecSubStatus == null
                            || d.ExecSubStatus != ExecutionStatusCatalog.SubPartiallyExecuted)));
            else if (status == ExecutionStatusCatalog.Deferred)
                q = q.Where(d => d.ExecStatus == ExecutionStatusCatalog.Deferred);
            else if (status == ExecutionStatusCatalog.StateCirculating)
                // «منفذ جبريا + منفذ جزئيا» متداول دائمًا بقرار المالك (H3 — مرآة
                // فلتر البوابة): مطابقة تامة هنا، وصيغة آمنة لـ NULL (الجزئي مطابقة
                // تامة فقط — NULL يبقى منفذًا كاملًا).
                q = q.Where(d => (string.IsNullOrEmpty(d.ExecStatus) && !d.IsDraft)
                    || (d.ExecStatus == ExecutionStatusCatalog.ExecutedForcibly
                        && d.ExecSubStatus == ExecutionStatusCatalog.SubPartiallyExecuted));
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
            q = ApplyPersonNameSearch(q, query.Trim());
        }

        return q;
    }

    /// <summary>
    /// كتلة البحث النصي الشاملة الموحدة (أسماء الأطراف): تُفوَّض إلى
    /// <see cref="DocumentPartySearch"/> — المصدر الوحيد لمنطق مطابقة الأسماء في القائمة
    /// الرئيسية وصفحات «الملفات المشطوبة» و«المنفذة» و«المحذوفة». صفحة «محال الى البداية»
    /// وحدها تستخدم الكتلة المضيَّقة (ApplyReferredPersonNameSearch).
    /// </summary>
    private IQueryable<Document> ApplyPersonNameSearch(IQueryable<Document> q, string term)
        => DocumentPartySearch.Apply(q, Db, term, includeExecutedFamily: true, includeAppeals: true);

    /// <summary>
    /// كتلة البحث الخاصة بصفحة «محال الى البداية»: تفويض إلى <see cref="DocumentPartySearch"/>
    /// مع استبعاد فروع عائلة «منفذ عليه» الثلاثة (تضييق معتمد 2026-09-24). يحرس التضييق
    /// اختبارا ArchiveSearch_ReferredPage_MatchesApplicantSideBranches
    /// وArchiveSearch_ReferredPage_IgnoresExecutedFamilyTables.
    /// </summary>
    private IQueryable<Document> ApplyReferredPersonNameSearch(IQueryable<Document> q, string term)
        => DocumentPartySearch.Apply(q, Db, term, includeExecutedFamily: false, includeAppeals: true);

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
            .Include(d => d.Annexes)
            // الدائرة وشعبتها للتوجيه بالنطاق (§5 — قرار §2.21): بدونهما يبقى
            // `SectionId` فارغًا بصمت في منطق النطاق.
            .Include(d => d.ExecutionCircuit)
                .ThenInclude(c => c!.Section)
            .Include(d => d.ExecutionApplicants)
            .Include(d => d.ExecutedPublicEntities)
            .Include(d => d.ExecutedNaturalPersons)
            .Include(d => d.ExecutedHeirs)
            .Include(d => d.Occurrences)
            .ThenInclude(o => o.CreatedBy)
            .Include(d => d.Interventions)
            .ThenInclude(i => i.Heirs)
            .Include(d => d.ApplicantPublicEntities)
            .Include(d => d.Assignments)
            // صف الإنابة المصدر: تاريخ إعادة الملف للدائرة المنيبة في «تاريخ التنفيذ»
            // لصفحة الملفات المنفذة (المناب).
            .Include(d => d.SourceDelegation);

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
        CancellationToken ct = default,
        int? ownerSectionId = null)
    {
        // كل قائمة تُقيَّد بباقي الفلاتر النشطة ما عدا فلتر الحقل نفسه،
        // فيلتزم الاختيار اللاحق بنتائج الفلتر السابق بأسلوب إكسل.
        IQueryable<Document> Base(string? st, string? ap, string? co, string? lw, string? br, string? ab, string? ee, string? peb) =>
            ApplySearchFilters(Db.Documents.AsNoTracking(), null, st, ap, co, lw, br, ab, ee, peb, visibleBranchId, visibleUserId, ownerSectionId);

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

}
