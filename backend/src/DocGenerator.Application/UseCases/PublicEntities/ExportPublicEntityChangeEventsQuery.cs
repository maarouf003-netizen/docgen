using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>
/// تصدير سجل التغييرات إلى Excel (نفس فلاتر القائمة ونطاق رئيس القسم) — قراءة صرفة
/// مع تدقيق التصدير القائم كما هو في الأصل (لا كتابة كيانات ولا معاملة).
/// </summary>
public sealed record ExportPublicEntityChangeEventsQuery(
    EntityChangeEventQuery Query,
    EntityRegistryActor Actor) : IQuery<Stream>;

internal sealed class ExportPublicEntityChangeEventsQueryHandler
    : QueryHandlerBase<ExportPublicEntityChangeEventsQuery, Stream>
{
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IAuditLogger _audit;

    public ExportPublicEntityChangeEventsQueryHandler(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ExportPublicEntityChangeEventsQuery, Stream>> behaviors)
        : base(behaviors)
    {
        _entities = entities;
        _branches = branches;
        _audit = audit;
    }

    protected override async Task<Stream> HandleCoreAsync(
        ExportPublicEntityChangeEventsQuery query, CancellationToken ct)
    {
        var request = query.Query;
        var actor = query.Actor;
        var filtered = await PublicEntityService.GetFilteredChangeEventsAsync(_entities, _branches, request, actor, ct);
        var items = filtered.Take(5000).Select(PublicEntityService.ToChangeEventDto).ToList();
        var degraded = items.Count(i => i.SummaryDegraded);
        await _audit.LogAsync(actor.Name, "export_change_events",
            details: $"تصدير سجل تغييرات الجهات: {items.Count} سطرًا"
                + (degraded > 0 ? $" (منها {degraded} بملخص منقوص — يلزم مراجعة بياناتها في قاعدة البيانات)" : "")
                + (request.Governorate != null ? $" محافظة={request.Governorate}" : ""), ct: ct);
        var exporter = new ExcelExportService();
        return exporter.BuildChangeEventsWorkbook(items);
    }
}
