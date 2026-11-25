using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>
/// تصدير المستندات بصفوف مسقطة (`DocumentExportRow`) بدل الكيانات الكاملة —
/// تغيير عقد داخلي مبرر (منادٍ واحد) لخفض ذاكرة التصدير الكبير جذريًا.
/// </summary>
public sealed record ExportDocumentsQuery(
    string? Query, string? Status, string? Applicant, string? Court, string? Lawyer,
    string? Branch, string? AdministrativeBranch, string? ExecutedEntity, string? PublicEntityBranch,
    int? VisibleBranchId = null, int? VisibleUserId = null, string? ActorName = null,
    int? OwnerSectionId = null) : IQuery<List<DocumentExportRow>>;

/// <summary>
/// استعلام التصدير — **الاستثناء الوحيد** بين استعلامات البحث: يكتب سجل تدقيق
/// `export_documents` بحفظة واحدة بلا معاملة (RF-017/SEC-002) كما في الخدمة الأصلية حرفيًا.
/// </summary>
internal sealed class ExportDocumentsQueryHandler
    : QueryHandlerBase<ExportDocumentsQuery, List<DocumentExportRow>>
{
    private readonly IDocumentRepository _documents;
    private readonly IAuditLogger _audit;
    private readonly int _maxExportRows;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public ExportDocumentsQueryHandler(
        IDocumentRepository documents,
        IAuditLogger audit,
        IOptions<ExportOptions> exportOptions,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<ExportDocumentsQuery, List<DocumentExportRow>>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _audit = audit;
        _maxExportRows = Math.Max(1, exportOptions.Value.MaxRows);
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<List<DocumentExportRow>> HandleCoreAsync(
        ExportDocumentsQuery query, CancellationToken ct)
    {
        // سقف التصدير: يُعدَّل عدد النتائج المطابقة أولًا قبل جلب أي صف إلى الذاكرة،
        // فيُرفض التصدير الواسع برسالة واضحة بدل ذروة ذاكرة غير محصورة على الخادم.
        var total = await _documents.CountExportAsync(
            query.Query, query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch, query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch, query.VisibleBranchId, query.VisibleUserId, ct, query.OwnerSectionId);
        if (total > _maxExportRows)
            throw new ArgumentException($"عدد النتائج يتجاوز الحد الأقصى للتصدير ({_maxExportRows:N0}) — طبّق فلترًا أضيق");

        var currentYear = ServerClock.CurrentYear(_clock, _timeZone);
        var rows = await _documents.ExportRowsAsync(
            query.Query, query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch, query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch, query.VisibleBranchId, query.VisibleUserId, ct, query.OwnerSectionId);
        // الرقم الفعّال (LatestFrom) على البذور المسقطة — نفس منطق `FromEntity` حرفيًا،
        // في الذاكرة (رخيص: بضعة أرقام أساس لكل صف).
        foreach (var row in rows)
        {
            var latest = EffectiveFileIdentity.LatestFrom(row.BaseNumbers, currentYear, row.ExecutionCircuitId);
            row.DisplayFileNumber = latest?.BaseNumber ?? row.FileNumber;
            row.DisplayFileYear = latest?.Year.ToString(CultureInfo.InvariantCulture) ?? row.FileYear;
        }
        // RF-017 (SEC-002): التصدير قراءة بلا كتابة عمل — تدقيقه حفظة واحدة بلا معاملة
        // (بالفلاتر والعدد الفعلي؛ الفلاتر الفارغة تُحذَف من النص).
        var filters = string.Join("،", new[] { query.Query, query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch, query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch }
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()));
        await _audit.LogAsync(query.ActorName, "export_documents",
            details: $"صدّر {rows.Count} صفًا" + (filters.Length == 0 ? " (بلا فلاتر)" : $" بفلاتر: {filters}"), ct: ct);
        return rows;
    }
}
