using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>دمج فرعين نشطين في نفس الهوية الأم والمحافظة — ضمن نطاق رئيس القسم.</summary>
public sealed record MergePublicEntityBranchesCommand(
    int GroupId,
    MergeBranchesRequest Request,
    EntityRegistryActor Actor) : ICommand<MergeBranchesResponse>;

internal sealed class MergePublicEntityBranchesCommandHandler
    : CommandHandlerBase<MergePublicEntityBranchesCommand, MergeBranchesResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public MergePublicEntityBranchesCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<MergePublicEntityBranchesCommand, MergeBranchesResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<MergeBranchesResponse> HandleCoreAsync(
        MergePublicEntityBranchesCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (request.SourceEntryId == request.TargetEntryId)
            throw new ArgumentException("لا يمكن دمج الفرع مع نفسه");
        var source = await _rules.GetActiveGroupEntryAsync(command.GroupId, request.SourceEntryId, actor, ct);
        var target = await _rules.GetActiveGroupEntryAsync(command.GroupId, request.TargetEntryId, actor, ct);
        if (!string.Equals(source.Governorate, target.Governorate, StringComparison.Ordinal))
            throw new ArgumentException("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة");
        if (source.NeedsReview || target.NeedsReview)
            throw new ArgumentException("لا يمكن الدمج لوجود قيد بانتظار المراجعة");

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(source.Id, token);
            foreach (var doc in linkedDocs)
            {
                _rules.RepointEntryLinks(doc, source.Id, target.Id);
                // بعد إعادة التوجيه تصبح صفوف المصدر صفوفًا للهدف: لقطة الفرع عليها تتبدل إلى فرع الهدف (S7).
                foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                    a.Branch = target.BranchName;
                foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                    e.EntityBranch = target.BranchName;
            }
            affected = linkedDocs.Count;

            var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(source.Id, token);
            foreach (var del in entryDelegates)
            {
                del.PortalGroupId = target.GroupId;
                del.PortalEntryId = target.Id;
            }

            var aliasesAdded = 0;
            _rules.AddFoldAliases(target, source.Group.CanonicalName, source, ref aliasesAdded);
            source.IsActive = false;
            await _uow.SaveChangesAsync(token);

            if (linkedDocs.Count > 0)
                await _rules.SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Merge,
                groupId = command.GroupId,
                sourceEntryId = source.Id,
                targetEntryId = target.Id,
                sourceBranchName = source.BranchName,
                targetBranchName = target.BranchName,
                governorate = target.Governorate,
                aliasesAdded,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = target.Id,
                GroupId = command.GroupId,
                ActionKind = ActionKindCatalog.Merge,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _rules.TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await _rules.InsertBranchOccurrencesAsync(linkedDocs,
                $"تم دمج فرع «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName})",
                actor, token);
            await _rules.InsertBranchChangeAlertAsync(target,
                HeadAlertMessages.EntityBranchMerged(new(source.Group.CanonicalName, target.Governorate, source.BranchName, target.BranchName)),
                ActionKindCatalog.Merge, actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_branches",
                details: $"دمج فرع: «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new MergeBranchesResponse(source.Id, target.Id, affected, changeEventId);
    }
}
