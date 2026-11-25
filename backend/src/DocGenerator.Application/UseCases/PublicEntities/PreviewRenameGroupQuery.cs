using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة إعادة تسمية هوية أم على مستوى المجموعة — قراءة صرفة.</summary>
public sealed record PreviewRenameGroupQuery(RenameGroupPreviewRequest Request) : IQuery<RenameGroupPreviewResponse>;

internal sealed class PreviewRenameGroupQueryHandler : QueryHandlerBase<PreviewRenameGroupQuery, RenameGroupPreviewResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;

    public PreviewRenameGroupQueryHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<PreviewRenameGroupQuery, RenameGroupPreviewResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
    }

    protected override async Task<RenameGroupPreviewResponse> HandleCoreAsync(
        PreviewRenameGroupQuery query, CancellationToken ct)
    {
        var request = query.Request;
        var group = await _entities.GetGroupAsync(request.GroupId, ct)
            ?? throw new ArgumentException("الهوية الأم غير موجودة");
        var newName = _rules.Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        if (group.Entries.Any(e => e.NeedsReview))
            throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

        var affected = await PublicEntityService.CountDocumentsForGroupAsync(_entities, request.GroupId, ct);
        var branches = await PublicEntityService.BranchNamesForGroupAsync(_entities, request.GroupId, ct);
        return new RenameGroupPreviewResponse(group.CanonicalName, newName, affected, branches);
    }
}
