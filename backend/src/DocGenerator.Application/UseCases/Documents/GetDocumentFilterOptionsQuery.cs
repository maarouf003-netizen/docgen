using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;

namespace DocGenerator.Application.UseCases.Documents;

/// <summary>خيارات فلترة صفحة الملفات ضمن نطاق الرؤية الممرَّر — قراءة صرفة بلا كتابة.</summary>
public sealed record GetDocumentFilterOptionsQuery(
    string? Status, string? Applicant, string? Court, string? Lawyer, string? Branch,
    string? AdministrativeBranch, string? ExecutedEntity, string? PublicEntityBranch,
    int? VisibleBranchId = null, int? VisibleUserId = null, int? OwnerSectionId = null)
    : IQuery<DocumentFilterOptions>;

internal sealed class GetDocumentFilterOptionsQueryHandler
    : QueryHandlerBase<GetDocumentFilterOptionsQuery, DocumentFilterOptions>
{
    private readonly IDocumentRepository _documents;

    public GetDocumentFilterOptionsQueryHandler(
        IDocumentRepository documents,
        IEnumerable<IPipelineBehavior<GetDocumentFilterOptionsQuery, DocumentFilterOptions>> behaviors)
        : base(behaviors)
        => _documents = documents;

    protected override Task<DocumentFilterOptions> HandleCoreAsync(
        GetDocumentFilterOptionsQuery query, CancellationToken ct)
        => _documents.GetFilterOptionsAsync(
            query.Status, query.Applicant, query.Court, query.Lawyer, query.Branch,
            query.AdministrativeBranch, query.ExecutedEntity, query.PublicEntityBranch,
            query.VisibleBranchId, query.VisibleUserId, ct, query.OwnerSectionId);
}
