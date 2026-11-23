using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// قواعد دوائر التنفيذ المشتركة بين الخدمة ومعالجات `P4-01` (إنشاء/تفعيل/تسمية):
/// فحص الفرع/المحافظة، تطبيع الاسم، مالك الدائرة، رسالة التكرار، نطاق الملكية، والإسقاط.
/// نقطة واحدة تمنع انحراف الرسائل أو العدّادات بين المسارات.
/// </summary>
internal static class ExecutionCircuitRules
{
    internal static Branch RequireBranchWithGovernorate(Branch? branch)
    {
        if (branch is null)
            throw new ArgumentException("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(branch.Governorate)
            || !GovernorateCatalog.IsGovernorate(branch.Governorate!.Trim()))
            throw new ArgumentException("فرعك بلا محافظة معتمدة — راجع الإدارة لتثبيت محافظة الفرع قبل إدارة الدوائر");
        return branch;
    }

    /// <summary>
    /// شعبة المنشئ المالكة للدائرة الجديدة (§8.4 + اتساق §4.4 خدميًا: الشعبة
    /// في الفرع نفسه — لا قيد بين جدولين في المزوّدين). `null` = ملك القسم.
    /// </summary>
    internal static async Task<Section?> ResolveOwnerSectionAsync(
        int branchId, int actorUserId, IUserRepository users,
        IRepository<Section> sections, CancellationToken ct)
    {
        var actor = await users.GetByIdAsync(actorUserId, ct)
            ?? throw new ArgumentException("المنشئ غير موجود");
        if (actor.Role == UserRole.Head)
            return null;
        if (actor.Role != UserRole.SubHead)
            throw new UnauthorizedAccessException("إنشاء الدوائر مقصور على الرؤساء — الإدارة تنقل فقط");
        if (actor.SectionId is null)
            throw new UnauthorizedAccessException("حسابك بلا شعبة — راجع الإدارة");
        var section = await sections.GetByIdAsync(actor.SectionId.Value, ct)
            ?? throw new ArgumentException("شعبتك غير موجودة — راجع الإدارة");
        if (section.BranchId != branchId)
            throw new UnauthorizedAccessException("شعبتك ليست ضمن هذا الفرع");
        return section;
    }

    internal static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الدائرة مطلوب");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("اسم الدائرة أطول من 200 حرف");
        return trimmed;
    }

    /// <summary>
    /// رسالة تكرار اسم الدائرة مع بيان المالك (قسم/شعبة). الاسم فريد على مستوى
    /// الفرع (`UNIQUE(BranchId, NameNorm)`)، والرسالة العامة السابقة كانت تُخفي
    /// على رئيس الشعبة أن الاسم محجوز لدائرة قسم أو شعبة أخرى.
    /// </summary>
    internal static async Task<string> DuplicateCircuitMessageAsync(
        ExecutionCircuit existing, int? actorSectionId,
        IRepository<Section> sections, CancellationToken ct)
    {
        // الاسم المعروض هو اسم الدائرة القائمة (لا المدخل) — أدق عند اختلاف التطبيع.
        var name = existing.Name;
        if (existing.SectionId is null)
            return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر القسم — لا يمكن تكرار اسم الدائرة داخل الفرع";
        var section = await sections.GetByIdAsync(existing.SectionId.Value, ct);
        var sectionName = section?.Name;
        if (string.IsNullOrWhiteSpace(sectionName))
            return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر إحدى الشعب — لا يمكن تكرار اسم الدائرة داخل الفرع";
        if (actorSectionId == existing.SectionId)
            return $"الدائرة «{name}» موجودة مسبقًا ضمن دوائر شعبتك — لا يمكن تكرار اسم الدائرة داخل الفرع";
        return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر شعبة «{sectionName}» — لا يمكن تكرار اسم الدائرة داخل الفرع";
    }

    /// <summary>البحث عن دائرة بنفس الاسم المعياري داخل الفرع (مع استثناء الدائرة الجاري تعديلها).</summary>
    internal static ExecutionCircuit? FindCircuitByName(
        IEnumerable<ExecutionCircuit> all, int branchId, string nameNorm, int? excludeCircuitId)
        => all.FirstOrDefault(c => c.BranchId == branchId
            && c.NameNorm == nameNorm
            && (!excludeCircuitId.HasValue || c.Id != excludeCircuitId.Value));

    /// <summary>نطاق الملكية (§5.3 — قرار §2.21): القسم لدوائر القسم، والشعبة لدوائرها.</summary>
    internal static ExecutionCircuit EnsureOwned(ExecutionCircuit circuit, int branchId, int? ownerSectionId)
    {
        if (circuit.BranchId != branchId)
            throw new ArgumentException("الدائرة ليست ضمن فرعك");
        if (circuit.SectionId != ownerSectionId)
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");
        return circuit;
    }

    internal static async Task<ExecutionCircuitDto> BuildDtoAsync(
        ExecutionCircuit c, IDocumentRepository documents,
        IRepository<Section> sections, CancellationToken token)
    {
        var counts = await documents.CountByCircuitsAsync(new List<int> { c.Id }, token);
        counts.TryGetValue(c.Id, out var v);
        string? sectionName = null;
        if (c.SectionId.HasValue)
            sectionName = (await sections.GetByIdAsync(c.SectionId.Value, token))?.Name;
        return new ExecutionCircuitDto(c.Id, c.BranchId, null, c.Name, c.IsActive, v.FileCount, v.PendingCount, c.Version,
            c.SectionId, sectionName, v.DeletedCount);
    }

    /// <summary>
    /// تدقيق حقلي لملف في المسارات الجماعية (S5) — مرآة `LogDocumentChangesAsync` في المسار
    /// المفرد: صفوف «حقل/قبل/بعد» تُكتب داخل المعاملة، وإلا يبقى التسجيل نصيًا.
    /// </summary>
    internal static async Task LogDocumentChangeAsync(
        IAuditLogger audit,
        Document doc,
        string? actorName,
        string actionType,
        string detail,
        Dictionary<string, string?> before,
        CancellationToken token)
    {
        var changes = DocumentChangeTracker.Diff(before, doc);
        if (changes.Count == 0)
        {
            await audit.LogAsync(actorName, actionType, doc.Id, doc.DocumentType, detail, token);
            return;
        }
        await audit.LogDocumentChangeAsync(actorName, actionType, doc.Id, doc.DocumentType,
            $"{detail} — غيّر {changes.Count} حقلًا", changes, token);
    }
}
