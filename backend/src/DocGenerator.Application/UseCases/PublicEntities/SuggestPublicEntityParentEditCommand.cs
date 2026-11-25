using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>اقتراح تعديل قيد «الجهة الأم» من رئيس القسم (يُخزَّن معلّقًا لمراجعة الإدارة).</summary>
public sealed record SuggestPublicEntityParentEditCommand(
    int EntryId,
    SuggestParentEditRequest Request,
    EntityRegistryActor Actor) : ICommand<ParentEditSuggestionDto>;

internal sealed class SuggestPublicEntityParentEditCommandHandler
    : CommandHandlerBase<SuggestPublicEntityParentEditCommand, ParentEditSuggestionDto>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public SuggestPublicEntityParentEditCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SuggestPublicEntityParentEditCommand, ParentEditSuggestionDto>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _branches = branches;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<ParentEditSuggestionDto> HandleCoreAsync(
        SuggestPublicEntityParentEditCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (actor.Role is not (UserRole.Head or UserRole.SubHead))
            throw new UnauthorizedAccessException("اقتراح تعديل الجهة الأم متاح لرئيس القسم أو الشعبة فقط");
        if (!actor.BranchId.HasValue)
            throw new UnauthorizedAccessException("حسابك غير مرتبط بفرع لتقديم اقتراح");

        var entry = await _entities.GetEntryWithDetailsAsync(command.EntryId, ct)
            ?? throw new ArgumentException("القيد غير موجود");
        if (!entry.IsParentEntity)
            throw new ArgumentException("الاقتراح يخص قيد «الجهة الأم» فقط");
        var group = entry.Group;
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        // شرط النطاق: للمجموعة فرع نشط في محافظة فرع الرئيس.
        var headBranch = await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var headGov = _rules.NormalizeOptional(headBranch?.Governorate);
        if (headGov is null || !group.Entries.Any(e => e.IsActive && e.Governorate == headGov))
            throw new UnauthorizedAccessException("لا توجد فروع نشطة لهذه الجهة في محافظة فرعك");

        var proposedCanonical = _rules.NormalizeOptional(request.ProposedCanonicalName);
        if (proposedCanonical is not null)
        {
            if (ArabicNameNormalizer.Normalize(proposedCanonical) == ArabicNameNormalizer.Normalize(group.CanonicalName))
                throw new ArgumentException("الاسم المقترح مطابق للاسم الحالي");
            await _rules.EnsureCanonicalAvailableAsync(proposedCanonical, group.Id, ct);
        }
        var proposedType = _rules.NormalizeOptional(request.ProposedEntityType) is { } pt ? _rules.ValidEntityType(pt) : null;
        var proposedCitation = _rules.NormalizeOptional(request.ProposedCitationFormula) is { } pc ? _rules.ValidCitationFormula(pc, CitationFormulaCatalog.AddToJob) : null;
        var reason = _rules.Required(request.Reason, "سبب الاقتراح مطلوب", 500);

        // منع المكرر المعلّق (GroupId × فرع الرئيس) — الانعكاس خادمي وفهرس فريد جزئي يعزّزه.
        var all = await _suggestions.ListAsync(ct);
        if (all.Any(s => s.Status == ParentEditSuggestionStatusCatalog.Pending
            && s.GroupId == group.Id
            && s.CreatedBranchId == actor.BranchId.Value))
            throw new ArgumentException("يوجد اقتراح معلّق بالفعل لهذه الجهة من فرعك");

        var suggestion = new ParentEditSuggestion
        {
            GroupId = group.Id,
            EntryId = entry.Id,
            ProposedCanonicalName = proposedCanonical,
            ProposedEntityType = proposedType,
            ProposedCitationFormula = proposedCitation,
            Reason = reason,
            Status = ParentEditSuggestionStatusCatalog.Pending,
            CreatedById = actor.UserId,
            CreatedBranchId = actor.BranchId.Value,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _tx.RunAsync(async token =>
        {
            await _suggestions.AddAsync(suggestion, token);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Propose,
                groupId = group.Id,
                entryId = entry.Id,
                canonicalName = group.CanonicalName,
                proposedCanonicalName = proposedCanonical,
                proposedEntityType = proposedType,
                proposedCitationFormula = proposedCitation,
                reason,
                createdBranchId = actor.BranchId,
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

            await _audit.LogAsync(actor.Name, "suggest_parent_edit",
                details: $"اقترح تعديل الجهة الأم «{group.CanonicalName}»{($" — {proposedCanonical}")} {reason}", ct: token);
        }, ct);

        return _rules.ToParentEditSuggestionDto(suggestion);
    }
}
