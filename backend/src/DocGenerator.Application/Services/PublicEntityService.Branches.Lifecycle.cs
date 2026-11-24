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
                HeadAlertMessages.EntityBranchUnified(new(target.Group.CanonicalName, target.Governorate, target.BranchName)),
                ActionKindCatalog.Unify, actor, token);
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
        string entityOperation,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        var occurredAt = DateTime.UtcNow;
        var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
        foreach (var branch in heads.Where(h => h.BranchId.HasValue).GroupBy(h => h.BranchId!.Value))
        {
            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    HeadAlertKindCatalog.EntityBranchChanged,
                    message,
                    HeadAlertAudience.BranchHeads,
                    branch.Key,
                    actor.UserId,
                    PublicEntityId: entry.Id,
                    EntityOperation: entityOperation),
                supersedePlan: null,
                occurredAt: occurredAt,
                ct: token);
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
