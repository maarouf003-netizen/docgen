using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ «تذكيرات الشطب»: استعلام خطوتين — تصفية/عدّ/ترتيب/ترقيم في SQL (معرّفات + تاريخ
/// آخر نشاط)، ثم تحميل الكتبة بشجرة العرض المعتمدة وإعادة الترتيب في الخدمة. القراءة لا
/// تكتب شيئًا؛ والإلغاء هو الكتابة الوحيدة (عمود على الملف + تدقيق).
/// </summary>
public sealed class StrikeOffReminderService : IStrikeOffReminderService
{
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public StrikeOffReminderService(
        IDocumentRepository documents,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _documents = documents;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
    }

    public async Task<PagedResult<DocumentResponse>> GetAsync(
        int page, int perPage, int? visibleBranchId, int? visibleUserId, int? ownerSectionId,
        CancellationToken ct = default)
    {
        var (safePage, safePerPage) = NormalizePaging(page, perPage);
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);

        var (total, rows) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, safePage, safePerPage, ct, ownerSectionId);

        var items = new List<DocumentResponse>(rows.Count);
        if (rows.Count > 0)
        {
            // الترتيب من SQL (الأقدم ركودًا) يُعاد صريحًا بعد التحميل — التحميل لا يضمن الترتيب.
            var ids = rows.Select(r => r.Id).ToList();
            var docs = (await _documents.GetByIdsWithStandardIncludesAsync(ids, ct))
                .ToDictionary(d => d.Id);
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

            foreach (var row in rows)
            {
                if (docs.TryGetValue(row.Id, out var doc))
                    items.Add(DocumentResponse.FromEntity(doc, currentYear));
            }
        }

        return new PagedResult<DocumentResponse>
        {
            Items = items,
            Page = safePage,
            PerPage = safePerPage,
            TotalCount = total,
        };
    }

    public async Task<int> GetCountAsync(
        int? visibleBranchId, int? visibleUserId, int? ownerSectionId, CancellationToken ct = default)
    {
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);
        var (total, _) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, 1, 1, ct, ownerSectionId);
        return total;
    }

    public async Task<bool> DismissAsync(
        int documentId, int ownerId, string? actorName, CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct);
        // المالك فقط (النطاق مفروض في المتحكم بالدور، والتحقق هنا دفاعًا مزدوجًا).
        if (doc is null || doc.CreatedById != ownerId)
            return false;

        doc.StrikeOffReminderDismissedAt = DateTime.UtcNow;

        await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "strike_off_reminder_dismiss", doc.Id, doc.DocumentType,
                $"ألغى تذكير الشطب للملف (رقم {doc.Id})", token);
        }, ct);

        return true;
    }

    /// <summary>تطبيع الترقيم: صفحة ≥ 1 وحجم صفحة داخل [1, MaxPerPage] — بلا رفض طلب كامل.</summary>
    private static (int Page, int PerPage) NormalizePaging(int page, int perPage)
    {
        var safePage = page < 1 ? 1 : page;
        var safePerPage = perPage < 1
            ? StrikeOffReminderCalculator.DefaultPerPage
            : Math.Min(perPage, StrikeOffReminderCalculator.MaxPerPage);
        return (safePage, safePerPage);
    }
}
