using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// مساعدات البحث/القوائم المشتركة بين استعلامات القراءة (`P4-01cr`) — نُقلت حرفيًا من
/// خدمة البحث القديمة بعد إفراغها: بناء صفحة عناصر القائمة، ومؤشر الصفحة التالية،
/// وإثراء شارة الاستئناف. مصدر واحد لكل قوائم الملفات فلا يتباعد شكل استجابة قائمة عن أخرى.
/// </summary>
internal static class DocumentSearchHelpers
{
    /// <summary>
    /// بناء صفحة عناصر القائمة من صفوف مُسقَطة — مصدر واحد لكل قوائم الملفات
    /// (P3-04b والقوائم الخمس في P3-04c) فلا يتباعد شكل استجابة قائمة عن أخرى.
    /// </summary>
    internal static PagedResult<DocumentListItemDto> BuildListResult(
        List<DocumentListRow> rows, int total, int page, int perPage, string? nextCursor,
        TimeProvider clock, TimeZoneInfo timeZone)
    {
        var currentYear = ServerClock.CurrentYear(clock, timeZone);
        return new PagedResult<DocumentListItemDto>
        {
            Items = rows.Select(row => DocumentListItemDto.FromRow(row, currentYear)).ToList(),
            Page = page,
            PerPage = perPage,
            TotalCount = total,
            NextCursor = nextCursor,
        };
    }

    /// <summary>
    /// يبني مؤشر الصفحة التالية من آخر صف معروض وقيمة مفتاح ترتيب القائمة — بلا
    /// قراءة إضافية؛ غياب صف تالٍ (`hasMore = false`) يعني آخر صفحة.
    /// </summary>
    internal static string? NextCursor(
        string kind, List<DocumentListRow> rows, bool hasMore, IKeysetCursorCodec cursorCodec)
    {
        if (!hasMore || rows.Count == 0)
            return null;

        var last = rows[^1];
        DateTime? sortValue = kind switch
        {
            DocumentListCursorKinds.CreatedAt => last.CreatedAt,
            DocumentListCursorKinds.DeletedAt => last.DeletedAt,
            DocumentListCursorKinds.StruckOffDate => last.StruckOffDate,
            DocumentListCursorKinds.UpdatedAt => last.UpdatedAt,
            DocumentListCursorKinds.StartReferralDate => last.StartReferralDate,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "نوع مؤشر غير معروف"),
        };
        return cursorCodec.Encode(kind, sortValue, last.Id);
    }

    /// <summary>
    /// إثراء نتائج القائمة بإسقاط الصف بعلم وجود استئناف منظور على الملف ومعرف أول
    /// استئناف منظور — استعلام دفعي واحد لمعرفات الصفحة الحالية فقط.
    /// </summary>
    internal static async Task EnrichAppealFlagsAsync(
        List<DocumentListItemDto> items, IAppealRepository appeals, CancellationToken ct)
    {
        if (items.Count == 0)
            return;
        var ids = items.Select(i => i.Id).ToList();
        var map = await appeals.MapFirstAppealIdByDocumentIdsAsync(ids, ct);
        if (map.Count == 0)
            return;
        foreach (var item in items)
        {
            if (map.TryGetValue(item.Id, out var appealId))
            {
                item.HasAppeals = true;
                item.MatchedAppealId = appealId;
            }
        }
    }

    /// <summary>
    /// إثراء نتائج القائمة بعلم وجود استئناف منظور على الملف ومعرف أول استئناف منظور —
    /// لشارة «استئناف» بجانب النتائج والانتقال إلى تفاصيل الاستئناف. المحسوم والمشطوب
    /// لا يُظهران الشارة (تختفي بعد حسم/شطب آخر منظور). استعلام دفعي واحد لمعرفات الصفحة الحالية فقط.
    /// </summary>
    internal static async Task EnrichAppealFlagsAsync(
        List<DocumentResponse> items, IAppealRepository appeals, CancellationToken ct)
    {
        if (items.Count == 0)
            return;
        var ids = items.Select(i => i.Id).ToList();
        var map = await appeals.MapFirstAppealIdByDocumentIdsAsync(ids, ct);
        if (map.Count == 0)
            return;
        foreach (var item in items)
        {
            if (map.TryGetValue(item.Id, out var appealId))
            {
                item.HasAppeals = true;
                item.MatchedAppealId = appealId;
            }
        }
    }
}
