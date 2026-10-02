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
                    ValidateMergeablePair(groupId, source, target, request.TargetId.Value, actor, ct, errors, warnings);
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
                        ValidateMergeablePair(groupId, entry, target, request.TargetId!.Value, actor, ct, errors, warnings);
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
                        ValidateMergeablePair(groupId, ae, target, target.Id, actor, ct, errors, warnings);
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
    private void ValidateMergeablePair(
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
        EnsureHeadScopeAsync(actor, source, source.Governorate, ct).GetAwaiter().GetResult();
        EnsureHeadScopeAsync(actor, target, target.Governorate, ct).GetAwaiter().GetResult();
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
                $"تم تغيير اسم فرع جهة «{oldCanonical}» ({entry.Governorate}): من «{oldBranch}» إلى «{newBranch}»", actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_branch",
                details: $"أعاد تسمية فرع: «{oldCanonical}» ({entry.Governorate}/{oldBranch}) → «{newBranch}» — مزامنة {affected} ملفًا", ct: token);
            if (wasNeedsReview && createdByLawyer)
                await InsertRenameNoticeToCreatorAsync(entry, oldCanonical, entry.Group.CanonicalName, token);
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
                $"تم دمج فرع جهة «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName})",
                actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_branches",
                details: $"دمج فرع: «{source.Group.CanonicalName}» ({target.Governorate}/{source.BranchName}) في ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new MergeBranchesResponse(source.Id, target.Id, affected, changeEventId);
    }
    /// <inheritdoc/>
    public async Task<AbolishBranchResponse> AbolishBranchAsync(
        int groupId,
        int entryId,
        AbolishBranchRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var entry = await GetActiveGroupEntryAsync(groupId, entryId, actor, ct);
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
            target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId.Value, actor, ct);
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
                    RepointEntryLinks(doc, entry.Id, target.Id);
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
                AddFoldAliases(target, entry.Group.CanonicalName, entry, ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                if (linkedDocs.Count > 0)
                    await SyncAppealsAfterEntityChangeAsync(linkedDocs, actor, token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Merge,
                    groupId,
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
                    $"تم دمج فرع «{entry.Group.CanonicalName}» ({target.Governorate}/{entry.BranchName}) في ({target.Governorate}/{target.BranchName})",
                    actor, token);
                await InsertBranchChangeAlertAsync(target,
                    $"تم دمج فرع جهة «{entry.Group.CanonicalName}» ({target.Governorate}/{entry.BranchName}) في ({target.Governorate}/{target.BranchName})",
                    actor, token);
            }
            else
            {
                // تعطيل مباشر (صفر ملفات مرتبطة).
                var aliasesAdded = 0;
                AddEachExtraAlias(entry, FullEntryName(entry.Group.CanonicalName, entry.Governorate, entry.BranchName), ref aliasesAdded);
                entry.IsActive = false;
                await _uow.SaveChangesAsync(token);

                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    actionKind = ActionKindCatalog.Abolish,
                    groupId,
                    entryId,
                    branchName = entry.BranchName,
                    governorate = entry.Governorate,
                    aliasesAdded,
                });
                var changeEvent = new PublicEntityChangeEvent
                {
                    EntryId = entry.Id,
                    GroupId = groupId,
                    ActionKind = ActionKindCatalog.Abolish,
                    PayloadJson = payload,
                    ActorUserId = actor.UserId,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await TrackChangeEventAsync(changeEvent, token);
                await _uow.SaveChangesAsync(token);
                changeEventId = changeEvent.Id;

                await InsertBranchChangeAlertAsync(entry,
                    $"تم إلغاء فرع جهة «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName}) بلا ملفات مرتبطة",
                    actor, token);
            }

            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actor.Name, "abolish_branch",
                details: $"ألغى فرع: «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName})"
                    + (target is not null ? $" بدمج ضمني في «{target.BranchName}»" : " (تعطيل مباشر)") + $" — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new AbolishBranchResponse(entry.Id, target?.Id, affected, changeEventId);
    }
    /// <inheritdoc/>
    public async Task<UnifyBranchesResponse> UnifyBranchesAsync(
        int groupId,
        UnifyBranchesRequest request,
        EntityRegistryActor actor,
        CancellationToken ct = default)
    {
        var target = await GetActiveGroupEntryAsync(groupId, request.TargetEntryId, actor, ct);
        var absorbedIds = request.AbsorbedEntryIds?.Where(x => x != target.Id).Distinct().ToList()
            ?? new List<int>();
        if (absorbedIds.Count == 0)
            throw new ArgumentException("لا توجد فروع محددة للتوحيد (فرع واحد على الأقل غير الهدف)");

        var absorbed = new List<PublicEntity>();
        foreach (var absorbedId in absorbedIds)
        {
            var ae = await GetActiveGroupEntryAsync(groupId, absorbedId, actor, ct);
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
            correctedName = Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
            await EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, correctedName, ct);
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
                    RepointEntryLinks(doc, ae.Id, target.Id);
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

                AddFoldAliases(target, ae.Group.CanonicalName, ae, ref aliasesAdded);
                ae.IsActive = false;
            }
            await _uow.SaveChangesAsync(token);

            if (correctedName is not null)
            {
                target.BranchName = correctedName;
                var labelDocs = await SyncBranchLabelsAsync(target, correctedName, token);
                foreach (var doc in labelDocs)
                    affectedDocs[doc.Id] = doc;
            }
            await _uow.SaveChangesAsync(token);

            var docsList = affectedDocs.Values.ToList();
            affected = docsList.Count;
            if (docsList.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(docsList, actor, token);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                actionKind = ActionKindCatalog.Unify,
                groupId,
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
                GroupId = groupId,
                ActionKind = ActionKindCatalog.Unify,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);
            changeEventId = changeEvent.Id;

            await InsertBranchOccurrencesAsync(docsList,
                $"تم توحيد تسميات فروع «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName})", actor, token);
            await InsertBranchChangeAlertAsync(target,
                $"تم توحيد تسميات فروع جهة «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName})", actor, token);
            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "unify_branches",
                details: $"وحّد {absorbed.Count} فروعًا في «{target.Group.CanonicalName}» ({target.Governorate}/{target.BranchName}) — {affected} ملفًا متأثرًا", ct: token);
        }, ct);

        return new UnifyBranchesResponse(target.Id, absorbed.Count, affected, changeEventId);
    }
    // ── مساعدات عمليات الفروع المشتركة (رئيس القسم — ضمن محافظته) ──

    /// <summary>
    /// يجلب مجموعة نشطة وقيدًا نشطًا تابعًا لها (بها قابلية الحارس الأم والنطاق).
    /// يُستخدم لكل عمليات الفروع الأربع.
    /// </summary>
    private async Task<PublicEntity> GetActiveGroupEntryAsync(
        int groupId,
        int entryId,
        EntityRegistryActor actor,
        CancellationToken ct)
    {
        var group = await _entities.GetGroupAsync(groupId, ct)
            ?? throw new ArgumentException("المجموعة غير موجودة");
        if (!group.IsActive)
            throw new ArgumentException("المجموعة غير نشطة");
        var entry = await _entities.GetEntryWithDetailsAsync(entryId, ct)
            ?? throw new ArgumentException("القيد غير موجود");
        if (entry.GroupId != groupId)
            throw new ArgumentException("القيد لا ينتمي إلى المجموعة المحددة");
        if (!entry.IsActive)
            throw new ArgumentException("القيد غير نشط");
        GuardNotParentEntry(entry);
        GuardHeadCannotEditParent(actor, entry);
        await EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
        return entry;
    }
    /// <summary>
    /// مزامنة لقطات فروع الجهة في كل الملفات المرتبطة بالقيد (نشطة/مشطوبة/تريث) — S7:
    /// تحديث عمودي Branch/EntityBranch للصفوف ذات RegistryId==entryId بلا إعادة بناء
    /// SearchText (الفرع ليس جزءًا من نص البحث — F1). تُرجع الملفات المتأثرة لكتابة الوقوعات.
    /// </summary>
    private async Task<List<Document>> SyncBranchLabelsAsync(
        PublicEntity entry,
        string newBranch,
        CancellationToken token)
    {
        var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
        if (linkedDocs.Count == 0)
            return linkedDocs;
        foreach (var doc in linkedDocs)
        {
            foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == entry.Id))
                a.Branch = newBranch;
            foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == entry.Id))
                e.EntityBranch = newBranch;
        }
        await _uow.SaveChangesAsync(token);
        return linkedDocs;
    }
    /// <summary>وقفعة «تغيير جهة» آلية لكل ملف متأثر بعملية فرع (نفس نمط النقل/الدمج).</summary>
    private async Task InsertBranchOccurrencesAsync(
        IReadOnlyCollection<Document> docs,
        string details,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        if (docs.Count == 0)
            return;
        foreach (var doc in docs)
        {
            await _occurrences.AddAsync(new DocumentOccurrence
            {
                DocumentId = doc.Id,
                Source = OccurrenceSourceCatalog.System,
                OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                EventDate = DateTime.UtcNow,
                CreatedById = actor.UserId,
                Details = details,
            }, token);
        }
    }
    /// <summary>تنبيه فرعي لرؤساء محافظة القيد بتغيير فرع جهة عامة ضمن محافظتهم.</summary>
    private async Task InsertBranchChangeAlertAsync(
        PublicEntity entry,
        string message,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
        foreach (var head in heads.Where(h => h.BranchId.HasValue))
        {
            var alert = new HeadAlert
            {
                BranchId = head.BranchId!.Value,
                CreatedById = actor.UserId,
                PublicEntityId = entry.Id,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { new HeadAlertRecipient { UserId = head.Id } },
            };
            await _headAlerts.AddAsync(alert, token);
        }
    }
    /// <summary>اسم بديل «للبحث فقط» على قيد: الاسم الكامل القديم (المعتمد — المحافظة / الفرع).</summary>
    private static void AddEachExtraAlias(PublicEntity entry, string text, ref int aliasesAdded)
    {
        var norm = ArabicNameNormalizer.Normalize(text);
        if (norm.Length == 0 || entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == norm))
            return;
        entry.Aliases.Add(new PublicEntityAlias { PublicEntityId = entry.Id, AliasText = text });
        aliasesAdded++;
    }
    private static string FullEntryName(string canonical, string governorate, string branchName)
        => $"{canonical} — {governorate} / {branchName}";
}
