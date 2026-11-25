using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>اقتراح تعديل فردي من المحامي على قيد جهة (يبقى بانتظار المراجعة — لا يزامن النصوص).</summary>
public sealed record ProposePublicEntityEditCommand(
    int EntryId,
    ProposeEditRequest Request,
    EntityRegistryActor Actor) : ICommand<PublicEntityEntryDto?>;

internal sealed class ProposePublicEntityEditCommandHandler
    : CommandHandlerBase<ProposePublicEntityEditCommand, PublicEntityEntryDto?>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public ProposePublicEntityEditCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<ProposePublicEntityEditCommand, PublicEntityEntryDto?>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<PublicEntityEntryDto?> HandleCoreAsync(
        ProposePublicEntityEditCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (actor.Role != UserRole.Lawyer)
            throw new UnauthorizedAccessException("اقتراح التعديل متاح للمحامي فقط");

        var entry = await _entities.GetEntryWithDetailsAsync(command.EntryId, ct);
        if (entry is null) return null;
        var group = entry.Group;

        // حوكمة S1 — منع صريح للجميع: اقتراح المحامي لا يغيِّر وضع «الجهة الأم»
        // (لا ترقية ولا تخفيض) — يبقى العلم على حاله، والإدارة وحدها عبر التعديل المركزي.
        if (request.IsParentEntity == true)
            throw new UnauthorizedAccessException("لا يُغيَّر وضع «الجهة الأم» عبر الاقتراحات — للإدارة فقط");

        string? newCanonical = null;
        if (!string.IsNullOrWhiteSpace(request.CanonicalName)
            && !string.Equals(request.CanonicalName.Trim(), group.CanonicalName, StringComparison.Ordinal))
        {
            newCanonical = _rules.Required(request.CanonicalName, "اسم الجهة مطلوب", 200);
            await _rules.EnsureCanonicalAvailableAsync(newCanonical, group.Id, ct);
        }

        var newGovernorate = entry.Governorate;
        if (!string.IsNullOrWhiteSpace(request.Governorate))
            newGovernorate = _rules.Required(request.Governorate, "المحافظة مطلوبة", 100);

        var newBranchName = entry.BranchName;
        if (!string.IsNullOrWhiteSpace(request.BranchName))
            newBranchName = _rules.RequiredWithFallback(request.BranchName, PublicEntityBranchCatalog.ParentBranchName, 200);

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            group.EntityType = _rules.ValidEntityType(request.EntityType);
        if (!string.IsNullOrWhiteSpace(request.CitationFormula))
            entry.CitationFormula = _rules.ValidCitationFormula(request.CitationFormula, entry.CitationFormula);
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = _rules.ValidateCoverageLabel(request.CoverageLabel);

        await _rules.EnsureNoDuplicateEntryAsync(entry.Id, newCanonical ?? group.CanonicalName, newGovernorate, newBranchName, ct);

        var oldCanonical = group.CanonicalName;
        var oldGov = entry.Governorate;
        var oldBranch = entry.BranchName;

        if (newCanonical is not null) group.CanonicalName = newCanonical;
        _rules.SetGroupNorm(group);
        entry.Governorate = newGovernorate;
        entry.BranchName = newBranchName;

        entry.NeedsReview = true;
        entry.ReviewedAtUtc = null;
        entry.ReviewedById = null;

        return await _tx.RunAsync(async token =>
        {
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                oldCanonical,
                newCanonical = group.CanonicalName,
                oldGovernorate = oldGov,
                newGovernorate,
                oldBranch,
                newBranch = newBranchName,
                coverageLabel = entry.CoverageLabel,
            });

            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Propose,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _rules.TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "propose_public_entity_edit",
                details: $"اقترح تعديل جهة: «{oldCanonical}» → «{group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})", ct: token);

            return _rules.ToEntryDto(group, entry);
        }, ct);
    }
}
