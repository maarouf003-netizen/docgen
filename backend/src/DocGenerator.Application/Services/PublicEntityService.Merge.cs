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
    // ── الدمج N←1 (د5 §4) ──

    /// <inheritdoc/>
    public async Task<MergePreviewResponse> PreviewMergeAsync(MergePreviewRequest request, CancellationToken ct = default)
    {
        var survivorGroup = await _entities.GetGroupAsync(request.SurvivorGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الناجية غير موجودة");
        if (!survivorGroup.IsActive)
            throw new ArgumentException("الهوية الأم الناجية غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للدمج");

        if (request.AbsorbedGroupIds.Contains(request.SurvivorGroupId))
            throw new ArgumentException("لا يمكن دمج هوية في نفسها");

        var survivorEntryEntities = await _entities.ListEntriesByGroupAsync(survivorGroup.Id, ct);
        var activeSurvivorEntries = survivorEntryEntities.Where(e => e.IsActive).ToList();
        if (activeSurvivorEntries.Count == 0)
            throw new ArgumentException("الهوية الأم الناجية بلا قيود نشطة");

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupPreviewDto>();
        int totalAffected = 0;

        foreach (var ae in survivorEntryEntities.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{survivorGroup.CanonicalName}» (الناجي) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntryEntities = await _entities.ListEntriesByGroupAsync(absorbedId, ct);

            foreach (var ae in absorbedEntryEntities.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            var entryDtos = new List<AbsorbedEntryPreviewDto>();
            int groupDocCount = 0;

            foreach (var ae in absorbedEntryEntities)
            {
                var matchedSurvivor = activeSurvivorEntries
                    .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                var defaultEntry = activeSurvivorEntries.First();
                int mappedToId = matchedSurvivor?.Id ?? defaultEntry.Id;
                bool conflictsWithSurvivor = matchedSurvivor is null;

                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, ct);
                int docCount = linkedDocs.Count;
                groupDocCount += docCount;

                entryDtos.Add(new AbsorbedEntryPreviewDto(
                    ae.Id, ae.Governorate, ae.BranchName,
                    docCount,
                    mappedToId, conflictsWithSurvivor));
            }

            totalAffected += groupDocCount;

            var aliases = absorbedEntryEntities
                .SelectMany(e => e.Aliases)
                .Select(a => a.AliasText)
                .Distinct()
                .ToList();

            absorbedDtos.Add(new AbsorbedGroupPreviewDto(
                absorbedGroup.Id, absorbedGroup.CanonicalName,
                entryDtos, groupDocCount, aliases));
        }

        return new MergePreviewResponse(survivorGroup.CanonicalName, absorbedDtos, totalAffected, warnings);
    }
    /// <inheritdoc/>
    public async Task<MergeCommitResponse> CommitMergeAsync(MergeCommitRequest request, EntityRegistryActor actor, CancellationToken ct = default, string? idempotencyKey = null)
    {
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        // RF-011: حجز المفتاح بعد التحقق (المرجع الباطل 400 بلا حجز) — التكرار
        // يُعيد النتيجة المخزنة نفسها.
        var ticket = await IdempotencyGuard.BeginAsync(_idempotency, "registry.merge-commit",
            actor.UserId, idempotencyKey, IdempotencyGuard.Fingerprint(request), ct);
        MergeCommitResponse result;
        try
        {
            result = await CommitMergeCoreAsync(request, actor, decreeKind, decreeNumber, decreeDate, ct);
        }
        catch
        {
            await IdempotencyGuard.ReleaseAsync(_idempotency, ticket, ct);
            throw;
        }

        await IdempotencyGuard.CompleteAsync(_idempotency, ticket, IdempotencyGuard.Snapshot(result), ct);
        return result;
    }

    private async Task<MergeCommitResponse> CommitMergeCoreAsync(
        MergeCommitRequest request, EntityRegistryActor actor,
        string decreeKind, string decreeNumber, DateTime? decreeDate, CancellationToken ct)
    {
        return await _tx.RunAsync(async token =>
        {
            var survivorGroup = await _entities.GetGroupAsync(request.SurvivorGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الناجية غير موجودة");
            if (!survivorGroup.IsActive)
                throw new ArgumentException("الهوية الأم الناجية غير نشطة");

            if (request.AbsorbedGroupIds.Count == 0)
                throw new ArgumentException("حدد هوية أم واحدة على الأقل للدمج");

            if (request.AbsorbedGroupIds.Contains(request.SurvivorGroupId))
                throw new ArgumentException("لا يمكن دمج هوية في نفسها");

            var survivorEntries = await _entities.ListEntriesByGroupAsync(survivorGroup.Id, token);
            var activeSurvivorEntries = survivorEntries.Where(e => e.IsActive).ToList();

            if (activeSurvivorEntries.Count == 0)
                throw new ArgumentException("الهوية الأم الناجية بلا قيود نشطة");

            if (survivorEntries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم الناجية قبل الدمج");

            // اسم نهائي اختياري على مستوى المجموعة (7-هـ): يُطبَّق قبل ترحيل الروابط كي تزامن النصوص
            // في خطوة واحدة بنهاية المعاملة الاسمَ الأخير.
            string? previousSurvivorName = null;
            if (!string.IsNullOrWhiteSpace(request.NewCanonicalName))
            {
                var newName = Required(request.NewCanonicalName, "الاسم النهائي مطلوب", 200);
                previousSurvivorName = survivorGroup.CanonicalName;
                await EnsureCanonicalAvailableAsync(newName, survivorGroup.Id, token);
                survivorGroup.CanonicalName = newName;
            }

            var absorbedGroupsProcessed = 0;
            var entriesMigrated = 0;
            var aliasesAdded = 0;
            var totalAffectedDocs = 0;
            var affectedDocsById = new Dictionary<int, Document>();
            var branchMap = new List<object>();
            var entryTargetByAbsorbed = new Dictionary<int, int>();
            var absorbedNames = new List<string>();

            // حفظ الاسم القديم للناجي اسمًا بديلًا (حجّة قانونية) على كل قيوده النشطة،
            // كي يبقى البحث بالاسم القديم يعثر على الجهة بعد الدمج (مواءمة 7-هـ وإعادة التسمية).
            if (previousSurvivorName is not null)
            {
                var normOldSurvivor = ArabicNameNormalizer.Normalize(previousSurvivorName);
                foreach (var se in activeSurvivorEntries.Where(e => e.IsActive))
                {
                    if (!se.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normOldSurvivor))
                    {
                        se.Aliases.Add(new PublicEntityAlias
                        {
                            PublicEntityId = se.Id,
                            AliasText = previousSurvivorName,
                        });
                        aliasesAdded++;
                    }
                }
            }

            foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
            {
                var absorbedGroup = await _entities.GetGroupAsync(absorbedId, token)
                    ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
                if (!absorbedGroup.IsActive)
                    throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");
                absorbedNames.Add(absorbedGroup.CanonicalName);

                var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, token);

                if (absorbedEntries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{absorbedGroup.CanonicalName}» قبل الدمج");

                foreach (var ae in absorbedEntries.Where(e => e.IsActive))
                {
                    var matchedSurvivor = activeSurvivorEntries
                        .FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                    var targetEntry = matchedSurvivor ?? activeSurvivorEntries.First();
                    // خريطة طيّ القيد الممتصّ إلى قيد الناجي (فرعًا بفرع)، تُستخدم لاحقًا
                    // لترحيل مندوبي القيود إلى نِسَبهم الفرعية الصحيحة بدل طيّهم على أول قيد.
                    entryTargetByAbsorbed[ae.Id] = targetEntry.Id;

                    // ترحيل روابط RegistryId + الأسماء البديلة (مساعدا الطيّ المشتركان)
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                    }

                    foreach (var doc in linkedDocs)
                        RepointEntryLinks(doc, ae.Id, targetEntry.Id);

                    // إيقاف القيد المُدمَج
                    ae.IsActive = false;

                    AddFoldAliases(targetEntry, absorbedGroup.CanonicalName, ae, ref aliasesAdded);

                    branchMap.Add(new
                    {
                        absorbedEntryId = ae.Id,
                        absorbedGov = ae.Governorate,
                        absorbedBranch = ae.BranchName,
                        targetEntryId = targetEntry.Id,
                        targetGov = targetEntry.Governorate,
                        targetBranch = targetEntry.BranchName,
                        docsAffected = linkedDocs.Count,
                    });

                    entriesMigrated++;
                }

                absorbedGroup.IsActive = false;
                absorbedGroupsProcessed++;
            }

            // مزامنة النصوص على مستوى المجموعة (تعويض): يُستبدل كل اسم قديم — أسماء الجهات
            // المُدمجة واسم الناجي السابق إن تغيّر — بالاسم النهائي عبر كل الملفات المرتبطة،
            // بما فيها الملفات المربوطة بقيود الناجي نفسها (مواءمة 7-هـ و7-و).
            var mergeTargetName = survivorGroup.CanonicalName;
            var namesToSync = absorbedNames
                .Append(previousSurvivorName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .GroupBy(n => ArabicNameNormalizer.Normalize(n!))
                .Select(g => g.First()!)
                .ToList();
            var nameMatchedDocs = new Dictionary<int, Document>();
            foreach (var oldName in namesToSync)
            {
                var matched = await SyncTextsAfterRenameAsync(oldName!, mergeTargetName, actor.Name, token);
                foreach (var doc in matched)
                    nameMatchedDocs[doc.Id] = doc;
            }

            // عدد الملفات المتأثرة = اتحاد المترحلة عبر RegistryId والمُلتقطة بالمزامنة الاسمية،
            // ليوحّد العداد مع السلوك في UnifyNamesAsync بدل اقتِصاره على المترحلة فقط (اتساق 7-و).
            totalAffectedDocs = CountUniqueDocuments(nameMatchedDocs, affectedDocsById);

            // أعد بناء نصوص المستندات المتأثرة بالترحيل (RegistryId) التي لم تُلتقط بالمزامنة الاسمية.
            var affectedDocs = affectedDocsById.Values.ToList();
            if (affectedDocs.Count > 0)
            {
                await SyncTextsAfterFoldAsync(affectedDocs, actor.Name, token);
            }

            // مزامنة لقطات أطراف الاستئنافات عبر اتحاد الملفات المتأثرة (الاسمية + المترحلة).
            // تُطابق صور الجهة العامة داخل اللقطة عبر (Kind, PartyId) ومعرّف صف الوصلة بالملف،
            // فتلتقط حتى الصور المسماة بخلاف الاسم المعياري. يبني الدالة خريطة أسماء الصفوف
            // الحالية فيغدو التحديث مستقرًا (idempotent) مهما اختلف مسار التقاط الملف.
            var allAffectedDocs = nameMatchedDocs.Values
                .Concat(affectedDocsById.Values)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();
            if (allAffectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(allAffectedDocs, actor, token);

            // ترحيل مندوبي الجهات المُدمجة إلى الناجية (مواءمة 7-ز):
            // المطابقة الفرعية عبر خريطة الطيّ، والارتكاز على أول قيد ناجٍ عند غياب المطابق.
            var absorbedIdsSet = new HashSet<int>(request.AbsorbedGroupIds.Distinct());
            await MigrateDelegatesAsync(
                absorbedIdsSet, survivorGroup.Id,
                activeSurvivorEntries.FirstOrDefault()?.Id, entryTargetByAbsorbed, token);

            // حدث الدمج الأب
            var renamedSurvivor = previousSurvivorName is not null;
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                survivorGroupId = survivorGroup.Id,
                survivorGroup = survivorGroup.CanonicalName,
                oldCanonicalNames = absorbedNames,
                absorbedGroupIds = request.AbsorbedGroupIds,
                newCanonical = survivorGroup.CanonicalName,
                renamedSurvivor,
                entriesMigrated,
                aliasesAdded,
                totalAffectedDocs,
                branchMap,
                unifyTexts = request.UnifyTexts,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = survivorGroup.Id,
                ActionKind = ActionKindCatalog.Merge,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر (اتحاد الاسمية + المترحلة عبر RegistryId)
            var absorbedNamesJoined = string.Join('،', absorbedNames);
            foreach (var docId in allAffectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.MergeOccurrence(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.MergeLawyersAlert(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.MergeHeadsAlert(absorbedNamesJoined, survivorGroup.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "merge_entity_registry",
                documentId: null, documentType: null,
                details: $"دمج {absorbedGroupsProcessed} هويات أم في «{survivorGroup.CanonicalName}» بموجب {BuildDecreeSuffix(decreeKind, decreeNumber, decreeDate)} — {entriesMigrated} قيد، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MergeCommitResponse(absorbedGroupsProcessed, entriesMigrated, aliasesAdded, totalAffectedDocs, changeEvent.Id);
        }, ct);
    }
}
