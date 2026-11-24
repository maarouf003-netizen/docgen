using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>إلغاء فرع: تعطيل مباشر لصفر روابط أو دمج ضمني مع هدف (S4).</summary>
public sealed record AbolishPublicEntityBranchCommand(
    int GroupId,
    int EntryId,
    AbolishBranchRequest Request,
    EntityRegistryActor Actor) : ICommand<AbolishBranchResponse>;

internal sealed class AbolishPublicEntityBranchCommandHandler
    : CommandHandlerBase<AbolishPublicEntityBranchCommand, AbolishBranchResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AbolishPublicEntityBranchCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IUserRepository users,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AbolishPublicEntityBranchCommand, AbolishBranchResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _users = users;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AbolishBranchResponse> HandleCoreAsync(
        AbolishPublicEntityBranchCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var entry = await _rules.GetActiveGroupEntryAsync(command.GroupId, command.EntryId, actor, ct);
        var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct);

        // «آخر نشط» ممنوع: لا يبقى للمجموعة فرع نشط بعد الإلغاء.
        var remainingActive = entry.Group.Entries.Any(e => e.Id != entry.Id && e.IsActive);
        if (!remainingActive)
            throw new ArgumentException("لا يمكن إلغاء آخر فرع نشط في المجموعة");

        if (linkedDocs.Count > 0 && !request.TargetEntryId.HasValue)
            throw new ArgumentException("الفرع مرتبط بملفات ولا يمكن إلغاؤه دون فرع هدف بديل (اختر قيدًا آخر للدمج)");

        // فرع هدف: دمج ضمني (سلوك دمج كامل — S4).
        PublicEntity? target = null;
        if (request.TargetEntryId.HasValue)
        {
            if (request.TargetEntryId.Value == entry.Id)
                throw new ArgumentException("لا يمكن دمج الفرع مع نفسه");
            target = await _rules.GetActiveGroupEntryAsync(
                command.GroupId, request.TargetEntryId.Value, actor, ct);
            if (!string.Equals(entry.Governorate, target.Governorate, StringComparison.Ordinal))
                throw new ArgumentException("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة");
            if (entry.NeedsReview || target.NeedsReview)
                throw new ArgumentException("لا يمكن الإلغاء لوجود قيد بانتظار المراجعة");
        }

        int affected = linkedDocs.Count;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            if (target is not null)
            {
                // دمج ضمني (نفس دورة الدمج الكاملة).
                foreach (var doc in linkedDocs)
                {
                    _rules.RepointEntryLinks(doc, entry.Id, target.Id);
                    foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == target.Id))
                        a.Branch = target.BranchName;
                    foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == target.Id))
                        e.EntityBranch = target.BranchName;
                }

                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(entry.Id, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = target.GroupId;
                    del.PortalEntryId = target.Id;
                }

                var aliasesAdded = 0;
                _rules.AddFoldAliases(target, entry.Group.CanonicalName, entry, ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                if (linkedDocs.Count > 0)
                    await _rules.SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Merge,
                    groupId = command.GroupId,
                    sourceEntryId = entry.Id,
                    targetEntryId = target.Id,
                    sourceBranchName = entry.BranchName,
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
                    $"تم دمج فرع «{entry.Group.CanonicalName}» ({target.Governorate}/{entry.BranchName}) في ({target.Governorate}/{target.BranchName})",
                    actor, token);
                await _rules.InsertBranchChangeAlertAsync(target,
                    HeadAlertMessages.EntityBranchMerged(new(entry.Group.CanonicalName, target.Governorate, entry.BranchName, target.BranchName)),
                    ActionKindCatalog.Merge, actor, token);
            }
            else
            {
                // تعطيل مباشر (صفر ملفات مرتبطة).
                var aliasesAdded = 0;
                _rules.AddEachExtraAlias(
                    entry, _rules.FullEntryName(entry.Group.CanonicalName, entry.Governorate, entry.BranchName),
                    ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Abolish,
                    groupId = command.GroupId,
                    entryId = command.EntryId,
                    branchName = entry.BranchName,
                    governorate = entry.Governorate,
                    aliasesAdded,
                });
                var changeEvent = new PublicEntityChangeEvent
                {
                    EntryId = entry.Id,
                    GroupId = command.GroupId,
                    ActionKind = ActionKindCatalog.Abolish,
                    PayloadJson = payload,
                    ActorUserId = actor.UserId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await _rules.TrackChangeEventAsync(changeEvent, token);
                await _uow.SaveChangesAsync(token);
                changeEventId = changeEvent.Id;

                await _rules.InsertBranchChangeAlertAsync(entry,
                    HeadAlertMessages.EntityBranchAbolished(new(entry.Group.CanonicalName, entry.Governorate, entry.BranchName)),
                    ActionKindCatalog.Abolish, actor, token);
            }

            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "abolish_branch",
                details: $"ألغى فرع: «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})"
                    + (target is not null ? $" بدمج ضمني في «{target.BranchName}»" : " (تعطيل مباشر)") + $" — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new AbolishBranchResponse(entry.Id, target?.Id, affected, changeEventId);
    }
}
