using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Portal;

/// <summary>قائمة ملفات نطاق المندوب بنفس فلاتر القائمة الأساسية + فلتر فرع ضمن النطاق.</summary>
public sealed record ListPortalFilesQuery(
    int UserId,
    string? Query,
    string? Status,
    int Page,
    int PerPage,
    int? EntryId = null) : IQuery<PagedResult<PortalFileListItemDto>>;

internal sealed class ListPortalFilesQueryHandler
    : QueryHandlerBase<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>>
{
    private readonly IPortalRepository _portal;

    public ListPortalFilesQueryHandler(
        IPortalRepository portal,
        IEnumerable<IPipelineBehavior<ListPortalFilesQuery, PagedResult<PortalFileListItemDto>>> behaviors)
        : base(behaviors)
        => _portal = portal;

    protected override async Task<PagedResult<PortalFileListItemDto>> HandleCoreAsync(
        ListPortalFilesQuery query, CancellationToken ct)
    {
        var scope = await _portal.ResolveForUserAsync(query.UserId, ct);
        var page = Math.Max(1, query.Page);
        var perPage = Math.Clamp(query.PerPage <= 0 ? 20 : query.PerPage, 1, 100);

        var effectiveIds = PortalService.ResolveEffectiveIds(scope, query.EntryId);
        var (total, items) = await _portal.SearchScopedAsync(effectiveIds, query.Query, query.Status, page, perPage, ct);
        var result = new PagedResult<PortalFileListItemDto> { Page = page, PerPage = perPage, TotalCount = total };
        result.Items = items.Select(d => ToListItem(d, scope)).ToList();
        return result;
    }

    private static PortalFileListItemDto ToListItem(PortalListRow d, PortalScopeResolution? scope = null)
    {
        // أحدث رقم أساس دائمًا (غير مقيّد بسنة الفحص) — وإلا رقم الملف/سنة قيده الأصلية.
        // انحراف مقصود وموثّق عن بقية النظام (as-of السنة الحالية): قرار صاحب المشروع
        // «دائمًا أحدث رقم أساس مع السنة» — فرقم بسنة مستقبلية (خطأ إدخال) يظهر هنا
        // دون قوائم المحامين؛ مثبّت باختبار سنة مستقبلية قصدًا لا سهوًا.
        var latestBase = EffectiveFileIdentity.LatestFrom(d.BaseNumbers, int.MaxValue, d.ExecutionCircuitId);
        var displayNumber = latestBase?.BaseNumber ?? d.FileNumber;
        var displayYear = latestBase is not null ? latestBase.Year.ToString(CultureInfo.InvariantCulture) : d.FileYear;
        return new(
            d.Id,
            d.DocumentType ?? string.Empty,
            d.IsDraft,
            d.BorrowerName,
            d.Applicant,
            ExecutedSummary(d),
            d.AmountNumeric,
            d.Currency,
            d.ExecStatus,
            d.CreatedAt,
            d.UpdatedAt,
            d.BorrowerFather,
            d.BorrowerFamily,
            d.FileType,
            d.Court,
            displayNumber,
            displayYear,
            PortalService.BuildMatchedEntries(d, scope),
            // شارة البطاقة من المصدر الوحيد (`DocumentStatusResolver`) — لا منطق
            // تصنيف في الواجهة: الخام (`ExecStatus`) للفلترة، والمعروض للشارة.
            DocumentStatusResolver.Resolve(d));
    }

    private static string ExecutedSummary(PortalListRow d) =>
        string.Join("؛ ", d.ExecutedEntities
            .Where(e => e.Nature == PartyNatureCatalog.PublicEntity)
            .Select(e => string.Join(' ', new[] { e.EntityName, e.EntityBranch }.Where(p => !string.IsNullOrWhiteSpace(p))))
            .Where(v => v.Length > 0));
}
