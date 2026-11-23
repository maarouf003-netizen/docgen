using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

public partial class HeadAlertRepository
{
    public async Task<HeadAlertSummaryPage> ListSummariesForRecipientAsync(
        int userId,
        int? branchId,
        AlertArchiveFilter archive,
        bool unreadOnly,
        int? delegationId,
        AlertCursor? cursor,
        int skip,
        int take,
        bool includeCounters,
        CancellationToken ct = default)
    {
        ValidatePaging(skip, take);

        // قراءة بالمستلم (§8): المحامي بلا قيد فرع، والرئيس/الشعبة بفرعه فقط.
        var query = Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.Recipients.Any(r => r.UserId == userId));

        if (branchId.HasValue)
            query = query.Where(a => a.BranchId == branchId.Value);
        if (unreadOnly)
            query = query.Where(a => a.Recipients.Any(r => r.UserId == userId && !r.IsRead));
        if (delegationId.HasValue)
            query = query.Where(a => a.DelegationId == delegationId.Value);

        return await ProjectSummariesAsync(
            ApplyArchiveFilter(query, archive), userId, includeCounters, cursor, skip, take, ct);
    }

    /// <summary>
    /// ملخّص صفٍّ واحد بالإسقاط نفسه الذي تبنيه القوائم (بلا <c>Include</c> ولا
    /// <c>Message</c> كامل)، ويُستعمل لجسم <c>201</c> بعد الإنشاء كي يطابق
    /// ما سيعيده صفُّ القائمة لاحقًا حقلًا بحقل.
    /// </summary>
    public async Task<HeadAlertSummaryDto?> GetSummaryAsync(
        int alertId,
        int? recipientUserId,
        bool includeCounters,
        CancellationToken ct = default)
    {
        var row = await SummaryRows(
                Db.HeadAlerts.AsNoTracking().Where(a => a.Id == alertId),
                recipientUserId,
                includeCounters)
            .FirstOrDefaultAsync(ct);

        return row is null ? null : ToSummaryDto(row);
    }

    public Task<HeadAlertSummaryPage> ListSummariesSentAsync(
        int userId,
        int branchId,
        AlertArchiveFilter archive,
        AlertCursor? cursor,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        ValidatePaging(skip, take);

        var query = Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.BranchId == branchId && a.CreatedById == userId);

        return ProjectSummariesAsync(
            ApplyArchiveFilter(query, archive), userId, includeCounters: true, cursor, skip, take, ct);
    }

    public Task<HeadAlertSummaryPage> ListSummariesForOversightAsync(
        AlertArchiveFilter archive,
        int? delegationId,
        AlertCursor? cursor,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        ValidatePaging(skip, take);

        var query = Db.HeadAlerts.AsNoTracking();
        if (delegationId.HasValue)
            query = query.Where(a => a.DelegationId == delegationId.Value);

        // حالة القراءة null (مستلم-محددة أصلًا)، والعدّادات تُحسب في القاعدة.
        return ProjectSummariesAsync(
            ApplyArchiveFilter(query, archive),
            recipientUserId: null,
            includeCounters: true,
            cursor,
            skip,
            take,
            ct);
    }

    public Task<HeadAlertSummaryPage> ListSummariesByDelegationAsync(
        int delegationId,
        int userId,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        ValidatePaging(skip, take);

        // شريط النشاط: نشط فقط، وحالة القراءة بحسب المستخدم، والعدّادات غير معروضة
        // (قائمة محامٍ لا تكشف أعداد المستلمين — §9.2).
        var query = Db.HeadAlerts
            .AsNoTracking()
            .Where(a => a.DelegationId == delegationId && !a.IsArchived);

        return ProjectSummariesAsync(
            query, userId, includeCounters: false, cursor: null, skip, take, ct);
    }

    private static void ValidatePaging(int skip, int take)
    {
        if (skip < 0)
            throw new ArgumentOutOfRangeException(nameof(skip), "الإزاحة لا تكون سالبة");
        if (take < 1 || take > AlertPageRequest.MaxPageSize)
            throw new ArgumentOutOfRangeException(
                nameof(take), $"حجم الصفحة يجب أن يكون بين 1 و{AlertPageRequest.MaxPageSize}");
    }

    private static IQueryable<HeadAlert> ApplyArchiveFilter(
        IQueryable<HeadAlert> query, AlertArchiveFilter archive) => archive switch
        {
            AlertArchiveFilter.Active => query.Where(a => !a.IsArchived),
            AlertArchiveFilter.All => query,
            AlertArchiveFilter.OnlyArchived => query.Where(a => a.IsArchived),
            _ => throw new ArgumentOutOfRangeException(
                nameof(archive), archive, "فلتر أرشفة غير معروف"),
        };

    /// <summary>
    /// الإسقاط الواحد لكل القوائم (الخطة §8.2/§13): يجلب بادئة 561 نقطة Unicode
    /// و<paramref name="messageLength"/> من <c>length()</c> في القاعدة — لا
    /// <c>Include(Recipients)</c> ولا <c>Message</c> كاملًا. المعاينة تُبنى في الذاكرة
    /// عبر <see cref="HeadAlertTextLimits.BuildPreview"/> على البادئة وحدها.
    ///
    /// <para><paramref name="cursor"/> غير الفارغ يستبدل <c>Skip</c> بشرط keyset على
    /// الترتيب نفسه (<c>LastContentChangedAt DESC, Id DESC</c>): يُقبل الصف الأقدم
    /// زمنيًا، أو الأقدم معرّفًا عند تساوي الزمن. و<paramref name="totalCount"/> يبقى
    /// عدد القائمة كلها بنفس الفلاتر (لا المتبقي بعد المؤشر) — العقد لا يتغير.</para>
    /// </summary>
    private async Task<HeadAlertSummaryPage> ProjectSummariesAsync(
        IQueryable<HeadAlert> query,
        int? recipientUserId,
        bool includeCounters,
        AlertCursor? cursor,
        int skip,
        int take,
        CancellationToken ct)
    {
        // COUNT منفصل بنفس الفلاتر قبل نافذة الصفحة — لا Count() بعد Skip/Take (§13).
        var totalCount = await query.CountAsync(ct);

        var window = cursor is null
            ? query
            : query.Where(a =>
                a.LastContentChangedAt < cursor.LastContentChangedAt
                || (a.LastContentChangedAt == cursor.LastContentChangedAt && a.Id < cursor.Id));

        var rows = await SummaryRows(
                window
                    .OrderByDescending(a => a.LastContentChangedAt)
                    .ThenByDescending(a => a.Id)
                    .Skip(cursor is null ? skip : 0)
                    .Take(take),
                recipientUserId,
                includeCounters)
            .ToListAsync(ct);

        return new HeadAlertSummaryPage(rows.Select(ToSummaryDto).ToList(), totalCount);
    }

    /// <summary>
    /// مصدر الإسقاط الواحد لكل الملخصات (القوائم وقراءة صفٍّ منفرد): Select على
    /// SQL ببادئة النص و<code>length()</code> والعدّادات — لا كيانات ولا مستلمين محمَّلين.
    /// </summary>
    private static IQueryable<SummaryRow> SummaryRows(
        IQueryable<HeadAlert> query,
        int? recipientUserId,
        bool includeCounters) =>
        query.Select(a => new SummaryRow(
            a.Id,
            a.Kind,
            a.Message.Substring(0, HeadAlertTextLimits.SqlPrefixLength),
            a.Message.Length,
            a.TargetType,
            a.DocumentId,
            a.Document != null,
            a.Document != null ? a.Document.BorrowerName : null,
            a.Document != null ? a.Document.BorrowerFather : null,
            a.Document != null ? a.Document.BorrowerFamily : null,
            a.Document != null ? a.Document.DocumentType : null,
            a.TargetLawyerId,
            a.TargetLawyer != null ? a.TargetLawyer.FullName : null,
            a.Recipients
                .Where(r => r.UserId == recipientUserId)
                .Select(r => (bool?)r.IsRead)
                .FirstOrDefault(),
            includeCounters ? a.Recipients.Count() : (int?)null,
            includeCounters ? a.Recipients.Count(r => !r.IsRead) : (int?)null,
            a.CreatedAt,
            a.LastContentChangedAt,
            a.CreatedBy != null ? a.CreatedBy.FullName : null,
            a.AppealId,
            a.ReviewLetterId,
            a.DelegationId,
            a.ExecutionCircuitId,
            a.IsArchived,
            a.ArchivedAt));

    /// <summary>صف وسيط مسقط في SQL — التحويلات غير القابلة للترجمة تتم في الذاكرة.</summary>
    private sealed record SummaryRow(
        int Id,
        string Kind,
        string MessagePrefix,
        int MessageLength,
        HeadAlertTargetType TargetType,
        int? DocumentId,
        bool DocumentExists,
        string? BorrowerName,
        string? BorrowerFather,
        string? BorrowerFamily,
        string? DocumentType,
        int? TargetLawyerId,
        string? TargetLawyerName,
        bool? IsRead,
        int? RecipientCount,
        int? UnreadCount,
        DateTime CreatedAt,
        DateTime LastContentChangedAt,
        string? CreatedByName,
        int? AppealId,
        int? ReviewLetterId,
        int? DelegationId,
        int? ExecutionCircuitId,
        bool IsArchived,
        DateTime? ArchivedAt);

    private static HeadAlertSummaryDto ToSummaryDto(SummaryRow row)
    {
        var preview = HeadAlertTextLimits.BuildPreview(row.MessagePrefix).Preview;
        var previewLength = HeadAlertTextLimits.CountCodePoints(preview);

        return new HeadAlertSummaryDto(
            row.Id,
            row.Kind,
            preview,
            row.MessageLength,
            row.MessageLength > previewLength,
            row.TargetType.ToString().ToLowerInvariant(),
            row.DocumentId,
            HeadAlertTitle.Build(
                row.DocumentId,
                row.DocumentExists,
                row.BorrowerName,
                row.BorrowerFather,
                row.BorrowerFamily,
                row.DocumentType),
            row.TargetLawyerId,
            row.TargetLawyerName,
            row.IsRead,
            row.RecipientCount,
            row.UnreadCount,
            row.CreatedAt,
            row.LastContentChangedAt,
            row.CreatedByName,
            row.AppealId,
            row.ReviewLetterId,
            row.DelegationId,
            row.ExecutionCircuitId,
            row.IsArchived,
            row.ArchivedAt);
    }

}
