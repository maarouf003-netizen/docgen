using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
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
