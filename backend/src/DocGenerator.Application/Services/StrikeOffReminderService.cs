using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

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
    private readonly IKeysetCursorCodec _cursorCodec;

    public StrikeOffReminderService(
        IDocumentRepository documents,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IKeysetCursorCodec cursorCodec)
    {
        _documents = documents;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _clock = clock;
        _timeZone = timeZone;
        _cursorCodec = cursorCodec;
    }

    public async Task<PagedResult<DocumentResponse>> GetAsync(
        int page, int perPage, int? visibleBranchId, int? visibleUserId, int? ownerSectionId,
        CancellationToken ct = default)
    {
        var (safePage, safePerPage) = NormalizePaging(page, perPage);
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);

        var (total, rows, _) = await _documents.SearchStrikeOffReminderIdsAsync(
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

    /// <summary>
    /// قائمة «تذكيرات الشطب» بإسقاط صف القائمة (P3-04c): نفس تصفية/ترقيم المعرّفات في SQL،
    /// ثم تحميل بطاقاتها بإسقاط خفيف — بلا `WithStandardIncludes` وبلا كيان كامل.
    /// </summary>
    public async Task<PagedResult<DocumentListItemDto>> GetListAsync(
        int page, int perPage, int? visibleBranchId, int? visibleUserId, int? ownerSectionId,
        CancellationToken ct = default, string? cursor = null)
    {
        var (safePage, safePerPage) = NormalizePaging(page, perPage);
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);

        var keyset = _cursorCodec.DecodeOptional(cursor, DocumentListCursorKinds.ReminderLastActivity);
        var (total, rows, hasMore) = await _documents.SearchStrikeOffReminderIdsAsync(
            cutoff, visibleBranchId, visibleUserId, safePage, safePerPage, ct, ownerSectionId, keyset);

        var items = new List<DocumentListItemDto>(rows.Count);
        if (rows.Count > 0)
        {
            // الترتيب من SQL (الأقدم ركودًا) يُعاد صريحًا بعد التحميل — التحميل لا يضمن الترتيب.
            var ids = rows.Select(r => r.Id).ToList();
            var byId = (await _documents.GetListRowsByIdsAsync(ids, ct))
                .ToDictionary(d => d.Id);
            var currentYear = ServerClock.CurrentYear(_clock, _timeZone);

            foreach (var row in rows)
            {
                if (byId.TryGetValue(row.Id, out var listRow))
                    items.Add(DocumentListItemDto.FromRow(listRow, currentYear));
            }
        }

        string? nextCursor = null;
        if (hasMore && rows.Count > 0)
        {
            var last = rows[^1];
            nextCursor = _cursorCodec.Encode(
                DocumentListCursorKinds.ReminderLastActivity, last.LastActivityUtc, last.Id);
        }

        return new PagedResult<DocumentListItemDto>
        {
            Items = items,
            Page = cursor is null ? safePage : 1,
            PerPage = safePerPage,
            TotalCount = total,
            NextCursor = nextCursor,
        };
    }

    public async Task<int> GetCountAsync(
        int? visibleBranchId, int? visibleUserId, int? ownerSectionId, CancellationToken ct = default)
    {
        var cutoff = StrikeOffReminderCalculator.CutoffUtc(ServerClock.Now(_clock, _timeZone), _timeZone);
        var (total, _, _) = await _documents.SearchStrikeOffReminderIdsAsync(
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
