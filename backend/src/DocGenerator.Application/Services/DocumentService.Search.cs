using System.Text.Json;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Security;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class DocumentService
{
    public async Task<PagedResult<DocumentResponse>> SearchDeletedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchDeletedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchStruckOffAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchStruckOffAsync(query, null, null, null, null, null, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchExecutedAsync(
        string? query, int page, int perPage, int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchExecutedAsync(query, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<PagedResult<DocumentResponse>> SearchAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch, int page, int perPage,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, items) = await _documents.SearchAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, page, perPage, ct);

        return new PagedResult<DocumentResponse>
        {
            Items = items.Select(DocumentResponse.FromEntity).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    public async Task<DocumentFilterOptions> GetFilterOptionsAsync(
        string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
        => await _documents.GetFilterOptionsAsync(status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct);

    public async Task<List<DocumentResponse>> ExportAsync(
        string? query, string? status, string? applicant, string? court, string? lawyer, string? branch, string? administrativeBranch, string? executedEntity, string? publicEntityBranch,
        int? visibleBranchId = null, int? visibleUserId = null, CancellationToken ct = default)
    {
        // سقف التصدير: يُعدَّل عدد النتائج المطابقة أولًا قبل جلب أي صف إلى الذاكرة،
        // فيُرفض التصدير الواسع برسالة واضحة بدل ذروة ذاكرة غير محصورة على الخادم.
        var total = await _documents.CountExportAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var items = await _documents.ExportAsync(
            query, status, applicant, court, lawyer, branch, administrativeBranch, executedEntity, publicEntityBranch, visibleBranchId, visibleUserId, ct);
        return items.Select(DocumentResponse.FromEntity).ToList();
    }


    public async Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        perPage = Math.Clamp(perPage, 1, 100);

        var (total, docs) = await _documents.GetRotationCandidatesAsync(userId, page, perPage, ct);
        var currentYear = DateTime.Today.Year;
        var items = docs
            .Select(d => new RotationDocumentDto(
                d.Id,
                d.Court,
                d.BorrowerName,
                d.BorrowerFather,
                d.BorrowerFamily,
                d.FileNumber,
                d.FileType,
                d.BaseNumbers.FirstOrDefault(b => b.Year == currentYear)?.BaseNumber,
                RotationDisplayName(d)))
            .ToList();

        return new PagedResult<RotationDocumentDto>
        {
            Items = items,
            Page = page,
            PerPage = perPage,
            TotalCount = total,
        };
    }

    /// <summary>
    /// اسم العرض للملف في صفحة التدوير. لعائلتي وضع «منفذ عليه» (Executed + Deposit) لا يوجد
    /// مقترض، فأُعرض اسم طالب العرض (الجهة العامة أو الشخص الطبيعي) كما تُعرض في ملفات الإكسل
    /// والطباعة، ولو تعذّر توافره فلا أسمًا. ولنظام «طالبة تنفيذ» تُستخدم أسماء المقترض المعتادة.
    /// </summary>
    private static string RotationDisplayName(Document d)
    {
        if (!GeneralEntitySideCatalog.IsExecutedLike(d.GeneralEntitySide))
            return FormatName(d.BorrowerName, d.BorrowerFather, d.BorrowerFamily);

        var applicant = d.ExecutionApplicants
            .Select(a => FormatName(a.Name, a.Father, a.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(applicant))
            return applicant;

        var entity = d.ExecutedPublicEntities
            .Select(e => e.EntityName)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        if (!string.IsNullOrWhiteSpace(entity))
            return entity;

        return d.ExecutedNaturalPersons
            .Select(p => FormatName(p.Name, p.Father, p.Family))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    private static string FormatName(string? first, string? father, string? family) =>
        string.Join(' ', new[] { first, father, family }.Where(v => !string.IsNullOrWhiteSpace(v)));

    public async Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        if (doc is null)
            return new List<BaseNumberHistoryDto>();

        return doc.BaseNumbers
            .OrderByDescending(b => b.Year)
            .Select(b => new BaseNumberHistoryDto(b.Year, b.BaseNumber))
            .ToList();
    }

    public async Task SaveBaseNumbersAsync(int userId, List<BaseNumberEntry> entries, string? actorName, CancellationToken ct = default)
    {
        if (entries is null || entries.Count == 0)
            return;

        // حماية من طلب غير صالح يحتوي عنصرًا فارغًا — نرفضه بدل خطأ داخلي.
        if (entries.Any(e => e is null))
            throw new ArgumentException("طلب تدوير غير صالح");

        var year = DateTime.Today.Year;

        // منع تكرار نفس الملف داخل الطلب (سلوك غامض) — يُرفض الطلب كاملًا.
        var unique = entries
            .GroupBy(e => e.DocumentId)
            .Select(g => g.Last())
            .ToList();
        if (unique.Count != entries.Count)
            throw new ArgumentException("لا يمكن تكرار نفس الملف في طلب التدوير");

        await _tx.RunAsync(async token =>
        {
            var docs = await _documents.GetByIdsAsync(unique.Select(e => e.DocumentId).ToList(), token);
            var found = docs.ToDictionary(d => d.Id);
            var auditEntries = new List<AuditLogEntry>(unique.Count);

            foreach (var entry in unique)
            {
                if (!found.TryGetValue(entry.DocumentId, out var doc))
                    throw new ArgumentException("أحد الملفات غير موجود أو محذوف");
                if (doc.CreatedById != userId)
                    throw new ArgumentException("لا يمكن تعديل رقم أساس لملف لا يملكه المحامي");

                // الأهلية: مقيد برقم ملف وغير مقيد في السنة الحالية — حتى لا يُدوَّر محتوى
                // الملف تحت رفع أو ملفٌ حديث عهد قيده (رقم ملفه الأصلي هو نفسه رقم أساس سنته،
                // فلا يحتاج رقم أساس جديدًا لهذه السنة).
                // الفحص العائلي قطعي ومطابق حرفيًا لعبارة القائمة في المستودع: عائلتا وضع
                // «منفذ عليه» (Executed + Deposit) تُحكمان حصريًا بحالة الوضع (متداولٌ فقط = لا
                // منفذ ولا مشطوب) وبشرط وجود رقم أساس من سنة سابقة — التدوير فيهما استمرار لدوران
                // سبق أن بدأ، ولا تُعدُّ حالة نظام «طالبة تنفيذ» (ExecStatus/ExecSubStatus) ذات
                // فائدة فيهما. أما نظام «طالبة تنفيذ» فشرطه القديم نفسه.
                if (doc.IsDraft || doc.FileYear == year.ToString())
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                var eligible = GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                    ? doc.ExecutedStatus == ExecutedStatusCatalog.None
                        && doc.BaseNumbers.Any(b => b.Year < year)
                    : !ExecutionStatusCatalog.IsExecuted(doc.ExecStatus, doc.ExecSubStatus);
                if (!eligible)
                    throw new ArgumentException($"الملف (رقم {doc.Id}) غير مؤهل للتدوير");

                var normalized = entry.BaseNumber?.Trim();
                if (string.IsNullOrEmpty(normalized))
                {
                    // إلغاء رقم أساس السنة الحالية: حذف السجل فقط مع الاحتفاظ بأرقام السنوات السابقة.
                    var existing = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
                    if (existing is null)
                        continue;

                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"ألغى رقم أساس {year}: {existing.BaseNumber}", doc)));
                    _baseNumbers.Remove(existing);
                    continue;
                }

                if (normalized.Length > 50)
                    throw new ArgumentException("رقم الأساس يتجاوز الطول المسموح");

                var record = doc.BaseNumbers.FirstOrDefault(b => b.Year == year);
                if (record is null)
                {
                    record = new DocumentBaseNumber
                    {
                        DocumentId = doc.Id,
                        Year = year,
                        BaseNumber = normalized,
                        CreatedById = userId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"دوّر الملف برقم أساس {year}: {normalized}", doc)));
                    await _baseNumbers.AddAsync(record, token);
                }
                else
                {
                    record.BaseNumber = normalized;
                    record.UpdatedAt = DateTime.UtcNow;
                    auditEntries.Add(new AuditLogEntry(actorName, "rotate", doc.Id, doc.DocumentType,
                        AuditWithActor($"حدّث رقم أساس {year}: {normalized}", doc)));
                    _baseNumbers.Update(record);
                }
            }

            // حفظ ذرّي واحد لكل تغييرات الأرقام ثم دفعة تدقيق واحدة — بدل حفظٍ لكل ملف
            // (مهم في الحسابات الكبيرة التي قد تصل لآلاف الملفات).
            await _uow.SaveChangesAsync(token);
            await _audit.LogManyAsync(auditEntries, token);
        }, ct);
    }

}
