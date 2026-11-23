using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Interventions;

/// <summary>
/// إضافة متدخل — يملك معاملته وتدقيقه (`intervention`)، ويزامن وقعة «طلب تدخل»
/// ويعيد بناء `SearchText` كالأصل. القفل داخل المعاملة يمنع سباق العدّ/الإضافة.
/// </summary>
public sealed record AddDocumentInterventionCommand(
    int DocumentId,
    DocumentInterventionUpsertRequest Request,
    int UserId,
    string? ActorName) : ICommand<DocumentInterventionDto>, ITransactionalRequest;

public sealed class AddDocumentInterventionCommandHandler
    : CommandHandlerBase<AddDocumentInterventionCommand, DocumentInterventionDto>
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<PublicEntity> _publicEntities;
    private readonly IRepository<PublicEntityGroup> _publicEntityGroups;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;

    public AddDocumentInterventionCommandHandler(
        IDocumentRepository documents,
        IRepository<PublicEntity> publicEntities,
        IRepository<PublicEntityGroup> publicEntityGroups,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AddDocumentInterventionCommand, DocumentInterventionDto>> behaviors)
        : base(behaviors)
    {
        _documents = documents;
        _publicEntities = publicEntities;
        _publicEntityGroups = publicEntityGroups;
        _uow = uow;
        _audit = audit;
    }

    protected override async Task<DocumentInterventionDto> HandleCoreAsync(
        AddDocumentInterventionCommand command, CancellationToken ct)
    {
        // تفريغ المتتبَّع قبل القراءة الطازجة: قد يكون المستند محمّلًا في الطلب نفسه
        // (فحص الوصول في المتحكم) أو في محاولة سابقة أعادت ExecutionStrategy تنفيذها.
        // إعادة استخدام كيان متتبَّع تُبقي المجموعة/العدّ على حالة قديمة رغم قفل الصف.
        _uow.ClearChanges();

        // القفل داخل المعاملة يمنع سباق العدّ/الإضافة (طلبَان يقرآن 49 ثم يضيفان معًا).
        var doc = await _documents.GetByIdForUpdateAsync(command.DocumentId, ct)
            ?? throw new KeyNotFoundException();
        DocumentInterventionRules.EnsureSupportedSide(doc);
        if (doc.Interventions.Count >= InterventionLimitsCatalog.MaxPerDocument)
            throw new ArgumentException(
                $"عدد المتدخلين في الملف يتجاوز الحد الأقصى ({InterventionLimitsCatalog.MaxPerDocument})");

        var intervention = DocumentInterventionText.Normalize(command.Request);
        await DocumentInterventionRules.ResolveRegistryAsync(
            intervention, existingRegistryId: null, _publicEntities, _publicEntityGroups, ct);
        intervention.DocumentId = command.DocumentId;
        intervention.CreatedById = command.UserId;
        intervention.CreatedAt = DateTime.UtcNow;
        intervention.UpdatedAt = DateTime.UtcNow;

        doc.Interventions.Add(intervention);
        doc.Occurrences.Add(
            DocumentInterventionText.BuildOccurrence(intervention, command.DocumentId, command.UserId));
        doc.SearchText = DocumentSearchTextBuilder.Build(doc);
        doc.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(command.ActorName, "intervention", command.DocumentId, doc.DocumentType,
            DocumentText.AuditWithActor(
                $"أضاف متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), ct);
        return DocumentInterventionText.ToDto(intervention);
    }
}
