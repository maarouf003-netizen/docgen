using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>الحلول: إلغاء عدة هويات أم واستبدالها بهوية أم جديدة بمرسوم إلزامي (المدير/المشرف).</summary>
public sealed record AbolishAndReplacePublicEntityGroupCommand(
    AbolishAndReplaceRequest Request,
    EntityRegistryActor Actor) : ICommand<AbolishAndReplaceResponse>;

internal sealed class AbolishAndReplacePublicEntityGroupCommandHandler
    : CommandHandlerBase<AbolishAndReplacePublicEntityGroupCommand, AbolishAndReplaceResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public AbolishAndReplacePublicEntityGroupCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<AbolishAndReplacePublicEntityGroupCommand, AbolishAndReplaceResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _occurrences = occurrences;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<AbolishAndReplaceResponse> HandleCoreAsync(
        AbolishAndReplacePublicEntityGroupCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (!RolePermissions.HasFullAccess(actor.Role))
            throw new UnauthorizedAccessException("الحلول (إلغاء واستبدال) للمدير أو المشرف فقط");
        if (request.AbolishedGroupIds is null || request.AbolishedGroupIds.Count == 0)
            throw new ArgumentException("حدد هوية أم واحدة على الأقل للإلغاء");

        var newCanonical = _rules.Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var entityType = _rules.ValidEntityType(request.EntityType);
        var governorate = _rules.Required(request.Governorate, "المحافظة مطلوبة", 100);
        var citationFormula = _rules.ValidCitationFormula(request.CitationFormula, CitationFormulaCatalog.AddToJob);
        var coverageLabel = _rules.ValidateCoverageLabel(request.CoverageLabel);
        var decreeKind = _rules.Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = _rules.Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
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
            await _rules.EnsureCanonicalAvailableAsync(newCanonical, 0, token);

            // 2) إنشاء الهوية الأم الجديدة + قيدها الأم
            var newGroup = new PublicEntityGroup
            {
                CanonicalName = newCanonical,
                EntityType = entityType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };
            _rules.SetGroupNorm(newGroup);
            await _entities.AddGroupAsync(newGroup, token);
            await _uow.SaveChangesAsync(token);

            var newParentEntry = new PublicEntity
            {
                GroupId = newGroup.Id,
                Group = newGroup,
                Governorate = governorate,
                BranchName = PublicEntityBranchCatalog.ParentBranchName,
                IsParentEntity = true,
                CoverageLabel = coverageLabel,
                CitationFormula = citationFormula,
                Status = EntityStatusCatalog.Final,
                IsActive = true,
                NeedsReview = false,
                CreatedById = actor.UserId,
                CreatedAt = DateTime.UtcNow,
            };
            var aliases = _rules.CleanAliases(request.Aliases, ArabicNameNormalizer.Normalize(newCanonical));
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
                await _rules.SyncTextsAfterFoldAsync(affectedDocsList, actor.Name, token);

                // مزامنة لقطات أطراف الاستئنافات للملفات المتأثرة (تُطابق صور الجهة العامة
                // عبر (Kind, PartyId) فيلتقط حتى الصور المخزَّنة باسم مختلف عن الاسم المعياري).
                await _rules.SyncAppealsAfterEntityChangeAsync(affectedDocsList, actor, token);
            }
            await _uow.SaveChangesAsync(token);

            // 5) ترحيل مندوبي الجهات المُلغاة إلى الهوية الجديدة (مواءمة 7-ز)
            var delegatesCount = await _rules.MigrateDelegatesAsync(abolishedIdsSet, newGroup.Id, newParentEntry.Id, null, token);

            // 6) سجل التغيير
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                abolishedGroupIds = abolishedIds,
                oldCanonicalNames = abolishedNames,
                newCanonical = newGroup.CanonicalName,
                entityType = newGroup.EntityType,
                governorate,
                branchName = PublicEntityBranchCatalog.ParentBranchName,
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
            await _rules.TrackChangeEventAsync(changeEvent, token);
            await _uow.SaveChangesAsync(token);

            // 8) تنبيه عام لكل المحامين + رؤساء الأقسام
            var abolishedNamesJoined = string.Join('،', abolishedNames);
            var entityGroupKey = newGroup.Id.ToString(CultureInfo.InvariantCulture);
            await _rules.BroadcastEntityChangeToAllLawyersAsync(
                HeadAlertKindCatalog.EntityAbolishLawyers,
                HeadAlertMessages.EntityAbolishLawyers(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                ActionKindCatalog.Abolish, entityGroupKey, actor.UserId, token);
            await _rules.BroadcastToAllHeadsAsync(
                HeadAlertKindCatalog.EntityAbolishHeads,
                HeadAlertMessages.EntityAbolishHeads(newGroup.CanonicalName, abolishedNamesJoined, decreeKind, decreeNumber, decreeDate),
                ActionKindCatalog.Abolish, entityGroupKey, actor.UserId, token);

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
}
