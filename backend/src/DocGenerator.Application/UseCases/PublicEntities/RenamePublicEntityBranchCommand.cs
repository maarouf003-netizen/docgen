using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>تعديل تسمية فرع ضمن محافظة رئيس القسم — يزامن لقطات الفروع (S7).</summary>
public sealed record RenamePublicEntityBranchCommand(
    int GroupId,
    int EntryId,
    RenameBranchRequest Request,
    EntityRegistryActor Actor) : ICommand<RenameBranchResponse>;

internal sealed class RenamePublicEntityBranchCommandHandler
    : CommandHandlerBase<RenamePublicEntityBranchCommand, RenameBranchResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public RenamePublicEntityBranchCommandHandler(
        IPublicEntityWriteRules rules,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<RenamePublicEntityBranchCommand, RenameBranchResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<RenameBranchResponse> HandleCoreAsync(
        RenamePublicEntityBranchCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        var entry = await _rules.GetActiveGroupEntryAsync(command.GroupId, command.EntryId, actor, ct);
        var oldBranch = entry.BranchName;
        var newBranch = _rules.Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
        if (string.Equals(oldBranch, newBranch, StringComparison.Ordinal))
            throw new ArgumentException("الاسم الجديد مطابق للاسم الحالي");
        await _rules.EnsureNoDuplicateEntryAsync(
            entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);

        var oldCanonical = entry.Group.CanonicalName;
        var wasNeedsReview = entry.NeedsReview;
        var createdByLawyer = entry.CreatedBy?.Role == UserRole.Lawyer;

        entry.BranchName = newBranch;
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = _rules.ValidateCoverageLabel(request.CoverageLabel);
        // إعادة التسمية تُقفل أي مراجعة معلّقة (نفس سلوك Update:636).
        if (entry.NeedsReview)
        {
            entry.NeedsReview = false;
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedById = actor.UserId;
        }

        int affected = 0;
        int changeEventId = 0;
        await _tx.RunAsync(async token =>
        {
            var affectedDocs = await _rules.SyncBranchLabelsAsync(entry, newBranch, token);
            if (affectedDocs.Count > 0)
                await _rules.SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);
            affected = affectedDocs.Count;

            var aliasesAdded = 0;
            _rules.AddEachExtraAlias(
                entry, _rules.FullEntryName(oldCanonical, entry.Governorate, oldBranch), ref aliasesAdded);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Rename,
                groupId = command.GroupId,
                entryId = command.EntryId,
                oldBranchName = oldBranch,
                newBranchName = newBranch,
                governorate = entry.Governorate,
                coverageLabel = entry.CoverageLabel,
                oldCanonical,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = command.GroupId,
                ActionKind = ActionKindCatalog.Rename,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _rules.TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await _rules.InsertBranchOccurrencesAsync(affectedDocs,
                $"تم تغيير اسم فرع «{oldCanonical}» من «{oldBranch}» إلى «{newBranch}»", actor, token);
            await _rules.InsertBranchChangeAlertAsync(entry,
                HeadAlertMessages.EntityBranchRenamed(new(oldCanonical, entry.Governorate, oldBranch, newBranch)),
                ActionKindCatalog.Rename, actor, token);
            if (wasNeedsReview && createdByLawyer)
                await _rules.InsertRenameNoticeToCreatorAsync(
                    entry, oldCanonical, entry.Group.CanonicalName, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_branch",
                details: $"أعاد تسمية فرع جهة: «{oldCanonical}» ({entry.Governorate}/{oldBranch}) ← «{newBranch}» — تغطية {affected} ملفًا", ct: token);
        }, ct);

        return new RenameBranchResponse(entry.Id, oldBranch, newBranch, affected, changeEventId);
    }
}
