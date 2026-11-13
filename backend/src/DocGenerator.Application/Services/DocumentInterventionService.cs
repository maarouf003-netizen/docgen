using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// تنفيذ خدمة «المتدخلين». كل عملية ذرّية ضمن معاملة واحدة: تحفظ المتدخل/تعدّله/تحذفه،
/// وتزامن وقعة «طلب تدخل» المرتبطة به، وتعيد بناء `SearchText`، وتكتب سجل التدقيق.
/// لا تمسّ `Document.Version` (كالإجراءات): حقول النموذج العامة لا تتغير، فلا تُسبب
/// 409 لنموذج ملف مفتوح، ولا يمكن لـ`PUT` العام أن يمحو المتدخلين (جدول مستقل).
/// </summary>
public sealed class DocumentInterventionService : IDocumentInterventionService
{
    private readonly IDocumentRepository _documents;
    private readonly IRepository<PublicEntity> _publicEntities;
    private readonly IRepository<PublicEntityGroup> _publicEntityGroups;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;

    public DocumentInterventionService(
        IDocumentRepository documents,
        IRepository<PublicEntity> publicEntities,
        IRepository<PublicEntityGroup> publicEntityGroups,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit)
    {
        _documents = documents;
        _publicEntities = publicEntities;
        _publicEntityGroups = publicEntityGroups;
        _uow = uow;
        _tx = tx;
        _audit = audit;
    }

    public async Task<DocumentInterventionDto> AddAsync(
        int documentId,
        DocumentInterventionUpsertRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        return await _tx.RunAsync(async token =>
        {
            // القفل داخل المعاملة يمنع سباق العدّ/الإضافة (طلبَان يقرآن 49 ثم يضيفان معًا).
            var doc = await _documents.GetByIdForUpdateAsync(documentId, token)
                ?? throw new KeyNotFoundException();
            EnsureSupportedSide(doc);
            if (doc.Interventions.Count >= InterventionLimitsCatalog.MaxPerDocument)
                throw new ArgumentException(
                    $"عدد المتدخلين في الملف يتجاوز الحد الأقصى ({InterventionLimitsCatalog.MaxPerDocument})");

            var intervention = DocumentInterventionText.Normalize(request);
            await ResolveRegistryAsync(intervention, existingRegistryId: null, token);
            intervention.DocumentId = documentId;
            intervention.CreatedById = userId;
            intervention.CreatedAt = DateTime.UtcNow;
            intervention.UpdatedAt = DateTime.UtcNow;

            doc.Interventions.Add(intervention);
            doc.Occurrences.Add(
                DocumentInterventionText.BuildOccurrence(intervention, documentId, userId));
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "intervention", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"أضاف متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), token);
            return DocumentInterventionText.ToDto(intervention);
        }, ct);
    }

    public async Task<DocumentInterventionDto?> UpdateAsync(
        int documentId,
        int interventionId,
        DocumentInterventionUpsertRequest request,
        int userId,
        string? actorName,
        CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException();
        EnsureSupportedSide(doc);

        var intervention = doc.Interventions.FirstOrDefault(i => i.Id == interventionId);
        if (intervention is null)
            return null;

        var normalized = DocumentInterventionText.Normalize(request);
        await ResolveRegistryAsync(normalized, intervention.RegistryId, ct);

        return await _tx.RunAsync(async token =>
        {
            DocumentInterventionText.Apply(intervention, normalized);

            var occurrence = doc.Occurrences.FirstOrDefault(o => o.DocumentInterventionId == intervention.Id);
            if (occurrence is null)
                doc.Occurrences.Add(
                    DocumentInterventionText.BuildOccurrence(intervention, documentId, userId));
            else
                DocumentInterventionText.ApplyOccurrence(occurrence, intervention);

            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "intervention", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"عدّل متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), token);
            return DocumentInterventionText.ToDto(intervention);
        }, ct);
    }

    public async Task<bool> DeleteAsync(
        int documentId,
        int interventionId,
        string? actorName,
        CancellationToken ct = default)
    {
        var doc = await _documents.GetByIdAsync(documentId, ct)
            ?? throw new KeyNotFoundException();
        EnsureSupportedSide(doc);

        var intervention = doc.Interventions.FirstOrDefault(i => i.Id == interventionId);
        if (intervention is null)
            return false;

        return await _tx.RunAsync(async token =>
        {
            // حذف صريح للوقعة (لا الاعتماد على حذف القاعدة) لتوحيد السلوك بين المزوّدين.
            var occurrence = doc.Occurrences.FirstOrDefault(o => o.DocumentInterventionId == intervention.Id);
            if (occurrence is not null)
                doc.Occurrences.Remove(occurrence);
            doc.Interventions.Remove(intervention);
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.UpdatedAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "intervention", documentId, doc.DocumentType,
                DocumentText.AuditWithActor(
                    $"حذف متدخلًا: {DocumentInterventionText.DisplayName(intervention)}", doc), token);
            return true;
        }, ct);
    }

    /// <summary>
    /// صفتا الملف المدعومتان هما «طالبة التنفيذ» و«منفذ عليها»؛ «عرض وايداع» مستثناة
    /// بقرار المالك (الجهة فيها عارضة للمبلغ لا خصمًا في التدخل).
    /// </summary>
    private static void EnsureSupportedSide(Document doc)
    {
        if (doc.GeneralEntitySide == GeneralEntitySideCatalog.Deposit)
            throw new ArgumentException("لا يمكن إضافة متدخل في ملف صفة «عرض وايداع»");
    }

    /// <summary>
    /// للجهة العامة: يتحقق من قيد السجل المرجعي وجهته الأم، ويعتمد اسم الجهة المعتمد
    /// وفرعها ومحافظتها من السجل لا من نص العميل — فيبقى الربط صحيحًا مهما عبث الطلب.
    /// عند التعديل يُقبل القيد نفسه وإن كان قد عُطّل لاحقًا (حفاظًا على قابلية تعديل
    /// بيانات تاريخية مرتبطة بجهة عُطّلت)، أما القيد الجديد/المختلف فيجب أن يكون نشطًا.
    /// </summary>
    private async Task ResolveRegistryAsync(
        DocumentIntervention intervention,
        int? existingRegistryId,
        CancellationToken ct)
    {
        if (intervention.Nature != PartyNatureCatalog.PublicEntity)
            return;
        if (intervention.RegistryId is null)
            throw new ArgumentException("اختر جهة عامة صالحة من السجل المرجعي");

        var registry = await _publicEntities.GetByIdAsync(intervention.RegistryId.Value, ct);
        var group = registry is null
            ? null
            : await _publicEntityGroups.GetByIdAsync(registry.GroupId, ct);
        var keepExisting = existingRegistryId is not null
            && intervention.RegistryId == existingRegistryId;
        if (registry is null
            || group is null
            || (!registry.IsActive && !keepExisting)
            || (!group.IsActive && !keepExisting))
            throw new ArgumentException("اختر جهة عامة صالحة من السجل المرجعي");

        intervention.EntityName = group.CanonicalName;
        intervention.EntityBranch = registry.BranchName;
        intervention.Governorate = registry.Governorate;
    }
}
