using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة النصوص التاريخية المتمايزة بعد التطبيع مع عدّادات ملفاتها — قراءة صرفة.</summary>
public sealed record PreviewPublicEntityImportQuery : IQuery<ImportPreviewResponse>;

internal sealed class PreviewPublicEntityImportQueryHandler
    : QueryHandlerBase<PreviewPublicEntityImportQuery, ImportPreviewResponse>
{
    private readonly IPublicEntityWriteRules _rules;

    public PreviewPublicEntityImportQueryHandler(
        IPublicEntityWriteRules rules,
        IEnumerable<IPipelineBehavior<PreviewPublicEntityImportQuery, ImportPreviewResponse>> behaviors)
        : base(behaviors)
        => _rules = rules;

    protected override async Task<ImportPreviewResponse> HandleCoreAsync(
        PreviewPublicEntityImportQuery query, CancellationToken ct)
        => new(DateTime.UtcNow, await _rules.BuildImportCandidatesAsync(ct));
}
