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
    // ── نقل القيد (د3) ──

    /// <inheritdoc/>
    public async Task<MoveEntryResponse> MoveEntryAsync(int entryId, MoveEntryRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        if (request.TargetGroupId is null && request.TargetEntryId is null)
            throw new ArgumentException("حدّد الهوية الأم الهدف (TargetGroupId) أو القيد الهدف (TargetEntryId)");

        if (request.TargetEntryId.HasValue && request.TargetEntryId.Value == entryId)
            throw new ArgumentException("لا يمكن طيّ قيد على نفسه");

        return await _tx.RunAsync(async token =>
        {
            var entry = await _entities.GetEntryWithDetailsAsync(entryId, token)
                ?? throw new ArgumentException("القيد غير موجود");

            if (entry.NeedsReview)
                throw new ArgumentException("لا يمكن نقل قيد بانتظار المراجعة؛ اعتمده أولًا");

            // حوكمة S1 — منع صريح للجميع: قيد «الجهة الأم» لا يُنقَل ولا يُطوى أصلًا؛
            // الحارس على المصدر فقط، فالطيُّ باتجاه أمّ هدفٍ يبقى مباحًا في وضع (ب).
            GuardNotParentEntry(entry, "القيد الأم لا يُنقَل ولا يُطوى — إعادة الهيكلة عبر الإلغاء والاستبدال المركزي");

            var fromGroupId = entry.GroupId;
            var fromGroupName = entry.Group.CanonicalName;
            int toGroupId;
            int affectedDocs = 0;
            int targetEntryId;
            var affectedDocIds = new List<int>();

            if (request.TargetEntryId.HasValue)
            {
                // وضع ب: الطيّ في قيد مطابق
                var targetEntry = await _entities.GetEntryAsync(request.TargetEntryId.Value, token)
                    ?? throw new ArgumentException("القيد الهدف غير موجود");
                if (!targetEntry.IsActive)
                    throw new ArgumentException("القيد الهدف غير نشط");
                if (targetEntry.Governorate != entry.Governorate || targetEntry.BranchName != entry.BranchName)
                    throw new ArgumentException("الطيّ يتطلب مطابقة المحافظة والفرع");

                toGroupId = targetEntry.GroupId;
                // نطاق رئيس القسم: القيد المنقول نفسه يجب أن يكون ضمن نطاقه (لمحامٍ من فرعه).
                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);
                targetEntryId = targetEntry.Id;

                // ترحيل روابط RegistryId
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entryId, token);
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                foreach (var doc in linkedDocs)
                    RepointEntryLinks(doc, entryId, targetEntryId);
                affectedDocs = linkedDocs.Count;

                // ترحيل مندوبي مستوى القيد إلى القيد الناجي (نطاق بوابة المحاماة يتبدل مع القيد المطوي)
                var entryDelegates = await _users.ListEntityManagersByEntryIdAsync(entryId, token);
                foreach (var del in entryDelegates)
                {
                    del.PortalGroupId = targetEntry.GroupId;
                    del.PortalEntryId = targetEntryId;
                }

                // إيقاف القيد المنقول
                entry.IsActive = false;

                // إضافة الاسم الكامل كاسم بديل للهدف
                var fullName = $"{entry.Group.CanonicalName} — {entry.Governorate} / {entry.BranchName}";
                var normalizedEntry = ArabicNameNormalizer.Normalize(entry.Group.CanonicalName);
                if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
                {
                    targetEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = targetEntry.Id,
                        AliasText = entry.Group.CanonicalName,
                    });
                }
                // إضافة النص الكامل أيضًا
                var normalizedFull = ArabicNameNormalizer.Normalize(fullName);
                if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedFull))
                {
                    targetEntry.Aliases.Add(new PublicEntityAlias
                    {
                        PublicEntityId = targetEntry.Id,
                        AliasText = fullName,
                    });
                }

                // مزامنة النصوص
                await SyncTextsAfterFoldAsync(linkedDocs, actor.Name, token);
            }
            else
            {
                // وضع أ: تغيير الهوية الأم
                var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId!.Value, token)
                    ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
                if (!targetGroup.IsActive)
                    throw new ArgumentException("الهوية الأم الهدف غير نشطة");
                if (targetGroup.Id == entry.GroupId)
                    throw new ArgumentException("القيد موجود مسبقًا في الهوية الأم الهدف");
                toGroupId = targetGroup.Id;
                targetEntryId = entryId;

                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);

                // فحص تعارض المحافظة والفرع
                var conflict = await _entities.FindEntryInGroupAsync(toGroupId, entry.Governorate, entry.BranchName, token);
                if (conflict is not null)
                    throw new ArgumentException(
                        $"يوجد قيد مطابق ({conflict.BranchName}) في الهوية الهدف؛ استخدم وضع الطيّ (TargetEntryId={conflict.Id}) بدلاً من ذلك");

                entry.GroupId = toGroupId;
                entry.Group = targetGroup;

                // مزامنة النصوص
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entryId, token);
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                affectedDocs = linkedDocs.Count;
                foreach (var doc in linkedDocs)
                    doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
                await SyncTextsAfterFoldAsync(linkedDocs, actor.Name, token);
            }

            // كتابة ChangeEvent
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                fromGroup = fromGroupName,
                toGroup = (await _entities.GetGroupAsync(toGroupId, token))?.CanonicalName ?? "",
                fromGroupId,
                toGroupId,
                entryName = entry.Group.CanonicalName,
                governorate = entry.Governorate,
                branchName = entry.BranchName,
                mode = request.TargetEntryId.HasValue ? "fold" : "reassign",
                affectedDocuments = affectedDocs,
                decreeKind = request.DecreeKind,
                decreeNumber = request.DecreeNumber,
                decreeDate = request.DecreeDate,
                note = request.Note,
            });
            var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرسوم");
            var changeEvent = new PublicEntityChangeEvent
            {
                EntryId = entryId,
                GroupId = fromGroupId,
                ActionKind = ActionKindCatalog.Move,
                DecreeKind = request.DecreeKind,
                DecreeNumber = request.DecreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر
            if (affectedDocIds.Count > 0)
            {
                foreach (var docId in affectedDocIds.Distinct())
                {
                    var occurrence = new DocumentOccurrence
                    {
                        DocumentId = docId,
                        Source = OccurrenceSourceCatalog.System,
                        OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                        EventDate = DateTime.UtcNow,
                        CreatedById = actor.UserId,
                        Details = EntityChangeMessages.MoveOccurrence(entry.Group.CanonicalName, entry.Governorate, entry.BranchName, request.DecreeKind ?? "", request.DecreeNumber ?? "", decreeDate),
                    };
                    await _occurrences.AddAsync(occurrence, token);
                }
            }

            // تنبيه رئيس الهوية الجديدة
            var heads = await _entities.ListActiveHeadsByGovernorateAsync(entry.Governorate, token);
            var targetHeads = heads.Where(h => h.BranchId.HasValue);
            foreach (var head in targetHeads)
            {
                var msg = $"أُلحق بقيدكم فرع من هيئة أخرى: «{entry.Group.CanonicalName}» — {entry.Governorate}/{entry.BranchName}";
                var alert = new HeadAlert
                {
                    BranchId = head.BranchId!.Value,
                    CreatedById = actor.UserId,
                    TargetType = HeadAlertTargetType.Branch,
                    Message = msg.Length > 2000 ? msg[..2000] : msg,
                    CreatedAt = DateTime.UtcNow,
                    Recipients = { new HeadAlertRecipient { UserId = head.Id } },
                };
                await _headAlerts.AddAsync(alert, token);
            }

            await _uow.SaveChangesAsync(token);

            // تدقيق
            await _audit.LogAsync(actor.Name, "move_entity_registry",
                documentId: null, documentType: null,
                details: $"نقل قيد «{entry.Group.CanonicalName}» ({entry.Governorate}/{entry.BranchName}) من «{fromGroupName}» ← هوية #{toGroupId} — {affectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MoveEntryResponse(entryId, fromGroupId, toGroupId, affectedDocs, changeEvent.Id);
        }, ct);
    }
    /// <inheritdoc/>
    public async Task<MoveAllEntriesResponse> MoveAllEntriesAsync(MoveAllEntriesRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            var sourceGroup = await _entities.GetGroupAsync(request.SourceGroupId, token)
                ?? throw new ArgumentException("الهوية الأم المصدر غير موجودة");
            var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
            if (!targetGroup.IsActive)
                throw new ArgumentException("الهوية الأم الهدف غير نشطة");
            if (request.SourceGroupId == request.TargetGroupId)
                throw new ArgumentException("الهوية الأم المصدر والهدف متطابقتان");

            var sourceEntries = sourceGroup.Entries.Where(e => e.IsActive).ToList();
            if (sourceEntries.Count == 0)
                throw new ArgumentException("لا يوجد قيود نشطة في الهوية الأم المصدر");

            int totalAffectedDocs = 0;
            int entriesMoved = 0;
            var affectedDocIds = new List<int>();

            foreach (var entry in sourceEntries)
            {
                await EnsureHeadScopeAsync(actor, entry, entry.Governorate, token);

                // فحص تعارض
                var conflict = await _entities.FindEntryInGroupAsync(request.TargetGroupId, entry.Governorate, entry.BranchName, token);
                if (conflict is not null)
                    throw new ArgumentException(
                        $"تعارض: القيد «{entry.Governorate}/{entry.BranchName}» موجود مسبقًا في الهوية الهدف (قيد #{conflict.Id})");

                entry.GroupId = request.TargetGroupId;
                entry.Group = targetGroup;

                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(entry.Id, token);
                totalAffectedDocs += linkedDocs.Count;
                affectedDocIds.AddRange(linkedDocs.Select(d => d.Id));
                foreach (var doc in linkedDocs)
                    doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);

                entriesMoved++;
            }

            // ChangeEvent واحد لكل عملية نقل جماعي
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                fromGroup = sourceGroup.CanonicalName,
                toGroup = targetGroup.CanonicalName,
                fromGroupId = sourceGroup.Id,
                toGroupId = targetGroup.Id,
                entriesMoved,
                affectedDocuments = totalAffectedDocs,
                decreeKind = request.DecreeKind,
                decreeNumber = request.DecreeNumber,
                decreeDate = request.DecreeDate,
                note = request.Note,
            });
            var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرسوم");
            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = sourceGroup.Id,
                ActionKind = ActionKindCatalog.Move,
                DecreeKind = request.DecreeKind,
                DecreeNumber = request.DecreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);

            await _uow.SaveChangesAsync(token);

            // وقوعات آلية لكل ملف متأثر
            if (affectedDocIds.Count > 0)
            {
                foreach (var docId in affectedDocIds.Distinct())
                {
                    var occurrence = new DocumentOccurrence
                    {
                        DocumentId = docId,
                        Source = OccurrenceSourceCatalog.System,
                        OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                        EventDate = DateTime.UtcNow,
                        CreatedById = actor.UserId,
                        Details = EntityChangeMessages.MoveAllOccurrence(sourceGroup.CanonicalName, targetGroup.CanonicalName, request.DecreeKind ?? "", request.DecreeNumber ?? "", decreeDate),
                    };
                    await _occurrences.AddAsync(occurrence, token);
                }
            }

            await _uow.SaveChangesAsync(token);
            var affectedGovernorates = sourceEntries.Select(e => e.Governorate).Distinct().ToList();
            foreach (var gov in affectedGovernorates)
            {
                var heads = await _entities.ListActiveHeadsByGovernorateAsync(gov, token);
                foreach (var head in heads.Where(h => h.BranchId.HasValue))
                {
                    var msg = $"تم نقل جميع قيود «{sourceGroup.CanonicalName}» ({gov}) إلى «{targetGroup.CanonicalName}»";
                    var alert = new HeadAlert
                    {
                        BranchId = head.BranchId!.Value,
                        CreatedById = actor.UserId,
                        TargetType = HeadAlertTargetType.Branch,
                        Message = msg.Length > 2000 ? msg[..2000] : msg,
                        CreatedAt = DateTime.UtcNow,
                        Recipients = { new HeadAlertRecipient { UserId = head.Id } },
                    };
                    await _headAlerts.AddAsync(alert, token);
                }
            }

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "move_all_entity_registry",
                documentId: null, documentType: null,
                details: $"نقل جميع القيود ({entriesMoved}) من «{sourceGroup.CanonicalName}» ← «{targetGroup.CanonicalName}» — {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            return new MoveAllEntriesResponse(sourceGroup.Id, targetGroup.Id, entriesMoved, totalAffectedDocs, changeEvent.Id);
        }, ct);
    }
}
