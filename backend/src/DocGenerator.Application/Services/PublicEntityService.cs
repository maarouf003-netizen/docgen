using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// خدمة السجل المرجعي للجهات العامة (نموذج الحوكمة الجديد): أي جهة يُدخلها
/// محامٍ تُخزَّن بـ Status=Final لكنها تبقى «بحاجة مراجعة» (NeedsReview=true) فلا
/// تظهر لبوات المندوبين قبل اعتماد/تعديل رئيس قسمها (المواءمة السلوكية §6bis)؛
/// الاعتماد يقفل المراجعة بصمت، والتعديل — وتغيير التسمية تحديدًا — يبلّغ
/// المُدخِل بالاسم القديم والجديد. الإدارة تعدّل كل السجل بتنفيذ فوري.
/// إعادة التسمية الجماعية تزامن الأعمدة النصية ضمن معاملة واحدة (د5)، وأداة
/// الاستيراد التاريخي تعتمد نهائيًا مباشرة (د12).
/// </summary>
public sealed partial class PublicEntityService : IPublicEntityService
{
    /// <summary>الاسم الافتراضي لفرع قيد الجهة الأم — من الكتالوج لا من حرفي مكرر.</summary>
    private const string DefaultBranchName = PublicEntityBranchCatalog.ParentBranchName;

    private readonly IPublicEntityRepository _entities;
    private readonly IRepository<Branch> _branches;
    private readonly IHeadAlertRepository _headAlerts;
    private readonly IRepository<PublicEntityChangeEvent> _changeEvents;
    private readonly IRepository<DocumentOccurrence> _occurrences;
    private readonly IRepository<ParentEditSuggestion> _suggestions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IUserRepository _users;
    private readonly IAppealRepository _appeals;

