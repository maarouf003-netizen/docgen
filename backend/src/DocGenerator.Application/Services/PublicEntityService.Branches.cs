using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    // ── عمليات فروع رئيس القسم (ضمن محافظته — بلا مرسوم) ──

    /// <inheritdoc/>
    public async Task<BranchActionPreviewResponse> PreviewBranchActionAsync(
        int groupId,
        PreviewBranchActionRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");

        var errors = new List<string>();
        var warnings = new List<string>();
        var previewEntries = new List<BranchPreviewEntryDto>();
        int totalAffected = 0;
        var targetBranch = string.Empty;
        string? summary = null;

        switch (request.Action)
        {
            case ActionKindCatalog.Rename:
                {
                    var entry = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                    if (entry is null || entry.GroupId != groupId)
                    {
                        errors.Add("القيد غير موجود في المجموعة");
                        break;
                    }
                    if (!entry.IsActive) { errors.Add("القيد غير نشط"); break; }
                    try
                    {
                        GuardNotParentEntry(entry);
                        GuardHeadCannotEditParent(actor, entry);
                        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
                        var newBranch = Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
                        if (string.Equals(newBranch, entry.BranchName, StringComparison.Ordinal))
                            errors.Add("الاسم الجديد مطابق للاسم الحالي");
                        else
                            await EnsureNoDuplicateEntryAsync(entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);
                        var count = (await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct)).Count;
                        targetBranch = newBranch;
                        totalAffected = count;
                        previewEntries.Add(new BranchPreviewEntryDto(entry.Id, entry.BranchName, entry.Governorate, count));
                        summary = $"إعادة تسمية فرع «{entry.BranchName}» إلى «{newBranch}» — {count} ملفًا متأثرًا";
                    }
                    catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                    break;
                }
            case ActionKindCatalog.Merge:
                {
                    if (!request.TargetId.HasValue) { errors.Add("يجب تحديد الفرع الهدف"); break; }
                    var source = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                    var target = await _entities.GetEntryWithDetailsAsync(request.TargetId.Value, ct);
                    try
                    {
                        await ValidateMergeablePairAsync(groupId, source, target, request.TargetId.Value, actor, ct, errors, warnings);
                    }
                    catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                    if (errors.Count == 0)
                    {
                        var count = (await _entities.ListDocumentsLinkedToEntryAsync(request.EntryId, ct)).Count;
                        targetBranch = target!.BranchName;
                        totalAffected = count;
                        previewEntries.Add(new BranchPreviewEntryDto(source!.Id, source.BranchName, source.Governorate,
                            (await _entities.ListDocumentsLinkedToEntryAsync(source.Id, ct)).Count));
                        previewEntries.Add(new BranchPreviewEntryDto(target.Id, target.BranchName, target.Governorate,
                            (await _entities.ListDocumentsLinkedToEntryAsync(target.Id, ct)).Count));
                        summary = $"دمج فرع «{source.BranchName}» في «{target.BranchName}» — {count} ملفًا متأثرًا";
                    }
                    break;
                }
            case ActionKindCatalog.Abolish:
                {
                    var entry = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                    if (entry is null || entry.GroupId != groupId) { errors.Add("القيد غير موجود في المجموعة"); break; }
                    if (!entry.IsActive) { errors.Add("القيد غير نشط"); break; }
                    try
                    {
                        GuardNotParentEntry(entry);
                        GuardHeadCannotEditParent(actor, entry);
                        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
                        var count = (await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, ct)).Count;
                        PublicEntity? target = request.TargetId.HasValue
                            ? await _entities.GetEntryWithDetailsAsync(request.TargetId.Value, ct)
                            : null;
                        if (count > 0 && target is null)
                        {
                            errors.Add("الفرع مرتبط بملفات ولا يمكن إلغاؤه دون فرع هدف بديل (S4)");
                        }
                        else if (target is not null)
                        {
                            await ValidateMergeablePairAsync(groupId, entry, target, request.TargetId!.Value, actor, ct, errors, warnings);
                        }
                        targetBranch = target?.BranchName ?? entry.BranchName;
                        totalAffected = count;
                        previewEntries.Add(new BranchPreviewEntryDto(entry.Id, entry.BranchName, entry.Governorate, count));
                        summary = count == 0
                            ? $"إلغاء فرع «{entry.BranchName}» بلا ملفات مرتبطة (تعطيل مباشر)"
                            : $"إلغاء فرع «{entry.BranchName}» بدمجه في «{targetBranch}» — {count} ملفًا متأثرًا";
                    }
                    catch (ArgumentException ex) { errors.Add(ex.Message); break; }
                    break;
                }
            case ActionKindCatalog.Unify:
                {
                    var target = await _entities.GetEntryWithDetailsAsync(request.EntryId, ct);
                    if (target is null || target.GroupId != groupId) { errors.Add("الفرع الهدف غير موجود في المجموعة"); break; }
                    var absorbed = request.AbsorbedIds?.Where(x => x != target.Id).Distinct().ToList() ?? new List<int>();
                    if (absorbed.Count == 0) { errors.Add("لا توجد فروع محددة للتوحيد (فرع واحد على الأقل غير الهدف)"); break; }
                    try
                    {
                        GuardNotParentEntry(target);
                        GuardHeadCannotEditParent(actor, target);
                        await EnsureHeadScopeAsync(actor, target, target.Governorate, ct);
                        var absorbedDocIds = new Dictionary<int, Document>();
                        foreach (var absorbedId in absorbed)
                        {
                            var ae = await _entities.GetEntryWithDetailsAsync(absorbedId, ct);
                            await ValidateMergeablePairAsync(groupId, ae, target, target.Id, actor, ct, errors, warnings);
                            if (errors.Count > 0) break;
                            foreach (var doc in await _entities.ListDocumentsLinkedToEntryAsync(ae!.Id, ct))
                                absorbedDocIds[doc.Id] = doc;
                            previewEntries.Add(new BranchPreviewEntryDto(ae.Id, ae.BranchName, ae.Governorate,
                                (await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, ct)).Count));
                        }
                        if (errors.Count == 0)
                        {
                            string? finalBranch = null;
                            if (!string.IsNullOrWhiteSpace(request.CorrectedName)
                                && !string.Equals(request.CorrectedName.Trim(), target.BranchName, StringComparison.Ordinal))
                            {
                                var corrected = Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
                                try
                                {
                                    await EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, corrected, ct);
                                }
                                catch (ArgumentException ex) { errors.Add(ex.Message); }
                                finalBranch = corrected;
                                foreach (var doc in await _entities.ListDocumentsLinkedToEntryAsync(target.Id, ct))
                                    absorbedDocIds[doc.Id] = doc;
                            }
                            targetBranch = finalBranch ?? target.BranchName;
                            totalAffected = absorbedDocIds.Count;
                            summary = $"توحيد {absorbed.Count} فرعًا في «{targetBranch}» — {totalAffected} ملفًا متأثرًا";
                        }
                    }
                    catch (ArgumentException ex) { errors.Add(ex.Message); }
                    break;
                }
            default:
                throw new ArgumentException("إجراء غير صالح: rename/merge/abolish/unify");
        }

        return new BranchActionPreviewResponse(
            request.Action,
            summary ?? string.Empty,
            targetBranch,
            previewEntries,
            totalAffected,
            warnings,
            errors);
    }
    /// <summary>
    /// تحقق شروط الدمج/الطيّ القاسية بين قيدين (المجموعة + المحافظة + النشاط + نطاق
    /// رئيس القسم + بلا NeedsReview). يملأ الأخطاء/التحذيرات بلا رمي (للشروط) أو يرمي
    /// UnauthorizedAccessException (للنطاق/حارس الأم).
    /// </summary>
    private async Task ValidateMergeablePairAsync(
        int groupId,
        PublicEntity? source,
        PublicEntity? target,
        int targetId,
        EntityRegistryActor actor,
        CancellationToken ct,
        List<string> errors,
        List<string> warnings)
    {
        if (source is null || target is null) { errors.Add("الفرع غير موجود"); return; }
        if (source.GroupId != groupId || target.GroupId != groupId) { errors.Add("الفروع المحددة ليست ضمن نفس المجموعة"); return; }
        if (source.Id == targetId) { errors.Add("لا يمكن دمج الفرع مع نفسه"); return; }
        if (!source.IsActive || !target.IsActive) { errors.Add("أحد الفروع غير نشط"); return; }
        if (source.NeedsReview || target.NeedsReview) { errors.Add("لا يمكن الدمج لوجود قيد بانتظار المراجعة"); return; }
        if (!string.Equals(source.Governorate, target.Governorate, StringComparison.Ordinal))
        { errors.Add("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة"); return; }
        GuardNotParentEntry(source);
        GuardNotParentEntry(target);
        GuardHeadCannotEditParent(actor, source);
        GuardHeadCannotEditParent(actor, target);
        // النطاق يُرمى كـ Unauthorized — لا يُعرض كخطأ قابل للتجاوز في المعاينة.
        await EnsureHeadScopeAsync(actor, source, source.Governorate, ct);
        await EnsureHeadScopeAsync(actor, target, target.Governorate, ct);
    }
    /// <inheritdoc/>
    public async Task<RenameBranchResponse> RenameBranchAsync(
        int groupId,
        int entryId,
        RenameBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var entry = await GetActiveGroupEntryAsync(groupId, entryId, actor, ct);
        var oldBranch = entry.BranchName;
        var newBranch = Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
        if (string.Equals(oldBranch, newBranch, StringComparison.Ordinal))
            throw new ArgumentException("الاسم الجديد مطابق للاسم الحالي");
        await EnsureNoDuplicateEntryAsync(entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);

        var oldCanonical = entry.Group.CanonicalName;
        var wasNeedsReview = entry.NeedsReview;
        var createdByLawyer = entry.CreatedBy?.Role == UserRole.Lawyer;

        entry.BranchName = newBranch;
        if (request.CoverageLabel is not null)
            entry.CoverageLabel = ValidateCoverageLabel(request.CoverageLabel);
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
            var affectedDocs = await SyncBranchLabelsAsync(entry, newBranch, token);
            if (affectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);
            affected = affectedDocs.Count;

            var aliasesAdded = 0;
            AddEachExtraAlias(entry, FullEntryName(oldCanonical, entry.Governorate, oldBranch), ref aliasesAdded);
            await _uow.SaveChangesAsync(token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Rename,
                groupId,
                entryId,
                oldBranchName = oldBranch,
                newBranchName = newBranch,
                governorate = entry.Governorate,
                coverageLabel = entry.CoverageLabel,
                oldCanonical,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entry.Id,
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Rename,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(affectedDocs,
                $"تم تغيير اسم فرع «{oldCanonical}» من «{oldBranch}» إلى «{newBranch}»", actor, token);
            await InsertBranchChangeAlertAsync(entry,
                HeadAlertMessages.EntityBranchRenamed(new(oldCanonical, entry.Governorate, oldBranch, newBranch)),
                ActionKindCatalog.Rename, actor, token);
            if (wasNeedsReview && createdByLawyer)
                await InsertRenameNoticeToCreatorAsync(entry, oldCanonical, entry.Group.CanonicalName, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_branch",
                details: $"أعاد تسمية فرع جهة: «{oldCanonical}» ({entry.Governorate}/{oldBranch}) ← «{newBranch}» — تغطية {affected} ملفًا", ct: token);
        }, ct);

        return new RenameBranchResponse(entry.Id, oldBranch, newBranch, affected, changeEventId);
    }
    /// <inheritdoc/>
    public async Task<MergeBranchesResponse> MergeBranchesAsync(
        int groupId,
        MergeBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        if (request.SourceEntryId == request.TargetEntryId)
            throw new ArgumentException("لا يمكن دمج الفرع مع نفسه");
        var source = await GetActiveGroupEntryAsync(groupId, request.SourceEntryId, actor, ct);
        var target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId, actor, ct);
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
                RepointEntryLinks(doc, source.Id, target.Id);
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
            AddFoldAliases(target, source.Group.CanonicalName, source, ref aliasesAdded);
            source.IsActive = false;
            await _uow.SaveChangesAsync(token);

            if (linkedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Merge,
                groupId,
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
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Merge,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(linkedDocs,
                $"تم دمج فرع «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName})",
                actor, token);
            await InsertBranchChangeAlertAsync(target,
                HeadAlertMessages.EntityBranchMerged(new(source.Group.CanonicalName, target.Governorate, source.BranchName, target.BranchName)),
                ActionKindCatalog.Merge, actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_branches",
                details: $"دمج فرع: «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new MergeBranchesResponse(source.Id, target.Id, affected, changeEventId);
    }
}
