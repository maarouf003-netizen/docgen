using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// مساعدات قراءات دوائر التنفيذ (`P4-01ct`): بوابة فرع المحافظة المشتركة بين
/// القراءتين اللتين تفرضانها، وأسماء الشعب دفعة واحدة. منطق الكاش في
/// `ReferenceDataQueries` فلا نسخة ثانية.
/// </summary>
internal static class ExecutionCircuitProjection
{
    internal static async Task RequireBranchWithGovernorateAsync(
        IRepository<Branch> branches, int branchId, CancellationToken ct)
        => ExecutionCircuitRules.RequireBranchWithGovernorate(
            await branches.GetByIdAsync(branchId, ct));

    internal static async Task<Dictionary<int, string>> SectionNamesAsync(
        IRepository<Section> sections, IReferenceDataCache? cache, CancellationToken ct)
        => (await ReferenceDataQueries.ListSectionsAsync(sections, cache, ct))
            .ToDictionary(s => s.Id, s => s.Name);
}
