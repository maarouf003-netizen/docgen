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
        => ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(branchId, ct));

    private static string NormalizeName(string? name) => ExecutionCircuitRules.NormalizeName(name);

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
        return await ExecutionCircuitRules.DuplicateCircuitMessageAsync(existing, actorSectionId, _sections, ct);
    }

    /// <summary>البحث عن دائرة بنفس الاسم المعياري داخل الفرع (مع استثناء الدائرة الجاري تعديلها).</summary>
    private async Task<ExecutionCircuit?> FindCircuitByNameAsync(
        int branchId, string nameNorm, int? excludeCircuitId, CancellationToken ct)
    {
        return ExecutionCircuitRules.FindCircuitByName(
            await ListCircuitsAsync(ct), branchId, nameNorm, excludeCircuitId);
    }

    private async Task<ExecutionCircuit> GetOwnedAsync(int circuitId, int branchId, int? ownerSectionId, CancellationToken ct)
    {
        var circuit = await _circuits.GetByIdAsync(circuitId, ct)
            ?? throw new ArgumentException("الدائرة غير موجودة");
        // ملكية النطاق (§5.3 — قرار §2.21): رئيس القسم لدوائر القسم (`null`)،
        // ورئيس الشعبة لدوائر شعبته — وغيرها «ليست ضمن نطاقك» (§14).
        return ExecutionCircuitRules.EnsureOwned(circuit, branchId, ownerSectionId);
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
