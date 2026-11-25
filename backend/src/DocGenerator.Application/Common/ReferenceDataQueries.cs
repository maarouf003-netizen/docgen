using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common;

/// <summary>
/// قراءات البيانات المرجعية المخزّنة (الدوائر/الشعب/الفروع) — مصدر واحد يمنع تكرار
/// منطق `IReferenceDataCache` بين الاستعلامات. العدّادات الحيّة تُحسب خارج الكاش دائمًا.
/// </summary>
internal static class ReferenceDataQueries
{
    internal static Task<List<ExecutionCircuit>> ListCircuitsAsync(
        IRepository<ExecutionCircuit> circuits, IReferenceDataCache? cache, CancellationToken ct)
        => cache is null
            ? circuits.ListAsync(ct)
            : cache.GetOrCreateAsync(ReferenceDataGroups.Circuits, "all", circuits.ListNoTrackingAsync, ct);

    internal static Task<List<Section>> ListSectionsAsync(
        IRepository<Section> sections, IReferenceDataCache? cache, CancellationToken ct)
        => cache is null
            ? sections.ListAsync(ct)
            : cache.GetOrCreateAsync(ReferenceDataGroups.Sections, "all", sections.ListNoTrackingAsync, ct);

    internal static Task<List<Branch>> ListBranchesAsync(
        IRepository<Branch> branches, IReferenceDataCache? cache, CancellationToken ct)
        => cache is null
            ? branches.ListAsync(ct)
            : cache.GetOrCreateAsync(ReferenceDataGroups.Branches, "all", branches.ListNoTrackingAsync, ct);
}
