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
    // ── توحيد التسمية N←1 (المدير/المشرف — بلا هجرة ملفات) ──

    /// <inheritdoc/>
    public async Task<UnifyNamesPreviewResponse> PreviewUnifyAsync(UnifyNamesPreviewRequest request, CancellationToken ct = default)
    {
        var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, ct)
            ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
        if (!targetGroup.IsActive)
            throw new ArgumentException("الهوية الأم الهدف غير نشطة");

        if (request.AbsorbedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للتوحيد");

        if (request.AbsorbedGroupIds.Contains(request.TargetGroupId))
            throw new ArgumentException("لا يمكن توحيد هوية مع نفسها");

        var targetEntries = await _entities.ListEntriesByGroupAsync(targetGroup.Id, ct);
        var activeTarget = targetEntries.Where(e => e.IsActive).ToList();
        // بركة الناجين: محاكاة مطابقة لحلقة التنفيذ في UnifyNamesAsync — تبدأ بنسخ القيود
        // النشطة للهدف ويُلحق بها كل قيد يُنقل؛ المطابقة خام (Ordinal) على (المحافظة/الفرع).
        var survivorPool = new List<PublicEntity>(activeTarget);

        var warnings = new List<string>();
        var absorbedDtos = new List<AbsorbedGroupUnifyPreviewDto>();
        var foldEntries = new List<(int GroupId, string GroupName, PublicEntity Entry)>();
        int totalToMove = 0;

        foreach (var ae in targetEntries.Where(e => e.NeedsReview))
            warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{targetGroup.CanonicalName}» (الهدف) بانتظار المراجعة");

        foreach (var absorbedId in request.AbsorbedGroupIds.Distinct())
        {
            var absorbedGroup = await _entities.GetGroupAsync(absorbedId, ct)
                ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
            if (!absorbedGroup.IsActive)
                throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

            var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, ct);
            foreach (var ae in absorbedEntries.Where(e => e.NeedsReview))
                warnings.Add($"القيد «{ae.Governorate}/{ae.BranchName}» في «{absorbedGroup.CanonicalName}» بانتظار المراجعة");

            if (!string.Equals(absorbedGroup.EntityType, targetGroup.EntityType, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"تنبيه: نوع الجهة مختلف — «{absorbedGroup.CanonicalName}» ({absorbedGroup.EntityType}) و«{targetGroup.CanonicalName}» ({targetGroup.EntityType})");

            var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();

            foreach (var ae in activeAbsorbed)
            {
                var survivor = survivorPool.FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);
                if (survivor is not null)
                {
                    // الطي لا يُعدّ نقلًا: يُبطل القيد المطابق ويُرحّل روابطه إلى الناجي.
                    foldEntries.Add((absorbedGroup.Id, absorbedGroup.CanonicalName, ae));
                }
                else
                {
                    totalToMove++;
                    survivorPool.Add(ae);
                }
            }

            var govs = activeAbsorbed.Select(e => e.Governorate).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            absorbedDtos.Add(new AbsorbedGroupUnifyPreviewDto(absorbedGroup.Id, absorbedGroup.CanonicalName, activeAbsorbed.Count, govs));
        }

        // عدّ ملفات القيود المزمع طيّها دفعة واحدة ثم نبني تفاصيل الطي بالترتيب.
        var linkedCounts = await _entities.CountLinkedDocumentsByEntryIdsAsync(foldEntries.Select(f => f.Entry.Id).ToList(), ct);
        var folds = foldEntries.Select(f => new EntryFoldPreviewDto(
            f.GroupId,
            f.GroupName,
            f.Entry.Governorate,
            f.Entry.BranchName,
            linkedCounts.TryGetValue(f.Entry.Id, out var count) ? count : 0)).ToList();

        return new UnifyNamesPreviewResponse(targetGroup.CanonicalName, absorbedDtos, totalToMove, folds.Count, folds, warnings);
    }
    /// <inheritdoc/>
    public async Task<UnifyNamesResponse> UnifyNamesAsync(UnifyNamesRequest request, EntityRegistryActor actor, CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            var targetGroup = await _entities.GetGroupAsync(request.TargetGroupId, token)
                ?? throw new ArgumentException("الهوية الأم الهدف غير موجودة");
            if (!targetGroup.IsActive)
                throw new ArgumentException("الهوية الأم الهدف غير نشطة");

            if (request.AbsorbedGroupIds.Count == 0)
                throw new ArgumentException("حدد هوية أم واحدة على الأقل للتوحيد");

            if (request.AbsorbedGroupIds.Contains(request.TargetGroupId))
                throw new ArgumentException("لا يمكن توحيد هوية مع نفسها");

            var targetEntries = await _entities.ListEntriesByGroupAsync(targetGroup.Id, token);
            var activeTarget = targetEntries.Where(e => e.IsActive).ToList();
            if (targetEntries.Any(e => e.NeedsReview))
                throw new ArgumentException("يجب إتمام مراجعة جميع قيود الهوية الهدف قبل التوحيد");

            // بركة الناجين: تبدأ بنسخ القيود النشطة للهدف ويُلحق بها كل قيد يُنقل؛
            // مطابقة خام (Ordinal) على (المحافظة/الفرع) كسيمانتك الدمج والحرّاس —
            // فرق فراغ زائد يعني «نقلًا» لا «طيًّا» (سياسة المفتاح المعتمدة).
            var survivorPool = new List<PublicEntity>(activeTarget);

            // مرسوم التوحيد العام (اختياري)
            var decreeKind = NormalizeOptional(request.DecreeKind);
            var decreeNumber = NormalizeOptional(request.DecreeNumber);
            var decreeDate = FreeDateParser.Parse(request.DecreeDate, "تاريخ المرسوم");
            if (decreeKind is not null && decreeKind.Length > 100)
                throw new ArgumentException("نوع المرسوم أطول من 100 حرف");
            if (decreeNumber is not null && decreeNumber.Length > 100)
                throw new ArgumentException("رقم المرسوم أطول من 100 حرف");

            int groupsUnified = 0;
            int entriesMoved = 0;
            int entriesFolded = 0;
            int aliasesAdded = 0;
            int totalAffectedDocs = 0;
            var oldNames = new List<string>();
            var movedEntryIds = new List<int>();
            var affectedDocsById = new Dictionary<int, Document>();
            var entryTargetByAbsorbed = new Dictionary<int, int>();
            var folds = new List<object>();
            var absorbedIdsDistinct = request.AbsorbedGroupIds.Distinct().ToList();

            foreach (var absorbedId in absorbedIdsDistinct)
            {
                var absorbedGroup = await _entities.GetGroupAsync(absorbedId, token)
                    ?? throw new ArgumentException($"الهوية الأم #{absorbedId} غير موجودة");
                if (!absorbedGroup.IsActive)
                    throw new ArgumentException($"الهوية الأم «{absorbedGroup.CanonicalName}» غير نشطة");

                var absorbedEntries = await _entities.ListEntriesByGroupAsync(absorbedId, token);
                if (absorbedEntries.Any(e => e.NeedsReview))
                    throw new ArgumentException($"يجب إتمام مراجعة جميع قيود «{absorbedGroup.CanonicalName}» قبل التوحيد");

                var activeAbsorbed = absorbedEntries.Where(e => e.IsActive).ToList();

                oldNames.Add(absorbedGroup.CanonicalName);

                foreach (var ae in activeAbsorbed)
                {
                    var survivor = survivorPool.FirstOrDefault(se => se.Governorate == ae.Governorate && se.BranchName == ae.BranchName);

                    if (survivor is not null)
                    {
                        // طيّ: قيد مطابق (محافظة/فرع) حرفيًا داخل الهوية الموحّدة — يُبطل ويُرحّل
                        // روابطه وأسماءه البديلة إلى الناجي (سيمانتك دمج الفروع؛ بلا fallback من نوع «أول قيد»).
                        var linkedFoldDocs = await _entities.ListDocumentsLinkedToEntryAsync(ae.Id, token);
                        foreach (var doc in linkedFoldDocs)
                        {
                            if (!affectedDocsById.ContainsKey(doc.Id))
                                affectedDocsById[doc.Id] = doc;
                        }
                        foreach (var doc in linkedFoldDocs)
                            RepointEntryLinks(doc, ae.Id, survivor.Id);

                        ae.IsActive = false;
                        entryTargetByAbsorbed[ae.Id] = survivor.Id;
                        AddFoldAliases(survivor, absorbedGroup.CanonicalName, ae, ref aliasesAdded);
                        entriesFolded++;
                        folds.Add(new
                        {
                            absorbedEntryId = ae.Id,
                            targetEntryId = survivor.Id,
                            governorate = ae.Governorate,
                            branchName = ae.BranchName,
                            linkedDocs = linkedFoldDocs.Count,
                        });
                    }
                    else
                    {
                        // نقل القيد إلى مجموعة الهدف (كما هو)
                        ae.GroupId = targetGroup.Id;
                        survivorPool.Add(ae);
                        movedEntryIds.Add(ae.Id);
                        entriesMoved++;

                        // حفظ الاسم الممتصّ اسمًا بديلًا «للبحث فقط» على القيد المنقول
                        var normAbsorbed = ArabicNameNormalizer.Normalize(absorbedGroup.CanonicalName);
                        if (!ae.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normAbsorbed))
                        {
                            ae.Aliases.Add(new PublicEntityAlias
                            {
                                PublicEntityId = ae.Id,
                                AliasText = absorbedGroup.CanonicalName,
                            });
                            aliasesAdded++;
                        }
                    }
                }

                absorbedGroup.IsActive = false;
                groupsUnified++;
            }

            // 4) مزامنة النصوص في الملفات المرتبطة بالقيود المنقولة (كل اسم ممتصّ ← الاسم الموحّد)
            //    تُجدّد صور الأسماء القديمة لتصبح التسمية الموحدة فقط في كل الملفات والاستئنافات.
            var nameMatchedDocs = new Dictionary<int, Document>();
            foreach (var oldName in oldNames)
            {
                var matched = await SyncTextsAfterRenameAsync(oldName, targetGroup.CanonicalName, actor.Name, token);
                foreach (var doc in matched)
                    nameMatchedDocs[doc.Id] = doc;
            }

            // الملفات المربوطة عبر RegistryId بالقيود المنقولة — أعِد بناء نصوصها لتتقيد بالاسم الموحّد
            // إن لم تلتقطها المزامنة الاسمية (مثل مسمّاة بخلاف الاسم المعياري).
            foreach (var movedId in movedEntryIds)
            {
                var linkedDocs = await _entities.ListDocumentsLinkedToEntryAsync(movedId, token);
                foreach (var doc in linkedDocs)
                    affectedDocsById[doc.Id] = doc;
            }

            var affectedDocs = affectedDocsById.Values.ToList();
            if (affectedDocs.Count > 0)
                await SyncTextsAfterFoldAsync(affectedDocs, actor.Name, token);

            totalAffectedDocs = CountUniqueDocuments(nameMatchedDocs, affectedDocsById);

            // 5) مزامنة لقطات أطراف الاستئنافات عبر اتحاد الملفات المتأثرة (الاسمية + المترحلة)
            var allAffectedDocs = nameMatchedDocs.Values
                .Concat(affectedDocsById.Values)
                .GroupBy(d => d.Id)
                .Select(g => g.First())
                .ToList();
            if (allAffectedDocs.Count > 0)
                await SyncAppealsAfterEntityChangeAsync(allAffectedDocs, actor, token);

            // 6) ترحيل مندوبي الجهات الممتصة إلى الهدف (على مستوى المجموعة)، مع خريطة الطيّ:
            //    المطوي يرحل لناجيه، والمنقول (غير المُدرج) يبقى على قيده.
            var absorbedIdsSet = new HashSet<int>(absorbedIdsDistinct);
            await MigrateDelegatesAsync(absorbedIdsSet, targetGroup.Id, null, entryTargetByAbsorbed, token);

            // 7) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                targetGroupId = targetGroup.Id,
                targetGroup = targetGroup.CanonicalName,
                absorbedGroupIds = absorbedIdsDistinct,
                oldCanonicalNames = oldNames,
                entriesMoved,
                entriesFolded,
                folds,
                groupsUnified,
                aliasesAdded,
                totalAffectedDocs,
                decreeKind,
                decreeNumber,
                decreeDate = decreeDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            });

            var changeEvent = new PublicEntityChangeEvent
            {
                GroupId = targetGroup.Id,
                ActionKind = ActionKindCatalog.Unify,
                DecreeKind = decreeKind,
                DecreeNumber = decreeNumber,
                DecreeDate = decreeDate,
                PayloadJson = payload,
                ActorUserId = actor.UserId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await TrackChangeEventAsync(changeEvent, token);

            // 8) وقوعات آلية لكل ملف متأثر (نوع entity-change)
            var absorbedNamesJoined = string.Join('،', oldNames);
            foreach (var docId in allAffectedDocs.Select(d => d.Id))
            {
                var occurrence = new DocumentOccurrence
                {
                    DocumentId = docId,
                    Source = OccurrenceSourceCatalog.System,
                    OccurrenceType = OccurrenceTypeCatalog.EntityChange,
                    EventDate = DateTime.UtcNow,
                    CreatedById = actor.UserId,
                    Details = EntityChangeMessages.UnifyOccurrence(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                };
                await _occurrences.AddAsync(occurrence, token);
            }

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "unify_entity_names",
                documentId: null, documentType: null,
                details: $"توحيد تسمية {groupsUnified} هويات في «{targetGroup.CanonicalName}» — {entriesMoved} قيدًا نُقل، {entriesFolded} قيدًا طُوي، {totalAffectedDocs} ملفًا متأثرًا",
                ct: token);

            // تنبيه عام لكل المحامين + تنبيه خاص لرؤساء الأقسام
            await BroadcastEntityChangeToAllLawyersAsync(
                EntityChangeMessages.UnifyLawyersAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);
            await BroadcastToAllHeadsAsync(
                EntityChangeMessages.UnifyHeadsAlert(absorbedNamesJoined, targetGroup.CanonicalName, decreeKind ?? "", decreeNumber ?? "", decreeDate),
                actor.UserId, token);

            return new UnifyNamesResponse(targetGroup.Id, targetGroup.CanonicalName, groupsUnified, entriesMoved, entriesFolded, changeEvent.Id);
        }, ct);
    }
}
