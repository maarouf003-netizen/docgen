using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Interventions;

/// <summary>
/// تعديل متدخل قائم ومزامنة وقعته — يملك معاملته وتدقيقه (`intervention`).
/// غياب المتدخل/عدم انتمائه للملف يعيد `null` بلا أثر جانبي.
/// </summary>
public sealed record UpdateDocumentInterventionCommand(
    int DocumentId,
    int InterventionId,
    DocumentInterventionUpsertRequest Request,
    int UserId,
    string? ActorName) : ICommand<DocumentInterventionDto?>, ITransactionalRequest;

public sealed class UpdateDocumentInterventionCommandHandler
    : CommandHandlerBase<UpdateDocumentInterventionCommand, DocumentInterventionDto?>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<PublicEntity> _publicEntities;
    private readonly IRepository<PublicEntityGroup> _publicEntityGroups;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public UpdateDocumentInterventionCommandHandler(
        IDocumentRepository documents,
        IRepository<PublicEntity> publicEntities,
        IRepository<PublicEntityGroup> publicEntityGroups,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UpdateDocumentInterventionCommand, DocumentInterventionDto?>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _publicEntities = publicEntities;
        _publicEntityGroups = publicEntityGroups;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<DocumentInterventionDto?> HandleCoreAsync(
        UpdateDocumentInterventionCommand command, CancellationToken ct)
    {
        var doc = await _documents.GetByIdAsync(command.DocumentId, ct)
            ?? throw new KeyNotFoundException();
        DocumentInterventionRules.EnsureSupportedSide(doc);

        var intervention = doc.Interventions.FirstOrDefault(i => i.Id == command.InterventionId);
        if (intervention is null)
            return null;

        OptimisticConcurrencyGuard.EnsureCurrent(intervention, command.Request.Version, "المتدخل");

        var normalized = DocumentInterventionText.Normalize(command.Request);
        await DocumentInterventionRules.ResolveRegistryAsync(
            normalized, intervention.RegistryId, _publicEntities, _publicEntityGroups, ct);

        DocumentInterventionText.Apply(intervention, normalized);

        var occurrence = doc.Occurrences.FirstOrDefault(o => o.DocumentInterventionId == intervention.Id);
        if (occurrence is null)
            doc.Occurrences.Add(
                DocumentInterventionText.BuildOccurrence(intervention, command.DocumentId, command.UserId));
        else
            DocumentInterventionText.ApplyOccurrence(occurrence, intervention);

        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        doc.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "intervention", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor(
                $"عدّل متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), ct);
        return DocumentInterventionText.ToDto(intervention);
    }
}
