using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>
/// توحيد تسميات عدة فروع في فرع ناجٍ واحد (اختياريًا مع تصحيح كتابة اسمه) —
/// ضمن نطاق رئيس القسم وبمعاملة واحدة.
/// </summary>
public sealed record UnifyPublicEntityBranchesCommand(
    int GroupId,
    UnifyBranchesRequest Request,
    EntityRegistryActor Actor) : ICommand<UnifyBranchesResponse>;

internal sealed class UnifyPublicEntityBranchesCommandHandler
    : CommandHandlerBase<UnifyPublicEntityBranchesCommand, UnifyBranchesResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public UnifyPublicEntityBranchesCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<UnifyPublicEntityBranchesCommand, UnifyBranchesResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<UnifyBranchesResponse> HandleCoreAsync(
        UnifyPublicEntityBranchesCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var target = await _rules.GetActiveGroupEntryAsync(command.GroupId, request.TargetEntryId, actor, ct);
        var absorbedIds = request.AbsorbedEntryIds?.Where(x => x != target.Id).Distinct().ToList()
            ?? new List<int>();
        if (absorbedIds.Count == 0)
            throw new ArgumentException("لا توجد فروع محددة للتوحيد (فرع واحد على الأقل غير الهدف)");

        var absorbed = new List<PublicEntity>();
        foreach (var absorbedId in absorbedIds)
        {
            var ae = await _rules.GetActiveGroupEntryAsync(command.GroupId, absorbedId, actor, ct);
            if (!string.Equals(ae.Governorate, target.Governorate, StringComparison.Ordinal))
                throw new ArgumentException($"فرع «{ae.BranchName}» في محافظة مختلفة — التوحيد يتطلب نفس المحافظة");
            if (ae.NeedsReview || target.NeedsReview)
                throw new ArgumentException("لا يمكن التوحيد لوجود قيد بانتظار المراجعة");
            absorbed.Add(ae);
        }

        // تصحيح كتابة اسم الناجي (اختياري): يُغيّر فرع الهدف ويزامن لقطاته (S7).
        string? correctedName = null;
        if (!string.IsNullOrWhiteSpace(request.CorrectedName)
            && !string.Equals(request.CorrectedName.Trim(), target.BranchName, StringComparison.Ordinal))
        {
            correctedName = _rules.Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
            await _rules.EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, correctedName, ct);
        }

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var affectedDocs = new Dictionary<int, Document>();
            var aliasesAdded = 0;

            foreach (var ae in absorbed)
            {
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                foreach (var doc in linkedDocs)
                {
                    _rules.RepointEntryLinks(doc, ae.Id, target.Id);
                    foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                        a.Branch = target.BranchName;
                    foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                        e.EntityBranch = target.BranchName;
                    affectedDocs[doc.Id] = doc;
                }

                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(ae.Id, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = target.GroupId;
                    del.PortalEntryId = target.Id;
                }

                _rules.AddFoldAliases(target, ae.Group.CanonicalName, ae, ref aliasesAdded);
                ae.IsActive = false;
            }
            await _uow.SaveChangesAsync(token);

            if (correctedName is not null)
            {
                target.BranchName = correctedName;
                var labelDocs = await _rules.SyncBranchLabelsAsync(target, correctedName, token);
                foreach (var doc in labelDocs)
                    affectedDocs[doc.Id] = doc;
            }
            await _uow.SaveChangesAsync(token);

            var docsList = affectedDocs.Values.ToList();
            affected = docsList.Count;
            if (docsList.Count > 0)
                await _rules.SyncAppealsAfterEntityChangeAsync(docsList, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Unify,
                groupId = command.GroupId,
                targetEntryId = target.Id,
                targetBranchName = target.BranchName,
                absorbedEntryIds = absorbed.Select(a => a.Id).ToList(),
                absorbedBranchNames = absorbed.Select(a => a.BranchName).ToList(),
                correctedName,
                governorate = target.Governorate,
                aliasesAdded,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = target.Id,
                GroupId = command.GroupId,
                ActionKind = ActionKindCatalog.Unify,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _rules.TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await _rules.InsertBranchOccurrencesAsync(docsList,
                $"تم توحيد تسميات فروع «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName})", actor, token);
            await _rules.InsertBranchChangeAlertAsync(target,
                HeadAlertMessages.EntityBranchUnified(new(target.Group.CanonicalName, target.Governorate, target.BranchName)),
                ActionKindCatalog.Unify, actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "unify_branches",
                details: $"وحّد {absorbed.Count} فروعًا في «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new UnifyBranchesResponse(target.Id, absorbed.Count, affected, changeEventId);
    }
}