    public PublicEntityService(
        IPublicEntityRepository entities,
        IRepository<Branch> branches,
        IHeadAlertRepository headAlerts,
        IRepository<PublicEntityChangeEvent> changeEvents,
        IRepository<DocumentOccurrence> occurrences,
        IRepository<ParentEditSuggestion> suggestions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IUserRepository users,
        IAppealRepository appeals)
    {
        _entities = entities;
        _branches = branches;
        _headAlerts = headAlerts;
        _changeEvents = changeEvents;
        _occurrences = occurrences;
        _suggestions = suggestions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _users = users;
        _appeals = appeals;
    }
    /// <summary>
    /// إبلاغ المُدخِل المحامي بتغيير تسمية جهته أثناء المراجعة:
    /// «تم تعديل اسم الجهة التي أدخلتها من “القديم” إلى “الجديد”».
    /// </summary>
    private async Task InsertRenameNoticeToCreatorAsync(PublicEntity entry, string oldName, string newName, CancellationToken token)
    {
        var creator = entry.CreatedBy;
        if (creator is null || creator.BranchId is null)
            return;

        var message = $"تم تعديل اسم الجهة العامة التي أدخلتها من «{oldName}» إلى «{newName}»";
        var alert = new HeadAlert
        {
            BranchId = creator.BranchId.Value,
            CreatedById = entry.ReviewedById ?? creator.Id,
            TargetType = HeadAlertTargetType.Lawyer,
            TargetLawyerId = creator.Id,
            Message = message.Length > 2000 ? message[..2000] : message,
            CreatedAt = DateTime.UtcNow,
            Recipients = { new HeadAlertRecipient { UserId = creator.Id } },
        };
        await _headAlerts.AddAsync(alert, token);
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
                doc.SearchText = DocumentSearchTextBuilder.Build(doc);
                doc.FullData = DocumentSearchTextBuilder.BuildFullData(doc);
            }
        }

        // طالبو التنفيذ الاعتباريون المربوطون جهة عامة (RegistryId != null): يُعاد
        // تسمية صفوفهم كبقية الجهات — لا يُلمس natural (بلا RegistryId) إطلاقًا.
        var executionApplicantNames = (await _entities.ListDistinctExecutionApplicantTextsAsync(token))
            .Select(t => t.Name)
            .Where(n => ArabicNameNormalizer.Normalize(n) == oldNorm)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (executionApplicantNames.Count > 0)
        {
            var rows = await _entities.ListExecutionApplicantRowsByNamesAsync(executionApplicantNames, token);
            foreach (var row in rows)
            {
                row.Name = newCanonical;
                AddLog(row.DocumentId, "__Col_ExecutionApplicants", "طالبو التنفيذ",
                    Clamp(oldCanonical), Clamp(newCanonical));
            }

            var applicantDocs = rows.Select(r => r.Document).GroupBy(d => d.Id).Select(g => g.First());
            foreach (var doc in applicantDocs)
            {
                affectedDocs[doc.Id] = doc;
                // ملف «منفذ عليه»/«عرض وايداع» بلا جهة طالبة كلاسية: اسم الطالب يُشتق من
                // طلبات التنفيذ الاعتباريين المربوطين جهة عامة فيتطابق العنوان مع الاسم
                // المعياري بعد إعادة التسمية (لا يبقى الاسم القديم في نص البحث).
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
        }

        await _uow.SaveChangesAsync(token);
        var details = $"مزامنة إعادة تسمية الجهة: «{oldCanonical}» ← «{newCanonical}»";
        foreach (var (documentId, changes) in logs)
            await _audit.LogDocumentChangeAsync(actorName, "rename_public_entity_sync",
                documentId, documentType: null, details, changes, token);
        return affectedDocs.Values.ToList();
    }
    // ── مساعدات خاصة ──

    private async Task EnsureHeadScopeAsync(EntityRegistryActor actor, PublicEntity? entry, string? fallbackGovernorate, CancellationToken ct)
    {
        if (actor.Role != UserRole.Head)
            return;
        var branch = actor.BranchId is null ? null : await _branches.GetByIdAsync(actor.BranchId.Value, ct);
        var branchGov = NormalizeOptional(branch?.Governorate);

        // نطاق رئيس القسم (قرار مالك المشروع): يدير ويراجع ما أدخله محامو فرعه،
        // بغض النظر عن المحافظة التي تتبع لها الجهة نفسها — فقد يُقيم محامٍ ملفًا
        // تنفيذيًا على جهة عامة تتبع محافظة أخرى. إضافةً إلى قيود محافظة فرعه
        // التي أدخلتها الإدارة (بلا محامٍ مُدخِل).
        if (entry?.CreatedBy is { BranchId: not null } creator)
        {
            var inCreatorBranch = creator.BranchId.Value == actor.BranchId;
            var inGovernorate = branchGov is not null
                && string.Equals(branchGov, entry.Governorate.Trim(), StringComparison.Ordinal);
            if (inCreatorBranch || inGovernorate)
                return;
            throw new UnauthorizedAccessException(
                "رئيس القسم مقصور على ما أدخله محامو فرعه أو قيود محافظة فرعه؛ اطلب من الإدارة ضبط محافظة الفرع أولًا");
        }

        var scopeGov = fallbackGovernorate ?? entry?.Governorate;
        if (branchGov is null || scopeGov is null || !string.Equals(branchGov, scopeGov.Trim(), StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                "رئيس القسم مقصور على ما أدخله محامو فرعه أو قيود محافظة فرعه؛ اطلب من الإدارة ضبط محافظة الفرع أولًا");
    }
    /// <summary>
    /// حارس الجهة الأم (C3/F3): رئيس القسم لا يحرّر قيد «الجهة الأم» إطلاقًا في أي مسار
    /// كتابة — يقتصر على إرسال اقتراح تعديل للإدارة.
    /// </summary>
    private static void GuardHeadCannotEditParent(EntityRegistryActor actor, PublicEntity entry)
    {
        if (actor.Role == UserRole.Head && entry.IsParentEntity)
            throw new UnauthorizedAccessException(
                "الجهة الأم تُدار عبر الاقتراح فقط — أرسل اقتراح تعديل للإدارة");
    }
    /// <summary>
    /// حارس الأم البنيوي (S1 — منع صريح للجميع): عمليات الفروع الأربع لا تستهدف قيد «الجهة الأم»
    /// ممن كان؛ طريقها الوحيد المسارات المركزية (Update / rename / AbolishAndReplace المجموعي).
    /// </summary>
    private static void GuardNotParentEntry(PublicEntity entry, string? message = null)
    {
        if (entry.IsParentEntity)
            throw new ArgumentException(
                message ?? "عمليات الفروع للفروع فقط — الأم تُدار عبر التعديل أو إعادة التسمية المركزية");
    }
    private async Task<PublicEntityGroup?> FindGroupByNormAsync(string norm, CancellationToken token)
    {
        if (norm.Length == 0)
            return null;
        // متتبَّعة عمدًا: القيد الجديد يشير إليها دون إعادة إدراجها (تعارض تتبع).
        var groups = await _entities.ListGroupsTrackedAsync(token);
        return groups.FirstOrDefault(g => ArabicNameNormalizer.Normalize(g.CanonicalName) == norm);
    }
    private async Task EnsureCanonicalAvailableAsync(string canonical, int excludeGroupId, CancellationToken ct)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        if (groups.Any(g => g.Id != excludeGroupId && ArabicNameNormalizer.Normalize(g.CanonicalName) == norm))
            throw new ArgumentException("اسم الجهة مستخدم مسبقًا لهوية أخرى");
    }
    private async Task EnsureNoDuplicateEntryAsync(int? excludeEntryId, string canonical, string governorate, string branchName, CancellationToken ct)
    {
        var norm = ArabicNameNormalizer.Normalize(canonical);
        var groups = await _entities.ListGroupsWithEntriesAsync(ct);
        var duplicated = groups
            .Where(g => ArabicNameNormalizer.Normalize(g.CanonicalName) == norm)
            .SelectMany(g => g.Entries)
            .Any(e => (excludeEntryId is null || e.Id != excludeEntryId)
                && e.Governorate == governorate && e.BranchName == branchName);
        if (duplicated)
            throw new ArgumentException("يوجد قيد لنفس الجهة بنفس المحافظة والفرع");
    }
    private static List<string> CleanAliases(IEnumerable<string>? aliases, string canonicalNorm)
    {
        var result = new List<string>();
        if (aliases is null)
            return result;
        var seen = new HashSet<string>(StringComparer.Ordinal) { canonicalNorm };
        foreach (var raw in aliases)
        {
            var text = (raw ?? string.Empty).Trim();
            if (text.Length == 0)
                continue;
            if (text.Length > 500)
                throw new ArgumentException("الاسم البديل أطول من 500 حرف");
            if (!seen.Add(ArabicNameNormalizer.Normalize(text)))
                continue;
            result.Add(text);
        }
        return result;
    }
    /// <summary>قيمة إلزامية بعد القصّ والتنظيف؛ الفارغ يُرفض برسالة.</summary>
    private static string Required(string? value, string emptyMessage, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException(emptyMessage);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{emptyMessage} — أقصى طول {maxLength}");
        return normalized;
    }
    /// <summary>قيمة اختيارية ببديل افتراضي معتمد («الجهة الأم») مع سقف الطول.</summary>
    private static string RequiredWithFallback(string? value, string fallback, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            normalized = fallback;
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{fallback} — أقصى طول {maxLength}");
        return normalized;
    }
    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
    private static string ValidEntityType(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (!PublicEntityTypeCatalog.IsValid(trimmed))
            throw new ArgumentException($"نوع الجهة غير صالح ({string.Join("/", PublicEntityTypeCatalog.All)})");
        return trimmed;
    }
    private static string ValidCitationFormula(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var trimmed = value.Trim().ToLowerInvariant();
        if (!CitationFormulaCatalog.IsValid(trimmed))
            throw new ArgumentException("صيغة المناداة غير صالحة (add-to-job/add-to-position)");
        return trimmed;
    }
    /// <summary>تحقق تسمية التغطية: فارغ → null؛ أطول من 150 → خطأ؛ مطابقة لمحافظة → خطأ.</summary>
    private static string? ValidateCoverageLabel(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;
        if (trimmed.Length > 150)
            throw new ArgumentException("تسمية التغطية أطول من 150 حرفًا");
        if (GovernorateCatalog.IsGovernorate(trimmed))
            throw new ArgumentException("تسمية التغطية لا يمكن أن تطابق اسم محافظة واحدة");
        if (trimmed.Any(c => c >= '\u0660' && c <= '\u0669'))
            throw new ArgumentException("تسمية التغطية لا تقبل أرقامًا عربية-هندية");
        return trimmed;
    }
    private static string? Clamp(string? value)
        => value is null ? null : DocumentSearchTextBuilder.Truncate(value);
    // ── مساعدا الطيّ المشتركان (الدمج/النقل-طي/التوحيد) ──
    // دلالة حرفية لمنطق الدمج (المرجع الأكمل): إعادة توجيه روابط القيد الثلاث وإعادة اشتقاق
    // المركّب، ثم الأسماء البديلة (المعياري + الكامل + سوابق القيد الممتصّ) بشروط الاستثناء نفسها.

    /// <summary>يعيد توجيه روابط قيد في مستند إلى قيد آخر ويعيد اشتقاق المركّب.</summary>
    private static void RepointEntryLinks(Document doc, int fromEntryId, int toEntryId)
    {
        foreach (var a in doc.ApplicantPublicEntities.Where(a => a.RegistryId == fromEntryId))
            a.RegistryId = toEntryId;
        foreach (var e in doc.ExecutedPublicEntities.Where(e => e.RegistryId == fromEntryId))
            e.RegistryId = toEntryId;
        foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId == fromEntryId))
            ea.RegistryId = toEntryId;
        doc.ApplicantRegistryId = ApplicantRegistryIdDeriver.Derive(doc);
    }
    /// <summary>أسماء بديلة «للبحث فقط» على قيد الناجي: المعياري + الكامل + سوابق القيد الممتصّ.</summary>
    private static void AddFoldAliases(PublicEntity targetEntry, string absorbedGroupName, PublicEntity absorbedEntry, ref int aliasesAdded)
    {
        var fullName = $"{absorbedGroupName} — {absorbedEntry.Governorate} / {absorbedEntry.BranchName}";
        var normalizedEntry = ArabicNameNormalizer.Normalize(absorbedGroupName);
        if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedEntry))
        {
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = absorbedGroupName,
            });
            aliasesAdded++;
        }
        var normalizedFull = ArabicNameNormalizer.Normalize(fullName);
        if (!targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == normalizedFull))
        {
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = fullName,
            });
            aliasesAdded++;
        }
        foreach (var priorAlias in absorbedEntry.Aliases)
        {
            var priorNorm = ArabicNameNormalizer.Normalize(priorAlias.AliasText);
            if (priorNorm.Length == 0
                || priorNorm == normalizedEntry
                || priorNorm == normalizedFull
                || targetEntry.Aliases.Any(a => ArabicNameNormalizer.Normalize(a.AliasText) == priorNorm))
                continue;
            targetEntry.Aliases.Add(new PublicEntityAlias
            {
                PublicEntityId = targetEntry.Id,
                AliasText = priorAlias.AliasText,
            });
            aliasesAdded++;
        }
    }
    private static int CountUniqueDocuments(Dictionary<int, Document> a, Dictionary<int, Document> b)
    {
        var ids = new HashSet<int>();
        foreach (var k in a.Keys) ids.Add(k);
        foreach (var k in b.Keys) ids.Add(k);
        return ids.Count;
    }
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
            // طالب التنفيذ الاعتباري المربوط جهة عامة (RegistryId != null): الاسم الاعتباري
            // يعادل TripleOr(Name, null, null, null) == Name — لا يُلمس natural (بلا RegistryId).
            foreach (var ea in doc.ExecutionApplicants.Where(ea => ea.RegistryId.HasValue))
                if (!string.IsNullOrWhiteSpace(ea.Name))
                    newNames[(AppealSnapshotSerializer.KindExecutionApplicant, ea.Id)] = ea.Name;
        }
        if (newNames.Count == 0)
            return;

        var documentIds = affectedDocs.Select(d => d.Id).Distinct().ToList();
        var appeals = await _appeals.ListByDocumentIdsAsync(documentIds, token);
        if (appeals.Count == 0)
            return;

        foreach (var appeal in appeals)
        {
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

    /// <summary>تنبيه تعميم لكل المحامين في كل الفروع النشطة — كل تنبيه برسالة مُقصّرة عند 2000.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastEntityChangeToAllLawyersAsync(string message, int actorUserId, CancellationToken token)
    {
        var grouped = await _headAlerts.ListAllActiveLawyersGroupedByBranchAsync(token);
        foreach (var (branchId, lawyers) in grouped)
        {
            if (lawyers.Count == 0)
                continue;
            var alert = new HeadAlert
            {
                BranchId = branchId,
                CreatedById = actorUserId,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { },
            };
            foreach (var lawyer in lawyers)
                alert.Recipients.Add(new HeadAlertRecipient { UserId = lawyer.Id });
            await _headAlerts.AddAsync(alert, token);
        }
    }
    /// <summary>تنبيه لكل رؤساء الأقسام في كل الفروع النشطة.</summary>
    /// <remarks>يُلتحم بالمعاملة الخارجية الواحدة التي يفتحها <see cref="TransactionRunner"/>، فيُثبَّت الكل أو يُتراجع الكل مع سائر التغييرات.</remarks>
    private async Task BroadcastToAllHeadsAsync(string message, int actorUserId, CancellationToken token)
    {
        var grouped = await _headAlerts.ListAllActiveHeadsGroupedByBranchAsync(token);
        foreach (var (branchId, heads) in grouped)
        {
            if (heads.Count == 0)
                continue;
            var alert = new HeadAlert
            {
                BranchId = branchId,
                CreatedById = actorUserId,
                TargetType = HeadAlertTargetType.Branch,
                Message = message.Length > 2000 ? message[..2000] : message,
                CreatedAt = DateTime.UtcNow,
                Recipients = { },
            };
            foreach (var head in heads)
                alert.Recipients.Add(new HeadAlertRecipient { UserId = head.Id });
            await _headAlerts.AddAsync(alert, token);
        }
    }
    // ── مساعدات مشتركة ──

    /// <summary>سقف لاحقة المرجع — يفوّض إلى <see cref="EntityChangeMessages.DecreeSuffix"/> (المصدر الموحّد).</summary>
    private static string BuildDecreeSuffix(string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.DecreeSuffix(decreeKind, decreeNumber, decreeDate);
}
