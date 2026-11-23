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
        // ملكية النطاق (§5.3 — قرار §2.21): رئيس القسم لدوائر القسم (`null`)،
        // ورئيس الشعبة لدوائر شعبته — وغيرها «ليست ضمن نطاقك» (§14).
        return await ExecutionCircuitRules.GetOwnedAsync(
            circuitId, branchId, ownerSectionId, _circuits, ct);
    }
}
