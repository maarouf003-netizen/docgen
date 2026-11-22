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
    /// <summary>
    /// إبلاغ المُدخِل المحامي بتغيير تسمية جهته أثناء المراجعة:
    /// «تم تعديل اسم الجهة التي أدخلتها من “القديم” إلى “الجديد”».
    /// </summary>
    private async Task InsertRenameNoticeToCreatorAsync(PublicEntity entry, string oldName, string newName, CancellationToken token)
    {
        var creator = entry.CreatedBy;
        if (creator is null || creator.BranchId is null)
            return;

        var audience = creator.Role switch
        {
            UserRole.Lawyer => HeadAlertAudience.SingleLawyer,
            UserRole.Head or UserRole.SubHead => HeadAlertAudience.SingleHead,
            _ => (HeadAlertAudience?)null,
        };
        if (audience is null)
            return;

        await _alertOutbox.EnqueueSystemAsync(
            new CreateSystemHeadAlertRequest(
                HeadAlertKindCatalog.EntityRenameNoticeToCreator,
                HeadAlertMessages.EntityRenameNoticeToCreator(new(oldName, newName)),
                audience.Value,
                creator.BranchId.Value,
                entry.ReviewedById ?? creator.Id,
                PublicEntityId: entry.Id,
                RecipientUserId: creator.Id,
                EntityOperation: ActionKindCatalog.Rename),
            supersedePlan: null,
            occurredAt: DateTime.UtcNow,
            ct: token);
    }
    /// <summary>
    /// نقطة الخنق الوحيدة لكتابة أحداث سجل التغييرات: ترفض صنفًا خارج الكتالوج
    /// (`ActionKindCatalog.IsValid`) قبل التتبّع — فالصنف المجهول لا يُخزَّن أصلًا
    /// (اتساقًا مع كل كتالوجات الكتابة الأخرى)، وما يُقرأ لاحقًا مضمون الصنف.
    /// </summary>
    private async Task TrackChangeEventAsync(PublicEntityChangeEvent changeEvent, CancellationToken token)
    {
        if (!ActionKindCatalog.IsValid(changeEvent.ActionKind))
            throw new ArgumentException("صنف حدث التغيير غير صالح");
        await _changeEvents.AddAsync(changeEvent, token);
    }
    // ── مزامنة الأعمدة النصية عند إعادة التسمية (شرط ثابت د5) ──

    /// <summary>
    /// يُحدّث صفوف الطرفين المطابقة للاسم القديم (بعد التطبيع) إلى الاسم المعتمد الجديد،
    /// ويُعيد بناء نص طالب التنفيذ ونص البحث لكل ملف متأثر، ثم يُدوّن قبل/بعد كل ملف
    /// في سجل تعديلات الحقول. تعمل داخل معاملة المتصل وتعيد الملفات المتأثرة كأشياء كاملة
    /// (لتمكين مزامنة لقطات الاستئنافات من نفس المجموعة بعد تحرير أسماء صفوفها).
    /// </summary>
    private async Task<List<Document>> SyncTextsAfterRenameAsync(string oldCanonical, string newCanonical, string? actorName, CancellationToken token)
    {
        var oldNorm = ArabicNameNormalizer.Normalize(oldCanonical);
        var newNorm = ArabicNameNormalizer.Normalize(newCanonical);
        if (oldNorm.Length == 0 || oldNorm == newNorm)
            return new List<Document>();

        var logs = new Dictionary<int, List<DocumentFieldChange>>();
        var affectedDocs = new Dictionary<int, Document>();
        void AddLog(int documentId, string fieldKey, string fieldLabel, string? oldValue, string? newValue)
        {
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                return;
            if (!logs.TryGetValue(documentId, out var list))
                logs[documentId] = list = new List<DocumentFieldChange>();
            list.Add(new DocumentFieldChange
            {
                DocumentId = documentId,
                FieldKey = fieldKey,
                FieldLabel = fieldLabel,
                OldValue = Clamp(oldValue),
                NewValue = Clamp(newValue),
            });
        }

        var applicantNames = (await _entities.ListDistinctApplicantTextsAsync(token))
            .Select(t => t.Name)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (applicantNames.Count > 0)
        {
            var rows = await _entities.ListApplicantRowsByNamesAsync(applicantNames, token);
            var docs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First()).ToList();
            var oldTexts = docs.ToDictionary(d => d.Id, d => d.Applicant);

            foreach (var row in rows)
                row.Name = newCanonical;
            foreach (var doc in docs)
            {
                var rebuilt = ApplicantTextBuilder.Build(doc.ApplicantPublicEntities);
                if (!string.IsNullOrWhiteSpace(rebuilt) || string.IsNullOrWhiteSpace(doc.Applicant))
                    doc.Applicant = rebuilt;
                var interventionsChanged = SyncPublicInterventionNames(doc, oldNorm, newCanonical);
                if (interventionsChanged > 0)
                    AddLog(doc.Id, "__Col_Interventions", "المتدخلون", oldCanonical, newCanonical);
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
                affectedDocs[doc.Id] = doc;
                AddLog(doc.Id, nameof(Document.Applicant), "طالب التنفيذ",
                    oldTexts.GetValueOrDefault(doc.Id), doc.Applicant);
            }
        }

        var executedNames = (await _entities.ListDistinctExecutedTextsAsync(token))
            .Select(t => t.EntityName)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (executedNames.Count > 0)
        {
            var rows = await _entities.ListExecutedRowsByNamesAsync(executedNames, token);
            foreach (var row in rows)
            {
                var oldSignature = JoinNameBranch(row.EntityName, row.EntityBranch);
                row.EntityName = newCanonical;
                AddLog(row.DocumentId, "__Col_ExecutedPublicEntities", "الجهات العامة المنفذ عليها",
                    oldSignature, JoinNameBranch(row.EntityName, row.EntityBranch));
            }

            // إعادة بناء نص البحث مرة واحدة لكل ملف متأثر (لا لكل صف مطابق).
            var executedDocs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First());
            foreach (var doc in executedDocs)
            {
                affectedDocs[doc.Id] = doc;
                var interventionsChanged = SyncPublicInterventionNames(doc, oldNorm, newCanonical);
                if (interventionsChanged > 0)
                    AddLog(doc.Id, "__Col_Interventions", "المتدخلون", oldCanonical, newCanonical);
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            }
        }

        // ملفات تحمل متدخل جهة عامة فقط (بلا صف طرف مطابق في الجدولين) لا تلتقطها
        // استعلامات الأطراف أعلاه؛ يُزامَن اسمها المجمّد ويُعاد بناء بحثها هنا.
        var interventionDocs = await _entities.ListDocumentsByPublicInterventionNameAsync(oldCanonical, token);
        foreach (var doc in interventionDocs)
        {
            var changed = SyncPublicInterventionNames(doc, oldNorm, newCanonical);
            if (changed == 0)
                continue;
            AddLog(doc.Id, "__Col_Interventions", "المتدخلون", oldCanonical, newCanonical);
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            affectedDocs[doc.Id] = doc;
        }

        await _uow.SaveChangesAsync(token);
        var details = $"مزامنة إعادة تسمية الجهة: «{oldCanonical}» ← «{newCanonical}»";
        foreach (var (documentId, changes) in logs)
            await _audit.LogDocumentChangeAsync(actorName, "rename_public_entity_sync",
                documentId, documentType: null, details, changes, token);
        return affectedDocs.Values.ToList();
    }
    // ── مساعدات خاصة ──


    /// <summary>مزامنة نصوص الملف بعد الطيّ ( Collector for applicant+executed).</summary>
    private async Task SyncTextsAfterFoldAsync(List<Document> linkedDocs, string? actorName, CancellationToken token)
    {
        if (linkedDocs.Count == 0) return;

        foreach (var doc in linkedDocs)
        {
            var rebuilt = ApplicantTextBuilder.Build(doc.ApplicantPublicEntities);
            if (!string.IsNullOrWhiteSpace(rebuilt) || string.IsNullOrWhiteSpace(doc.Applicant))
                doc.Applicant = rebuilt;
            // ملف «منفذ عليه»/«عرض وايداع» بلا جهة طالبة كلاسية: اسم الطالب يُشتق من
            // طلبات التنفيذ الاعتباريين المربوطين جهة عامة وأسماء طلبات العرض الطبيعية
            // فيتطابق العنوان مع الاسم المعياري بعد الطيّ/الدمج/الحلول.
            if (GeneralEntitySideCatalog.IsExecutedLike(doc.GeneralEntitySide)
                && doc.ApplicantPublicEntities.Count == 0)
            {
                var executedApplicantName = doc.ExecutionApplicants
                    .Select(a => (a.Name ?? string.Empty).Trim())
                    .FirstOrDefault(v => v.Length > 0);
                doc.Applicant = executedApplicantName ?? doc.Applicant;
            }
            doc.SearchText = DocumentSearchTextBuilder.Build(doc);
            doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
        }
        await _uow.SaveChangesAsync(token);
    }
    /// <summary>
    /// مزامنة لقطات أطراف الاستئنافات (AppellantsJson / AppelleesJson) بعد تغيير جهة عامة
    /// (إعادة تسمية / دمج / حلول) على مجموعة من الملفات المتأثرة. تُطابق صور الجهة العامة
    /// (طالب أو منفذ عليه) داخل اللقطات عبر (Kind, PartyId) ومعرّف صف الوصلة بالملف — لا عبر
    /// الاسم حصرًا — فتلتقط حتى الصور المخزَّنة بأسامٍ تختلف عن الاسم المعياري (الثغرة)،
    /// وتُجدّد اسم الصورة من الاسم الحالي للصف المرتَّل. تعمل داخل معاملة المتصل وتُدوّن
    /// التغيير عبر AuditLogger لكل استئناف.
    /// </summary>
    private async Task SyncAppealsAfterEntityChangeAsync(
        IReadOnlyCollection<Document> affectedDocs,
        EntityRegistryActor actor,
        CancellationToken token)
    {
        if (affectedDocs.Count == 0)
            return;

        // خريطة (Kind, PartyId) → الاسم الحالي لصف الوصلة، من الملفات المتأثرة
        // (حُرِّرت أسماءها قبلاً بمزامنة النصوص أو بالتحديث المباشر عند الحلول).
        var newNames = new Dictionary<(string Kind, int PartyId), string>();
        foreach (var doc in affectedDocs)
        {
            foreach (var a in doc.ApplicantPublicEntities)
                if (!string.IsNullOrWhiteSpace(a.Name))
                    newNames[(AppealSnapshotSerializer.KindApplicantEntity, a.Id)] = a.Name;
            foreach (var e in doc.ExecutedPublicEntities)
                if (!string.IsNullOrWhiteSpace(e.EntityName))
                    newNames[(AppealSnapshotSerializer.KindExecutedPublic, e.Id)] = e.EntityName;
        }
        if (newNames.Count == 0)
            return;

        var documentIds = affectedDocs.Select(d => d.Id).Distinct().ToList();
        var appeals = await _appeals.ListByDocumentIdsAsync(documentIds, token);
        if (appeals.Count == 0)
            return;

        foreach (var appeal in appeals)
        {
            // القرار المعتمد: لقطات الاستئناف المنظورة فقط تتغير مع تغيير الجهة؛
            // المحسوم/المشطوب يبقى لقطة تاريخية لا تُمس (دليل قانوني).
            if (appeal.Status != AppealStatusCatalog.Pending)
                continue;

            // اللقطة التالفة تُتخطى حمايةً للمعاملة — وتُدوَّن واقعة تخطٍّ بدل الصمت، فبقاء الاسم
            // القديم في اللقطة بعد إعادة تسمية ناجحة يُقرأ خطأً كفشل العملية.
            if (AppealSnapshotSerializer.IsCorruptedSnapshot(appeal.AppellantsJson)
                || AppealSnapshotSerializer.IsCorruptedSnapshot(appeal.AppelleesJson))
            {
                await _audit.LogAsync(actor.Name, "appeal_entity_sync_skipped",
                    documentId: appeal.DocumentId, documentType: null,
                    details: $"تُخطيت مزامنة لقطات الاستئناف (رقم {appeal.Id}) بعد تغيير جهة عامة في الملف #{appeal.DocumentId} — لقطة أطراف تالفة",
                    ct: token);
                continue;
            }

            var newAppellants = AppealSnapshotSerializer.UpdateEntityParties(appeal.AppellantsJson, newNames);
            var newAppellees = AppealSnapshotSerializer.UpdateEntityParties(appeal.AppelleesJson, newNames);
            var changed = !string.Equals(newAppellants, appeal.AppellantsJson, StringComparison.Ordinal)
                          || !string.Equals(newAppellees, appeal.AppelleesJson, StringComparison.Ordinal);
            if (!changed)
                continue;

            appeal.AppellantsJson = newAppellants;
            appeal.AppelleesJson = newAppellees;
            appeal.UpdatedAt = DateTime.UtcNow;
            await _audit.LogAsync(actor.Name, "appeal_entity_sync",
                documentId: appeal.DocumentId, documentType: null,
                details: $"مزامنة لقطات الاستئناف بعد تغيير جهة عامة في الملف #{appeal.DocumentId}",
                ct: token);
        }

        await _uow.SaveChangesAsync(token);
    }
    /// <summary>ترحيل مندوبي الجهات المُمتصة/المُلغاة إلى الهوية الهدف (مواءمة 7-ز).</summary>
    /// <remarks>
    /// المندوب المجموعتي يُتوجَّه دائمًا إلى المجموعة الهدف. المندوب القيدي يُتوجَّه إلى
    /// القيد المطابق لفرعه عبر <paramref name="entryTargetByAbsorbedEntry"/>، ويسقط على
    /// <paramref name="defaultTargetEntryId"/> عند غياب المطابق (حيث لا تُمرَّر الخريطة).
    /// في مسار التوحيد تُمرَّر خريطة الطيّ <paramref name="entryTargetByAbsorbedEntry"/>
    /// ليرحل المطوي إلى قرينه الناجي، بينما المُتَنقَّل (غير المُدرج في الخريطة) يبقى على
    /// قيده دون تغيير، مع تمرير null لقيمة <paramref name="defaultTargetEntryId"/> كي لا
    /// يُطوى المنقول على قيد عشوائي.
    /// </remarks>
    private async Task<int> MigrateDelegatesAsync(
        HashSet<int> absorbedIds,
        int targetGroupId,
        int? defaultTargetEntryId,
        IReadOnlyDictionary<int, int>? entryTargetByAbsorbedEntry,
        CancellationToken token)
    {
        var delegates = await _users.ListEntityManagersByGroupIdsAsync(absorbedIds, token);
        foreach (var delegateUser in delegates)
        {
            if (delegateUser.PortalGroupId.HasValue && absorbedIds.Contains(delegateUser.PortalGroupId.Value))
                delegateUser.PortalGroupId = targetGroupId;
            if (delegateUser.PortalEntryId.HasValue
                && delegateUser.PortalEntry is not null
                && absorbedIds.Contains(delegateUser.PortalEntry.GroupId))
            {
                delegateUser.PortalGroupId = targetGroupId;
                var branchTarget = entryTargetByAbsorbedEntry is not null
                    && entryTargetByAbsorbedEntry.TryGetValue(delegateUser.PortalEntryId.Value, out var matched)
                        ? matched
                        : defaultTargetEntryId;
                if (branchTarget.HasValue)
                    delegateUser.PortalEntryId = branchTarget.Value;
            }
        }
        return delegates.Count;
    }
    // ── مساعدات البث العام لكل الفروع (أ1) ──

    /// <summary>تعميم تغيير جهة على محامي كل فرع نشط — صف Outbox واحد لكل فرع.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastEntityChangeToAllLawyersAsync(
        string kind,
        string message,
        string entityOperation,
        string entityGroupKey,
        int actorUserId,
        CancellationToken token)
    {
        var occurredAt = DateTime.UtcNow;
        var grouped = await _headAlerts.ListAllActiveLawyersGroupedByBranchAsync(token);
        foreach (var (branchId, lawyers) in grouped)
        {
            if (lawyers.Count == 0)
                continue;
            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    kind,
                    message,
                    HeadAlertAudience.BranchLawyers,
                    branchId,
                    actorUserId,
                    EntityOperation: entityOperation,
                    EntityGroupKey: entityGroupKey),
                supersedePlan: null,
                occurredAt: occurredAt,
                ct: token);
        }
    }
    /// <summary>تعميم تغيير جهة على رؤساء كل فرع نشط — صف Outbox واحد لكل فرع.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastToAllHeadsAsync(
        string kind,
        string message,
        string entityOperation,
        string entityGroupKey,
        int actorUserId,
        CancellationToken token)
    {
        var occurredAt = DateTime.UtcNow;
        var grouped = await _headAlerts.ListAllActiveHeadsGroupedByBranchAsync(token);
        foreach (var (branchId, heads) in grouped)
        {
            if (heads.Count == 0)
                continue;
            await _alertOutbox.EnqueueSystemAsync(
                new CreateSystemHeadAlertRequest(
                    kind,
                    message,
                    HeadAlertAudience.BranchHeads,
                    branchId,
                    actorUserId,
                    EntityOperation: entityOperation,
                    EntityGroupKey: entityGroupKey),
                supersedePlan: null,
                occurredAt: occurredAt,
                ct: token);
        }
    }
    // ── مساعدات مشتركة ──

}
