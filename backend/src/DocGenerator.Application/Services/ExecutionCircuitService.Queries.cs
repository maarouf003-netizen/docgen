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
    public async Task<List<ExecutionCircuitDto>> ListMineAsync(int branchId, int? ownerSectionId, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        var all = await ListCircuitsAsync(ct);
        var circuits = all
            .Where(c => c.BranchId == branchId && c.SectionId == ownerSectionId)
            .OrderBy(c => c.Name)
            .ToList();
        var counts = await _documents.CountByCircuitsAsync(circuits.Select(c => c.Id).ToList(), ct);
        var sectionNames = await SectionNamesAsync(ct);
        return circuits.Select(c => new ExecutionCircuitDto(
            c.Id, c.BranchId, null, c.Name, c.IsActive,
            counts.TryGetValue(c.Id, out var v) ? v.FileCount : 0,
            counts.TryGetValue(c.Id, out var v2) ? v2.PendingCount : 0,
            c.Version, c.SectionId,
            c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null,
            counts.TryGetValue(c.Id, out var v3) ? v3.DeletedCount : 0)).ToList();
    }

    private async Task<Dictionary<int, string>> SectionNamesAsync(CancellationToken ct)
    {
        var sections = await ListSectionsAsync(ct);
        return sections.ToDictionary(s => s.Id, s => s.Name);
    }

    /// <summary>
    /// نافذة الإفراغ (حزمة A): كل ملفات الدائرة غير المحذوفة — المخفية عن «القوائم» شاملة
    /// (إخفاء الحالات المتقدمة من ApplySearchFilters مقصود في القوائم، والمسار هنا مخصص
    /// يلتفّ عليه) — بلا حدّ صفحات (العطل القديم: perPage:100 كان يقطع الدوائر الكبيرة).
    /// البوابات اتساقًا مع ListMineAsync/DeleteAsync: فرع المحافظة → النطاق من GetOwnedAsync.
    /// </summary>
    public async Task<List<CircuitFileDto>> ListFilesAsync(int circuitId, int branchId, int? ownerSectionId, CancellationToken ct = default)
    {
        await RequireBranchWithGovernorateAsync(branchId, ct);
        _ = await GetOwnedAsync(circuitId, branchId, ownerSectionId, ct);
        var docs = await _documents.ListCircuitFileSummariesAsync(circuitId, ct);
        return docs.Select(d => new CircuitFileDto(
            d.Id, d.BorrowerName, d.FileNumber, d.FileType, d.FileYear, d.Lawyer)).ToList();
    }

    /// <summary>
    /// قوائم الاختيار للمحامين (تسطير الملفات في أي دائرة — قرار §2.11) مقابل
    /// الرؤساء (دوائر نطاقهم فقط). `limitToOwner` يميّز الحالتين صراحةً — لا
    /// قيمة سحرية (`null` تعني القسم للرئيس، ولا شيء للمحامي).
    /// </summary>
    public async Task<List<ExecutionCircuitDto>> ListForLawyerAsync(int branchId, int? ownerSectionId, bool limitToOwner, CancellationToken ct = default)
    {
        var all = await ListCircuitsAsync(ct);
        var sectionNames = await SectionNamesAsync(ct);
        return all.Where(c => c.BranchId == branchId && c.IsActive && (!limitToOwner || c.SectionId == ownerSectionId))
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version,
                c.SectionId, c.SectionId.HasValue ? sectionNames.GetValueOrDefault(c.SectionId.Value) : null)).ToList();
    }

    public async Task<List<ExecutionCircuitDto>> ListForDelegationAsync(string? governorate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(governorate) || !GovernorateCatalog.IsGovernorate(governorate!.Trim()))
            return new List<ExecutionCircuitDto>();
        var gov = governorate!.Trim();
        var branches = await ListBranchesAsync(ct);
        var branchIds = branches.Where(b => b.Governorate == gov).Select(b => b.Id).ToHashSet();
        if (branchIds.Count == 0) return new List<ExecutionCircuitDto>();
        var all = await ListCircuitsAsync(ct);
        return all.Where(c => branchIds.Contains(c.BranchId) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ExecutionCircuitDto(
                c.Id, c.BranchId, null, c.Name, c.IsActive, 0, 0, c.Version)).ToList();
    }

    /// <summary>
    /// صفوف إحصاءات الدوائر بالنطاق (قرار §2.27 — التفصيل الرباعي في المرحلة 8):
    /// الرئيس لدوائر نطاقه فقط، والمدير/المشرف للكل — زائد صف اصطناعي واحد «بلا دائرة»
    /// (`CircuitId = 0`) لملفات النطاق بلا دائرة عندما وُجدت (F4: شرط `FileCount > 0`
    /// يمنع صفًا صفريًا دائمًا؛ والشعبة بلا صف أصلًا إذ ملفاتها كلها بدوائر §5.6).
    /// </summary>
    public async Task<List<CircuitStatsDto>> CircuitStatsAsync(int? branchId, int? ownerSectionId, bool fullAccess, CancellationToken ct = default)
    {
        var all = await ListCircuitsAsync(ct);
        var circuits = (branchId is null ? all : all.Where(c => c.BranchId == branchId.Value));
        if (!fullAccess)
            circuits = circuits.Where(c => c.SectionId == ownerSectionId);
        var ordered = circuits.OrderBy(c => c.BranchId).ThenBy(c => c.Name).ToList();
        var branches = await ListBranchesAsync(ct);
        var branchNames = branches.ToDictionary(b => b.Id, b => b.Name);
        // أسماء الشعب دفعة واحدة — بلا استعلام لكل دائرة (N+1).
        var sectionNames = (await ListSectionsAsync(ct))
            .Where(s => ordered.Any(c => c.SectionId == s.Id))
            .ToDictionary(s => s.Id, s => s.Name);
        var counts = await _documents.CountByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        // المحامون النشطون (≥ ملف واحد): استعلام تجميعي واحد — بلا تحميل كيانات (N+1).
        var lawyerCounts = await _documents.CountLawyersByCircuitsAsync(ordered.Select(c => c.Id).ToList(), ct);
        var rows = ordered.Select(c =>
        {
            counts.TryGetValue(c.Id, out var v);
            return new CircuitStatsDto(c.Id, c.Name, c.BranchId,
                branchNames.TryGetValue(c.BranchId, out var n) ? n : null,
                c.IsActive, v.FileCount, lawyerCounts.TryGetValue(c.Id, out var lc) ? lc : 0, v.PendingCount,
                c.SectionId,
                c.SectionId.HasValue && sectionNames.TryGetValue(c.SectionId.Value, out var s) ? s : null,
                c.Version);
        }).ToList();
        if (!ownerSectionId.HasValue)
        {
            var (fileCount, pendingCount, lawyerCount) = await _documents.CountWithoutCircuitAsync(branchId, ct);
            if (fileCount > 0)
            {
                branchNames.TryGetValue(branchId ?? 0, out var branchName);
                rows.Add(new CircuitStatsDto(0, "بلا دائرة", branchId ?? 0, branchName,
                    true, fileCount, lawyerCount, pendingCount, null, null, 0));
            }
        }
        return rows;
    }

    private Task<ExecutionCircuitDto> ToDtoAsync(ExecutionCircuit c, CancellationToken token)
        => ExecutionCircuitRules.BuildDtoAsync(c, _documents, _sections, token);
}
