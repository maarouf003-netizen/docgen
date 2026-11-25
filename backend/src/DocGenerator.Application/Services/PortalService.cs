using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IPortalService
{
    /// <summary>نطاق المندوب (هوية/فرع) — null إن لم يُربط بنطاق بعد.</summary>
    Task<PortalScopeDto?> GetMyScopeAsync(int userId, CancellationToken ct = default);

    Task<PagedResult<PortalFileListItemDto>> ListFilesAsync(
        int userId, string? query, string? status, int page, int perPage, CancellationToken ct = default, int? entryId = null);

    /// <summary>
    /// تفاصيل ملف قراءةً — null إذا خرج عن نطاق المندوب (يُترجم 404). داخليّات المحامي
    /// (الملاحظات `Notes`/`ImmediateActions` وإجراءات نوع `note`) مُسقطة سلكيًا (ق7).
    /// </summary>
    Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default);

    /// <summary>بطاقة الاستئنافات القرائية لملف داخل النطاق — null إذا خرج عن النطاق.</summary>
    Task<IReadOnlyList<PortalAppealDto>?> ListAppealsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>الإجراءات التنفيذية القرائية (نوع action فقط، الأحدث أولًا) — null خارج النطاق.</summary>
    Task<IReadOnlyList<PortalExecutionActionDto>?> ListExecutionActionsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تشعبات الملف القرائية (إنابة) — null خارج النطاق.</summary>
    Task<IReadOnlyList<DelegationDto>?> ListDelegationsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تفاصيل استئنافات الملف (رأي المحامي وملاحظاته مخفيّان عنه في البوابة) — null خارج النطاق.</summary>
    Task<IReadOnlyList<AppealDto>?> ListAppealDetailsAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>تاريخ أرقام الأساس للملف — null خارج النطاق.</summary>
    Task<IReadOnlyList<BaseNumberHistoryDto>?> ListBaseNumbersAsync(int userId, int documentId, CancellationToken ct = default);

    /// <summary>
    /// مصنّف Excel لملفات النطاق وفق فلاتر القائمة نفسها، مع سقف
    /// ExportOptions.MaxRows وتدقيق export_entity_portal_excel.
    /// </summary>
    Task<Stream> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default, int? entryId = null);

    /// <summary>إحصاءات قرائية لنطاق المندوب (المرحلة 4) — مع فلتر فرع اختياري ضمن النطاق.</summary>
    Task<PortalStatsDto> GetStatsAsync(int userId, CancellationToken ct = default, int? entryId = null);
}

/// <summary>
/// خدمة بوابة مندوب الجهة العامة (المرحلة 3): رؤية قرائية بحسب الربط — هوية أم
/// تشمل كل فروعها النهائية، أو فرع بعينه (د1/د4)؛ فروع الانتظار لا تظهر إطلاقًا.
/// التصدير يمرّ بسقف الصفوف ويُدوَّن، وأعمدة المحامين الداخلية مخفية دائمًا عن البوابة.
/// </summary>
public sealed partial class PortalService : IPortalService
{
    private readonly IPortalRepository _portal;
    private readonly IRepository<Document> _documents;
    private readonly IAppealRepository _appeals;
    private readonly IDocumentAppealService _appealService;
    private readonly IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> _delegationReads;
    private readonly IExcelExportService _excel;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public PortalService(
        IPortalRepository portal,
        IRepository<Document> documents,
        IAppealRepository appeals,
        IDocumentAppealService appealService,
        IQueryHandler<GetDocumentDelegationsQuery, List<DelegationDto>> delegationReads,
        IExcelExportService excel,
        IAuditLogger audit,
        IOptions<ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _portal = portal;
        _documents = documents;
        _appeals = appeals;
        _appealService = appealService;
        _delegationReads = delegationReads;
        _excel = excel;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
    }
}
