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
    // ── أداة إعادة تسمية الهوية الأم (المدير/المشرف — مستوى المجموعة) ──
    // القاعدة: إعادة التسمية على مستوى المجموعة لا تمس Governorate/BranchName/CitationFormula/CoverageLabel
    // — تغيّر Group.CanonicalName فقط، وتُحفظ الأسماء القديمة أسماءً بديلة (حجّة قانونية د5).

    /// <inheritdoc/>
    public async Task<RenameGroupPreviewResponse> PreviewRenameGroupAsync(
        RenameGroupPreviewRequest request, CancellationToken ct = default)
    {
        var group = await _entities.GetGroupAsync(request.GroupId, ct)
            ?? throw new ArgumentException("الهوية الأم غير موجودة");
        var newName = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        if (group.Entries.Any(e => e.NeedsReview))
            throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

        var affected = await CountDocumentsForGroupAsync(request.GroupId, ct);
        var branches = await BranchNamesForGroupAsync(request.GroupId, ct);
        return new RenameGroupPreviewResponse(group.CanonicalName, newName, affected, branches);
    }
    /// <inheritdoc/>
    public async Task<RenameGroupResponse> RenameGroupAsync(
        RenameGroupRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (!RolePermissions_IsFullAccess(actor.Role))
            throw new UnauthorizedAccessException("إعادة التسمية للمدير أو المشرف فقط");

        var newCanonical = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        return await _tx.RunAsync(async token =>
        {
            var group = await _entities.GetGroupAsync(request.GroupId, token)
                ?? throw new ArgumentException("الهوية الأم غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException("الهوية الأم غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الأم قبل إعادة تسميتها");

            var oldCanonical = group.CanonicalName;
            if (string.Equals(ArabicNameNormalizer.Normalize(oldCanonical),
                ArabicNameNormalizer.Normalize(newCanonical), StringComparison.Ordinal))
                throw new ArgumentException("الاسم الجديد مطابق للاسم الحالي");

            await EnsureCanonicalAvailableAsync(newCanonical, group.Id, token);

            group.CanonicalName = newCanonical;

            // حفظ الاسم القديم اسمًا بديلًا (حجّة قانونية): يُضاف على القيد الأم بمحافظة الفرع
            // وعلى كل قيود المجموعة ليبقى البحث بالاسم القديم يعثر على الجهة.
            var entries = await _entities.ListEntriesByGroupAsync(group.Id, token);
            foreach (var entry in entries.Where(e => e.IsActive))
            {
                var normOld = ArabicNameNormalizer.Normalize(oldCanonical);
                if (!entry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normOld))
                {
                    entry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = entry.Id,
                        AliasText = oldCanonical,
                    });
                }
            }

            // مزامنة النصوص على مستوى المجموعة (الاسم القديم ← الجديد عبر كل الملفات المرتبطة)
            var affectedDocs = await SyncTextsAfterRenameAsync(oldCanonical, newCanonical, actor.Name, token);
            var affected = affectedDocs.Count;

            // مزامنة لقطات أطراف الاستئنافات المرتبطة بنفس الملفات المتأثرة
            await SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

            // سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                oldCanonicalNames = new[] { oldCanonical },
                newCanonical = group.CanonicalName,
                entityType = group.EntityType,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                affectedDocuments = affected,
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = group.Id,
                ActionKind = ActionKindCatalog.Rename,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر (المزامنة الاسمية الفعلية — لا إعادة استعلام عبر RegistryId)
            foreach (var docId in affectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.RenameOccurrence(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.RenameLawyersAlert(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.RenameHeadsAlert(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_public_entity_group",
                documentId: null, documentType: null,
                details: $"أعاد تسمية الهوية الأم: «{oldCanonical}» إلى «{group.CanonicalName}» بموجب {BuildDecreeSuffix(decreeKind, decreeNumber, decreeDate)} — {affected} ملفًا متأثرًا",
                ct: token);

            return new RenameGroupResponse(group.Id, oldCanonical, group.CanonicalName, affected, changeEvent.Id);
        }, ct);
    }
    // ── أداة الحلول (إلغاء عدة هويات أم واستبدالها بهوية جديدة) ──

    /// <inheritdoc/>
    public async Task<AbolishReplacePreviewResponse> PreviewAbolishAndReplaceAsync(
        AbolishReplacePreviewRequest request, CancellationToken ct = default)
    {
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var names = new List<string>();
        var affectedDocs = 0;
        var branches = new HashSet<string>(StringComparer.Ordinal);
        var abolishedGroupIds = new HashSet<int>(request.AbolishedGroupIds);

        foreach (var id in request.AbolishedGroupIds.Distinct())
        {
            var group = await _entities.GetGroupAsync(id, ct)
                ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
            if (!group.IsActive)
                throw new ArgumentException($"الهوية الأم «{group.CanonicalName}» غير نشطة");
            if (group.Entries.Any(e => e.NeedsReview))
                throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{group.CanonicalName}» قبل الإلغاء");
            names.Add(group.CanonicalName);
            affectedDocs += await CountDocumentsForGroupAsync(id, ct);
            foreach (var b in await BranchNamesForGroupAsync(id, ct))
                branches.Add(b);
        }

        var delegates = await _users.ListEntityManagersByGroupIdsAsync(abolishedGroupIds, ct);
        return new AbolishReplacePreviewResponse(
            names, request.AbolishedGroupIds.Count,
            await CountActiveEntriesForGroupsAsync(abolishedGroupIds, ct),
            affectedDocs, delegates.Count, branches.OrderBy(x => x).ToList());
    }
    /// <inheritdoc/>
    public async Task<AbolishAndReplaceResponse> AbolishAndReplaceAsync(
        AbolishAndReplaceRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (!RolePermissions_IsFullAccess(actor.Role))
            throw new UnauthorizedAccessException("الحلول (إلغاء واستبدال) للمدير أو المشرف فقط");
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var newCanonical = Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var entityType = ValidEntityType(request.EntityType);
        var governorate = Required(request.Governorate, "المحافظة مطلوبة", 100);
        var citationFormula = ValidCitationFormula(request.CitationFormula, CitationFormulaCatalog.AddToJob);
        var coverageLabel = ValidateCoverageLabel(request.CoverageLabel);
        var decreeKind = Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
        var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرجع");
        if (decreeDate is null)
            throw new ArgumentException("تاريخ المرجع مطلوب — استخدم مثال: 1/8/2026");

        var abolishedIds = request.AbolishedGroupIds.Distinct().ToList();

        return await _tx.RunAsync(async token =>
        {
            // 1) تحقق: كل الجهات المُلغاة نشطة بلا NeedsReview؛ الاسم الجديد فريد
            foreach (var id in abolishedIds)
            {
                var g = await _entities.GetGroupAsync(id, token)
                    ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
                if (!g.IsActive)
                    throw new ArgumentException($"الهوية الأم «{g.CanonicalName}» غير نشطة");
                if (g.Entries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{g.CanonicalName}» قبل الإلغاء");
            }
            await EnsureCanonicalAvailableAsync(newCanonical, 0, token);

            // 2) إنشاء الهوية الأم الجديدة + قيدها الأم
            var newGroup = new PublicEntityGroup
            {
                CanonicalName = newCanonical,
                EntityType = entityType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            await _entities.AddGroupAsync(newGroup, token);
            await _uow.SaveChangesAsync(token);

            var newParentEntry = new PublicEntity
            {
                GroupId = newGroup.Id,
                Group = newGroup,
                Governorate = governorate,
                BranchName = DefaultBranchName,
                IsParentEntity = true,
                CoverageLabel = coverageLabel,
                CitationFormula = citationFormula,
                Status = EntityStatusCatalog.Final,
                IsActive = true,
                NeedsReview = false,
                CreatedById = actor.UserId,
                CreatedAt = DateTime.UtcNow,
            };
            var aliases = CleanAliases(request.Aliases, ArabicNameNormalizer.Normalize(newCanonical));
            foreach (var alias in aliases)
                newParentEntry.Aliases.Add(new PublicEntityAlias { PublicEntityId = newParentEntry.Id, AliasText = alias });
            await _entities.AddEntryAsync(newParentEntry, token);
            await _uow.SaveChangesAsync(token);

            // 3) ترحيل روابط القيود الفعّالة للجهات المُلغاة إلى القيد الأم الجديد + إيقافها
            var abolishedIdsSet = new HashSet<int>(abolishedIds);
            var abolishedNames = new List<string>();
            var affectedDocsById = new Dictionary<int, Document>();
            var entriesMoved = 0;

            foreach (var id in abolishedIds)
            {
                var group = await _entities.GetGroupAsync(id, token)
                    ?? throw new ArgumentException($"الهوية الأم #{id} غير موجودة");
                abolishedNames.Add(group.CanonicalName);
                var entries = await _entities.ListEntriesByGroupAsync(id, token);

                foreach (var entry in entries.Where(e => e.IsActive))
                {
                    var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
                    foreach (var doc in linkedDocs)
                    {
                        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == entry.Id))
                        {
                            a.RegistryId = newParentEntry.Id;
                            // تحديث مباشر للاسم: يُحلّ الاسم الجديد محل القديم في نص الطالب
                            // (ApplicantTextBuilder يقرأ e.Name)، فلا يبقى الاسم المُلغى في النصوص.
                            a.Name = newCanonical;
                        }
                        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == entry.Id))
                        {
                            e.RegistryId = newParentEntry.Id;
                            e.EntityName = newCanonical;
                        }
                        foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == entry.Id))
                        {
                            ea.RegistryId = newParentEntry.Id;
                            ea.Name = newCanonical;
                        }
                        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                        if (!affectedDocsById.ContainsKey(doc.Id))
                            affectedDocsById[doc.Id] = doc;
                        await _occurrences.AddAsync(new DocumentOccurrence
                        {
                            DocumentId = doc.Id,
                            Source = OccurrenceSourceCatalog.System,
                            OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                            EventDate = DateTime.UtcNow,
                            CreatedById = actor.UserId,
                            Details = EntityChangeMessages.AbolishOccurrence(newCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                        }, token);
                    }

                    entry.IsActive = false;
                    entriesMoved++;
                }

                // حفظ أسماء الجهات المُلغاة أسماءً بديلة على القيد الجديد (مرة لكل مجموعة ملغاة)
                var norm = ArabicNameNormalizer.Normalize(group.CanonicalName);
                if (!newParentEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == norm))
                    newParentEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = newParentEntry.Id,
                        AliasText = group.CanonicalName,
                    });

                group.IsActive = false;
            }

            // 4) مزامنة النصوص للملفات المتأثرة (يحلّ الاسم الجديد محل القديم)
            if (affectedDocsById.Count > 0)
            {
                var affectedDocsList = affectedDocsById.Values.ToList();
                await SyncTextsAfterFoldAsync(affectedDocsList, actor.Name, token);

                // مزامنة لقطات أطراف الاستئنافات للملفات المتأثرة (تُطابق صور الجهة العامة
                // عبر (Kind, PartyId) فيلتقط حتى الصور المخزَّنة باسم مختلف عن الاسم المعياري).
                await SyncAppealsAfterEntityChangeAsync(affectedDocsList, actor, token);
            }
            await _uow.SaveChangesAsync(token);

            // 5) ترحيل مندوبي الجهات المُلغاة إلى الهوية الجديدة (مواءمة 7-ز)
            var delegatesCount = await MigrateDelegatesAsync(abolishedIdsSet, newGroup.Id, newParentEntry.Id, null, token);

            // 6) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                abolishedGroupIds = abolishedIds,
                oldCanonicalNames = abolishedNames,
                newCanonical = newGroup.CanonicalName,
                entityType = newGroup.EntityType,
                governorate,
                branchName = DefaultBranchName,
                entriesMoved,
                affectedDocuments = affectedDocsById.Count,
                delegatesReassigned = delegatesCount,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate!.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = newGroup.Id,
                ActionKind = ActionKindCatalog.Abolish,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // 8) تنبيه عام لكل المحامين + رؤساء الأقسام
            var abolishedNamesJoined = string.Join('،', abolishedNames);
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.AbolishLawyersAlert(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.AbolishHeadsAlert(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "abolish_and_replace_entity",
                documentId: null, documentType: null,
                details: $"حلّت «{newGroup.CanonicalName}» محل {abolishedIds.Count} هويات، {entriesMoved} قيدًا، {affectedDocsById.Count} ملفًا متأثرًا، {delegatesCount} مندوبًا",
                ct: token);

            return new AbolishAndReplaceResponse(
                newGroup.Id, newGroup.CanonicalName, abolishedIds.Count, entriesMoved,
                affectedDocsById.Count, changeEvent.Id);
        }, ct);
    }
    private async Task<int> CountDocumentsForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var ids = new HashSet<int>();
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            foreach (var d in await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token))
                ids.Add(d.Id);
        }
        return ids.Count;
    }
    private async Task<List<string>> BranchNamesForGroupAsync(int groupId, CancellationToken token)
    {
        var entries = await _entities.ListEntriesByGroupAsync(groupId, token);
        var branches = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.IsActive))
        {
            if (!string.IsNullOrWhiteSpace(entry.BranchName) && entry.BranchName != DefaultBranchName)
                branches.Add(entry.BranchName);
        }
        return branches.OrderBy(x => x).ToList();
    }
    private async Task<int> CountActiveEntriesForGroupsAsync(IReadOnlyCollection<int> groupIds, CancellationToken token)
    {
        var count = 0;
        foreach (var id in groupIds)
            count += (await _entities.ListEntriesByGroupAsync(id, token)).Count(e => e.IsActive);
        return count;
    }
    private static bool RolePermissions_IsFullAccess(DocGenerator.Domain.Enums.UserRole role)
        => role is DocGenerator.Domain.Enums.UserRole.Manager or DocGenerator.Domain.Enums.UserRole.Admin;
}
