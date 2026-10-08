using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── سجل تغييرات الجهات (د5 §7) ──

    private static (DateTime? From, DateTime? To) ParseChangeEventPeriod(string? fromRaw, string? toRaw)
    {
        // رفض صريح بدل التوسيع الصامت: تاريخ فلتر غير فارغ وغير صالح كان يُفسَّر «بلا فلتر»
        // فيُرى الكل ظنًّا أنه المدة المطلوبة (قرار إداري وتصدير على مجموعة موسعة خطأ).
        DateTime? from = null, to = null;
        if (!string.IsNullOrWhiteSpace(fromRaw))
        {
            var f = ActionDateParser.TryParse(fromRaw);
            if (!f.HasValue)
                throw new ArgumentException("تاريخ البداية (من) غير صالح — استخدم مثال: 1/8/2026");
            from = f.Value.Date;
        }
        if (!string.IsNullOrWhiteSpace(toRaw))
        {
            var t = ActionDateParser.TryParse(toRaw);
            if (!t.HasValue)
                throw new ArgumentException("تاريخ النهاية (إلى) غير صالح — استخدم مثال: 1/8/2026");
            to = t.Value.Date.AddDays(1).AddTicks(-1);
        }
        return (from, to);
    }
    private static bool MatchesGovernorate(PublicEntityChangeEvent e, string? governorate)
    {
        if (governorate is null) return true;
        // حدث مستوى قيد: نطاقه محافظة القيد نفسه — لا تتسرّب أحداث محافظة أخرى لرؤساء
        // محافظات أعضاء المجموعة (بعد ThenInclude(g => g.Entries) في جلب الأحداث).
        if (e.Entry != null) return e.Entry.Governorate == governorate;
        // حدث مستوى مجموعة (بلا EntryId — عمليات مركزية بمرسوم): يظهر لرؤساء محافظات أعضائها.
        if (e.Group != null) return e.Group.Entries.Any(en => en.Governorate == governorate);
        return false;
    }
    private async Task<List<PublicEntityChangeEvent>> GetFilteredChangeEventsAsync(
        EntityChangeEventQuery query,
        EntityRegistryActor actor,
        CancellationToken ct)
    {
        var all = await _entities.ListChangeEventsAsync(ct);
        // نطاق الرئيس: محافظته فقط (جبر خادمي يتجاهل پارامتر العميل تمامًا).
        string? governorate = NormalizeOptional(query.Governorate);
        if (actor.Role is UserRole.Head or UserRole.SubHead && actor.BranchId.HasValue)
        {
            var headBranch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
            governorate = NormalizeOptional(headBranch?.Governorate);
        }
        var actionKind = NormalizeOptional(query.ActionKind);
        var (actorUserId, actorName) = ResolveActorFilter(query);
        var (from, to) = ParseChangeEventPeriod(query.From, query.To);
        return all
            .Where(e => MatchesGovernorate(e, governorate))
            .Where(e => actionKind is null || e.ActionKind == actionKind)
            .Where(e => query.ActorUserId is null || e.ActorUserId == query.ActorUserId)
            .Where(e => actorUserId is null || e.ActorUserId == actorUserId)
            .Where(e => actorName is null || MatchesActorName(e, actorName))
            .Where(e => from is null || e.CreatedAtUtc >= from)
            .Where(e => to is null || e.CreatedAtUtc <= to)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToList();
    }
    /// <summary>
    /// يفسّر نص الفلتر الخام للفاعل: فارغ بلا فلترة، ورقم (بأرقام عربية أيضًا)
    /// يُفسَّر معرِّفًا، ونصٌّ يُطابَق على الاسم الكامل أو اسم الدخول بعد التطبيع العربي.
    /// </summary>
    private static (int? ActorUserId, string? ActorName) ResolveActorFilter(EntityChangeEventQuery query)
    {
        if (query.ActorUserId.HasValue)
            return (query.ActorUserId, null);
        var raw = NormalizeOptional(query.Actor);
        if (raw is null)
            return (null, null);
        var digits = ArabicDigitNormalizer.Normalize(raw);
        if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
            return (id, null);
        return (null, raw);
    }
    /// <summary>
    /// مطابقة اسم الفاعل بعد التطبيع العربي على الاسم الكامل أو اسم الدخول.
    /// فلتر يُطبَّع إلى فراغ (محارف تُحذف كلها) يطابق لا شيء — لا الكل: إرجاع الكل هنا كان
    /// يحوّل فلترًا بلا معنى إلى إلغاء صامت للفلترة.
    /// </summary>
    private static bool MatchesActorName(PublicEntityChangeEvent e, string actorName)
    {
        var normalized = ArabicNameNormalizer.Normalize(actorName);
        if (normalized.Length == 0)
            return false;
        return ArabicNameNormalizer.Normalize(e.ActorUser?.FullName).Contains(normalized, StringComparison.Ordinal)
            || ArabicNameNormalizer.Normalize(e.ActorUser?.Username).Contains(normalized, StringComparison.Ordinal);
    }
    public async Task<PagedResult<EntityChangeEventDto>> ListChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, actor, ct);
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);
        var total = filtered.Count;
        var items = filtered.Skip((page - 1) * perPage).Take(perPage).Select(ToChangeEventDto).ToList();
        return new PagedResult<EntityChangeEventDto> { Items = items, Page = page, PerPage = perPage, TotalCount = total };
    }
    public async Task<byte[]> ExportChangeEventsAsync(EntityChangeEventQuery query, EntityRegistryActor actor, CancellationToken ct = default)
    {
        var filtered = await GetFilteredChangeEventsAsync(query, actor, ct);
        var items = filtered.Take(5000).Select(ToChangeEventDto).ToList();
        var degraded = items.Count(i => i.SummaryDegraded);
        await _audit.LogAsync(actor.Name, "export_change_events",
            details: $"تصدير سجل تغييرات الجهات: {items.Count} سطرًا"
                + (degraded > 0 ? $" (منها {degraded} بملخص منقوص — يلزم مراجعة بياناتها في قاعدة البيانات)" : "")
                + (query.Governorate != null ? $" محافظة={query.Governorate}" : ""), ct: ct);
        var exporter = new ExcelExportService();
        return exporter.BuildChangeEventsWorkbook(items);
    }
    private static EntityChangeEventDto ToChangeEventDto(PublicEntityChangeEvent e)
    {
        var (summary, degraded) = EntityChangeLogSummary.Build(e.ActionKind, e.PayloadJson, e.DecreeKind, e.DecreeNumber, e.DecreeDate);
        return new(
            e.Id,
            e.EntryId,
            e.GroupId,
            e.ActionKind,
            e.DecreeKind,
            e.DecreeNumber,
            e.DecreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            e.ActorUserId,
            e.ActorUser?.FullName ?? e.ActorUser?.Username,
            e.CreatedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            e.Entry?.Governorate ?? e.Group?.Entries.FirstOrDefault()?.Governorate,
            e.Group?.CanonicalName ?? e.Entry?.Group?.CanonicalName,
            ActionKindCatalog.ToLabel(e.ActionKind),
            summary,
            degraded);
    }
}
