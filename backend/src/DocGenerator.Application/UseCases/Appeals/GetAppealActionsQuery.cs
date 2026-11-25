using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Appeals;

/// <summary>إجراءات وملاحظات الاستئناف (قائمة مستقلة عن الملف الأساس).</summary>
public sealed record GetAppealActionsQuery(int AppealId) : IQuery<List<AppealActionDto>>;

internal sealed class GetAppealActionsQueryHandler
    : QueryHandlerBase<GetAppealActionsQuery, List<AppealActionDto>>
{
    private readonly IAppealRepository _appeals;

    public GetAppealActionsQueryHandler(
        IAppealRepository appeals,
        IEnumerable<IPipelineBehavior<GetAppealActionsQuery, List<AppealActionDto>>> behaviors)
        : base(behaviors)
        => _appeals = appeals;

    protected override async Task<List<AppealActionDto>> HandleCoreAsync(
        GetAppealActionsQuery query, CancellationToken ct)
    {
        var appeal = await _appeals.GetByIdWithDetailsAsync(query.AppealId, ct)
            ?? throw new ArgumentException("الاستئناف غير موجود");
        return appeal.Actions
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => DocumentAppealService.ToActionDto(a))
            .ToList();
    }
}
