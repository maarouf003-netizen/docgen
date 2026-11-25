using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class PublicEntityService
{
    private async Task EnsureHeadScopeAsync(EntityRegistryActor actor, PublicEntity? entry, string? fallbackGovernorate, CancellationToken ct)
    {
        if (actor.Role is not (UserRole.Head or UserRole.SubHead))
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
        if (actor.Role is UserRole.Head or UserRole.SubHead && entry.IsParentEntity)
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
    /// <summary>
    /// ضبط الاسم المعياري (`PB-001`) — يُستدعى بعد كل كتابة `CanonicalName`
    /// (إنشاء/تسمية/دمج/استيراد) فيغذّي القيد الفريد المعياري.
    /// </summary>
    private static void SetGroupNorm(PublicEntityGroup group)
        => group.CanonicalNameNorm = ArabicNameNormalizer.Normalize(group.CanonicalName);

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
        // `PB-001`: الفرع نص حر — يُقارَن معياريًا (المحافظة من كتالوج ثابت، وتُطبَّع مع ذلك توحيدًا).
        var normGov = ArabicNameNormalizer.Normalize(governorate);
        var normBranch = ArabicNameNormalizer.Normalize(branchName);
        var duplicated = groups
            .Where(g => ArabicNameNormalizer.Normalize(g.CanonicalName) == norm)
            .SelectMany(g => g.Entries)
            .Any(e => (excludeEntryId is null || e.Id != excludeEntryId)
                && ArabicNameNormalizer.Normalize(e.Governorate) == normGov
                && ArabicNameNormalizer.Normalize(e.BranchName) == normBranch);
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
    internal static string? NormalizeOptional(string? value)
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
    /// <summary>
    /// تحقق شروط الدمج/الطيّ القاسية بين قيدين (المجموعة + المحافظة + النشاط + نطاق
    /// رئيس القسم + بلا NeedsReview). يملأ الأخطاء/التحذيرات بلا رمي (للشروط) أو يرمي
    /// UnauthorizedAccessException (للنطاق/حارس الأم).
    /// </summary>
    internal static async Task ValidateMergeablePairAsync(
        IPublicEntityWriteRules rules,
        int groupId,
        PublicEntity? source,
        PublicEntity? target,
        int targetId,
        EntityRegistryActor actor,
        CancellationToken ct,
        List<string> errors,
        List<string> warnings)
    {
        if (source is null || target is null) { errors.Add("الفرع غير موجود"); return; }
        if (source.GroupId != groupId || target.GroupId != groupId) { errors.Add("الفروع المحددة ليست ضمن نفس المجموعة"); return; }
        if (source.Id == targetId) { errors.Add("لا يمكن دمج الفرع مع نفسه"); return; }
        if (!source.IsActive || !target.IsActive) { errors.Add("أحد الفروع غير نشط"); return; }
        if (source.NeedsReview || target.NeedsReview) { errors.Add("لا يمكن الدمج لوجود قيد بانتظار المراجعة"); return; }
        if (!string.Equals(source.Governorate, target.Governorate, StringComparison.Ordinal))
        { errors.Add("المحافظتان مختلفتان — الدمج يتطلب نفس المحافظة"); return; }
        rules.GuardNotParentEntry(source);
        rules.GuardNotParentEntry(target);
        rules.GuardHeadCannotEditParent(actor, source);
        rules.GuardHeadCannotEditParent(actor, target);
        // النطاق يُرمى كـ Unauthorized — لا يُعرض كخطأ قابل للتجاوز في المعاينة.
        await rules.EnsureHeadScopeAsync(actor, source, source.Governorate, ct);
        await rules.EnsureHeadScopeAsync(actor, target, target.Governorate, ct);
    }

    private static string? Clamp(string? value)
        => value is null ? null : DocumentSearchTextBuilder.Truncate(value);

    /// <summary>سقف لاحقة المرجع — يفوّض إلى <see cref="EntityChangeMessages.DecreeSuffix"/> (المصدر الموحّد).</summary>
    internal static string BuildDecreeSuffix(string decreeKind, string decreeNumber, DateTime? decreeDate)
        => EntityChangeMessages.DecreeSuffix(decreeKind, decreeNumber, decreeDate);
}
