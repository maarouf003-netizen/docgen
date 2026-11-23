using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;

namespace DocGenerator.Application.UseCases.Interventions;

/// <summary>
/// حذف متدخل وورثته ووقعته من ملف — يملك معاملته وتدقيقه (`intervention`)،
/// ويعيد بناء `SearchText` كما الأصل. غياب الملف يرفع `KeyNotFoundException`،
/// وغياب المتدخل يعيد `false` بلا أثر جانبي.
/// </summary>
public sealed record DeleteDocumentInterventionCommand(
    int DocumentId,
    int InterventionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteDocumentInterventionCommandHandler
    : CommandHandlerBase<DeleteDocumentInterventionCommand, bool>
{
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public DeleteDocumentInterventionCommandHandler(
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteDocumentInterventionCommand, bool>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteDocumentInterventionCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct)
            ?? throw new KeyNotFoundException();
        DocumentInterventionService.EnsureSupportedSide(doc);

        var intervention = doc.Interventions.FirstOrDefault(i => i.Id == command.InterventionId);
        if (intervention is null)
            return false;

        // حذف صريح للوقعة (لا الاعتماد على حذف القاعدة) لتوحيد السلوك بين المزوّدين.
        var occurrence = doc.Occurrences.FirstOrDefault(
            o => o.DocumentInterventionId == intervention.Id);
        if (occurrence is not null)
            doc.Occurrences.Remove(occurrence);
        doc.Interventions.Remove(intervention);
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        doc.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "intervention", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor(
                $"حذف متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), ct);
        return true;
    }
}
