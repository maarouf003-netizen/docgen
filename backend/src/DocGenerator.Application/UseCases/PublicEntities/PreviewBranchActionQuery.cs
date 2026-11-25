using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.PublicEntities;

/// <summary>معاينة موحدة لأي عملية فرع (تعديل تسمية/دمج/إلغاء/توحيد) — قراءة صرفة.</summary>
public sealed record PreviewBranchActionQuery(
    int GroupId,
    PreviewBranchActionRequest Request,
    EntityRegistryActor Actor) : IQuery<BranchActionPreviewResponse>;

internal sealed class PreviewBranchActionQueryHandler
    : QueryHandlerBase<PreviewBranchActionQuery, BranchActionPreviewResponse>
{
    private readonly IPublicEntityWriteRules _rules;
    private readonly IPublicEntityRepository _entities;

    public PreviewBranchActionQueryHandler(
        IPublicEntityWriteRules rules,
        IPublicEntityRepository entities,
        IEnumerable<IPipelineBehavior<PreviewBranchActionQuery, BranchActionPreviewResponse>> behaviors)
        : base(behaviors)
    {
        _rules = rules;
        _entities = entities;
    }

    protected override async Task<BranchActionPreviewResponse> HandleCoreAsync(
        PreviewBranchActionQuery query, CancellationToken ct)
    {
        var groupId = query.GroupId;
        var request = query.Request;
        var actor = query.Actor;
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
                        _rules.GuardNotParentEntry(entry);
                        _rules.GuardHeadCannotEditParent(actor, entry);
                        await _rules.EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
                        var newBranch = _rules.Required(request.NewBranchName, "اسم الفرع مطلوب", 200);
                        if (string.Equals(newBranch, entry.BranchName, StringComparison.Ordinal))
                            errors.Add("الاسم الجديد مطابق للاسم الحالي");
                        else
                            await _rules.EnsureNoDuplicateEntryAsync(entry.Id, entry.Group.CanonicalName, entry.Governorate, newBranch, ct);
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
                        await PublicEntityService.ValidateMergeablePairAsync(_rules, groupId, source, target, request.TargetId.Value, actor, ct, errors, warnings);
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
                        _rules.GuardNotParentEntry(entry);
                        _rules.GuardHeadCannotEditParent(actor, entry);
                        await _rules.EnsureHeadScopeAsync(actor, entry, entry.Governorate, ct);
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
                            await PublicEntityService.ValidateMergeablePairAsync(_rules, groupId, entry, target, request.TargetId!.Value, actor, ct, errors, warnings);
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
                        _rules.GuardNotParentEntry(target);
                        _rules.GuardHeadCannotEditParent(actor, target);
                        await _rules.EnsureHeadScopeAsync(actor, target, target.Governorate, ct);
                        var absorbedDocIds = new Dictionary<int, Document>();
                        foreach (var absorbedId in absorbed)
                        {
                            var ae = await _entities.GetEntryWithDetailsAsync(absorbedId, ct);
                            await PublicEntityService.ValidateMergeablePairAsync(_rules, groupId, ae, target, target.Id, actor, ct, errors, warnings);
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
                                var corrected = _rules.Required(request.CorrectedName, "اسم الفرع مطلوب", 200);
                                try
                                {
                                    await _rules.EnsureNoDuplicateEntryAsync(target.Id, target.Group.CanonicalName, target.Governorate, corrected, ct);
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
}
