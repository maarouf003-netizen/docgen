using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Audit;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public sealed partial class ExecutionCircuitService : IExecutionCircuitService
{
    private async Task<Branch> RequireBranchWithGovernorateAsync(int branchId, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        if (string.IsNullOrWhiteSpace(branch.Governorate) || !GovernorateCatalog.IsGovernorate(branch.Governorate!.Trim()))
            throw new ArgumentException("فرعك بلا محافظة معتمدة — راجع الإدارة لتثبيت محافظة الفرع قبل إدارة الدوائر");
        return branch;
    }

    /// <summary>
    /// شعبة المنشئ المالكة للدائرة الجديدة (§8.4 + اتساق §4.4 خدميًا: الشعبة
    /// في الفرع نفسه — لا قيد بين جدولين في المزوّدين). `null` = ملك القسم.
    /// </summary>
    private async Task<Section?> ResolveOwnerSectionAsync(int branchId, int actorUserId, CancellationToken ct)
    {
        var actor = await _users.GetByIdAsync(actorUserId, ct)
            ?? throw new ArgumentException("المنشئ غير موجود");
        if (actor.Role == UserRole.Head)
            return null;
        if (actor.Role != UserRole.SubHead)
            throw new UnauthorizedAccessException("إنشاء الدوائر مقصور على الرؤساء — الإدارة تنقل فقط");
        if (actor.SectionId is null)
            throw new UnauthorizedAccessException("حسابك بلا شعبة — راجع الإدارة");
        var section = await _sections.GetByIdAsync(actor.SectionId.Value, ct)
            ?? throw new ArgumentException("شعبتك غير موجودة — راجع الإدارة");
        if (section.BranchId != branchId)
            throw new UnauthorizedAccessException("شعبتك ليست ضمن هذا الفرع");
        return section;
    }

    private static string NormalizeName(string? name)
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
    /// على رئيس الشعبة أن الاسم محجوز لدائرة قسم أو شعبة أخرى — فيظن أن الدائرة
    /// غير موجودة ويحاول إنشاءها مرارًا.
    /// </summary>
    private async Task<string> DuplicateCircuitMessageAsync(
        ExecutionCircuit existing, int? actorSectionId, CancellationToken ct)
    {
        // الاسم المعروض هو اسم الدائرة القائمة (لا المدخل) — أدق عند اختلاف التطبيع
        // (مسافات/همزات) فيرى المستخدم الدائرة التي يحجزه اسمُها فعلًا.
        var name = existing.Name;
        if (existing.SectionId is null)
            return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر القسم — لا يمكن تكرار اسم الدائرة داخل الفرع";
        var section = await _sections.GetByIdAsync(existing.SectionId.Value, ct);
        var sectionName = section?.Name;
        if (string.IsNullOrWhiteSpace(sectionName))
            return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر إحدى الشعب — لا يمكن تكرار اسم الدائرة داخل الفرع";
        if (actorSectionId == existing.SectionId)
            return $"الدائرة «{name}» موجودة مسبقًا ضمن دوائر شعبتك — لا يمكن تكرار اسم الدائرة داخل الفرع";
        return $"الدائرة «{name}» موجودة مسبقًا في فرعك ضمن دوائر شعبة «{sectionName}» — لا يمكن تكرار اسم الدائرة داخل الفرع";
    }

    /// <summary>البحث عن دائرة بنفس الاسم المعياري داخل الفرع (مع استثناء الدائرة الجاري تعديلها).</summary>
    private async Task<ExecutionCircuit?> FindCircuitByNameAsync(
        int branchId, string nameNorm, int? excludeCircuitId, CancellationToken ct)
    {
        var all = await ListCircuitsAsync(ct);
        return all.FirstOrDefault(c => c.BranchId == branchId
            && c.NameNorm == nameNorm
            && (!excludeCircuitId.HasValue || c.Id != excludeCircuitId.Value));
    }

    private async Task<ExecutionCircuit> GetOwnedAsync(int circuitId, int branchId, int? ownerSectionId, CancellationToken ct)
    {
        var circuit = await _circuits.GetByIdAsync(circuitId, ct)
            ?? throw new ArgumentException("الدائرة غير موجودة");
        if (circuit.BranchId != branchId)
            throw new ArgumentException("الدائرة ليست ضمن فرعك");
        // ملكية النطاق (§5.3 — قرار §2.21): رئيس القسم لدوائر القسم (`null`)،
        // ورئيس الشعبة لدوائر شعبته — وغيرها «ليست ضمن نطاقك» (§14).
        if (circuit.SectionId != ownerSectionId)
            throw new ArgumentException("الدائرة ليست ضمن نطاقك");
        return circuit;
    }

    /// <summary>
    /// تدقيق حقلي لكل ملف في المسارات الجماعية (S5) — مرآة LogDocumentChangesAsync
    /// في المسار المفرد: لقطة «قبل» تُلتقط قبل الطفرة، وصفوف «حقل/قبل/بعد»
    /// تُكتب داخل المعاملة نفسها بعد الحفظ الرئيسي.
    /// </summary>
    private async Task LogCircuitDocumentChangeAsync(
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
            await _audit.LogAsync(actorName, actionType, doc.Id, doc.DocumentType, detail, token);
            return;
        }
        await _audit.LogDocumentChangeAsync(actorName, actionType, doc.Id, doc.DocumentType,
            $"{detail} — غيّر {changes.Count} حقلًا", changes, token);
    }
}
