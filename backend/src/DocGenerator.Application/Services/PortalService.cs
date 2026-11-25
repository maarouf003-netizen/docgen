using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Services;

public interface IPortalService
{
    /// <summary>
    /// تفاصيل ملف قراءةً — null إذا خرج عن نطاق المندوب (يُترجم 404). داخليّات المحامي
    /// (الملاحظات `Notes`/`ImmediateActions` وإجراءات نوع `note`) مُسقطة سلكيًا (ق7).
    /// </summary>
    Task<DocumentResponse?> GetFileAsync(int userId, int documentId, string? viewerName, CancellationToken ct = default);

    /// <summary>
    /// مصنّف Excel لملفات النطاق وفق فلاتر القائمة نفسها، مع سقف
    /// ExportOptions.MaxRows وتدقيق export_entity_portal_excel.
    /// </summary>
    Task<Stream> ExportWorkbookAsync(int userId, string? query, string? status, string? viewerName, CancellationToken ct = default, int? entryId = null);
}

/// <summary>
/// خدمة بوابة مندوب الجهة العامة (المرحلة 3): رؤية قرائية بحسب الربط — هوية أم
/// تشمل كل فروعها النهائية، أو فرع بعينه (د1/د4)؛ فروع الانتظار لا تظهر إطلاقًا.
/// التصدير يمرّ بسقف الصفوف ويُدوَّن، وأعمدة المحامين الداخلية مخفية دائمًا عن البوابة.
/// <para>
/// `P4-01co`: بقيت هنا القراءتان اللتان تكتبان تدقيقًا (`view_entity_portal_files`
/// و`export_entity_portal_excel`) — ليستا قراءة صرفة — ونُقلت بقية القراءات إلى
/// معالجات استعلام تحت <c>UseCases/Portal</c>.
/// </para>
/// </summary>
public sealed partial class PortalService : IPortalService
{
    private readonly IPortalRepository _portal;
    private readonly IRepository<Document> _documents;
    private readonly IExcelExportService _excel;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public PortalService(
        IPortalRepository portal,
        IRepository<Document> documents,
        IExcelExportService excel,
        IAuditLogger audit,
        IOptions<ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone)
    {
        _portal = portal;
        _documents = documents;
        _excel = excel;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
    }
}
