using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>إعادة تسمية هوية أم واحدة على مستوى المجموعة بمرسوم إلزامي (المدير/المشرف).</summary>
public sealed record RenamePublicEntityGroupCommand(
    RenameGroupRequest Request,
    EntityRegistryActor Actor) : ICommand<RenameGroupResponse>;

internal sealed class RenamePublicEntityGroupCommandHandler
    : CommandHandlerBase<RenamePublicEntityGroupCommand, RenameGroupResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public RenamePublicEntityGroupCommandHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IRepository<DocumentOccurrence> occurrences,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<RenamePublicEntityGroupCommand, RenameGroupResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
        _occurrences = occurrences;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    protected override async Task<RenameGroupResponse> HandleCoreAsync(
        RenamePublicEntityGroupCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var actor = command.Actor;
        if (!RolePermissions.HasFullAccess(actor.Role))
            throw new UnauthorizedAccessException("إعادة التسمية للمدير أو المشرف فقط");

        var newCanonical = _rules.Required(request.NewCanonicalName, "اسم الجهة مطلوب", 200);
        var decreeKind = _rules.Required(request.DecreeKind, "نوع المرجع مطلوب", 100);
        var decreeNumber = _rules.Required(request.DecreeNumber, "رقم المرجع مطلوب", 100);
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

            await _rules.EnsureCanonicalAvailableAsync(newCanonical, group.Id, token);

            group.CanonicalName = newCanonical;
            _rules.SetGroupNorm(group);

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
            var affectedDocs = await _rules.SyncTextsAfterRenameAsync(oldCanonical, newCanonical, actor.Name, token);
            var affected = affectedDocs.Count;

            // مزامنة لقطات أطراف الاستئنافات المرتبطة بنفس الملفات المتأثرة
            await _rules.SyncAppealsAfterEntityChangeAsync(affectedDocs, actor, token);

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
            await _rules.TrackChangeEventAsync(changeEvent, token);

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
            var entityGroupKey = group.Id.ToString(CultureInfo.InvariantCulture);
            await _rules.BroadcastEntityChangeToAllLawyersAsync(
                HeadAlertKindCatalog.EntityRenameLawyers,
                HeadAlertMessages.EntityRenameLawyers(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                ActionKindCatalog.Rename, entityGroupKey, actor.UserId, token);
            await _rules.BroadcastToAllHeadsAsync(
                HeadAlertKindCatalog.EntityRenameHeads,
                HeadAlertMessages.EntityRenameHeads(oldCanonical, group.CanonicalName, decreeKind, decreeNumber, decreeDate),
                ActionKindCatalog.Rename, entityGroupKey, actor.UserId, token);

            await _uow.SaveChangesAsync(token);

            await _audit.LogAsync(actor.Name, "rename_public_entity_group",
                documentId: null, documentType: null,
                details: $"أعاد تسمية الهوية الأم: «{oldCanonical}» إلى «{group.CanonicalName}» بموجب {PublicEntityService.BuildDecreeSuffix(decreeKind, decreeNumber, decreeDate)} — {affected} ملفًا متأثرًا",
                ct: token);

            return new RenameGroupResponse(group.Id, oldCanonical, group.CanonicalName, affected, changeEvent.Id);
        }, ct);
    }
}
