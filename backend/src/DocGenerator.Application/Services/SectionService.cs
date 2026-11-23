using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

public interface ISectionService
{
    Task<List<SectionDto>> ListSectionsAsync(int branchId, CancellationToken ct = default);
    Task<SectionDto?> GetSectionAsync(int sectionId, CancellationToken ct = default);
}

/// <summary>
/// قراءة الشعب والإسقاط المشترك (`SectionService.ToDto`) — الإنشاء/التسمية/التفعيل/الحذف
/// في Commands `P4-01` — مشرف/مدير (قرار §2.15).
/// القواعد (قرار §2 + §8.2): الاسم فريد × الفرع؛ الحذف والتعطيل ممنوعان مع وجود
/// دوائر (تُنقل أولًا)؛ والحذف ممنوع مع بقاء حسابات؛ التعطيل إخفاء من القوائم
/// فقط (مرآة فلسفة السجل) ولا يوقف عمل الرئيس — يُعلن ذلك في الرسائل.
/// </summary>
public sealed class SectionService : ISectionService
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IReferenceDataCache? _cache;

    public SectionService(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IReferenceDataCache? cache = null)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _cache = cache;
    }

    // قراءات مرجعية بحتة — العدّادات الحيّة (الدوائر/الرؤساء) تُحسب خارج الكاش.
    private Task<List<Section>> ListSectionsAsync(CancellationToken ct)
        => _cache is null
            ? _sections.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Sections, "all", _sections.ListNoTrackingAsync, ct);

    private Task<List<ExecutionCircuit>> ListCircuitsAsync(CancellationToken ct)
        => _cache is null
            ? _circuits.ListAsync(ct)
            : _cache.GetOrCreateAsync(ReferenceDataGroups.Circuits, "all", _circuits.ListNoTrackingAsync, ct);

    public async Task<List<SectionDto>> ListSectionsAsync(int branchId, CancellationToken ct = default)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        var all = await ListSectionsAsync(ct);
        var sections = all.Where(s => s.BranchId == branchId).OrderBy(s => s.Name).ToList();
        if (sections.Count == 0)
            return new List<SectionDto>();

        var circuits = await ListCircuitsAsync(ct);
        var circuitCounts = circuits
            .Where(c => c.SectionId.HasValue)
            .GroupBy(c => c.SectionId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());
        var allSectionUsers = new List<User>();
        foreach (var s in sections)
            allSectionUsers.AddRange(await _users.ListUsersBySectionAsync(s.Id, ct));
        var headNames = allSectionUsers
            .Where(u => u.IsActive && u.Role == UserRole.SubHead)
            .GroupBy(u => u.SectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(u => u.Id).First().FullName);

        return sections.Select(s => new SectionDto(
            s.Id,
            s.BranchId,
            branch.Name,
            s.Name,
            s.IsActive,
            circuitCounts.GetValueOrDefault(s.Id),
            headNames.GetValueOrDefault(s.Id),
            s.Version)).ToList();
    }

    public async Task<SectionDto?> GetSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _sections.GetByIdAsync(sectionId, ct);
        return section is null ? null : await ToDtoAsync(section, ct);
    }

    internal static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الشعبة مطلوب");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("اسم الشعبة أطول من 200 حرف");
        return trimmed;
    }

    /// <summary>وحدانية الاسم × الفرع (بلا حساسية للحالة/التشكيل عبر `NameNorm`) — مشتركة مع معالج الإنشاء.</summary>
    internal static bool NameExists(
        IEnumerable<Section> all, int branchId, string norm, int? excludeId)
        => all.Any(s => s.BranchId == branchId
            && s.NameNorm == norm
            && (excludeId == null || s.Id != excludeId.Value));

    private async Task<bool> NameExistsAsync(int branchId, string norm, int? excludeId, CancellationToken ct)
    {
        var all = await ListSectionsAsync(ct);
        return NameExists(all, branchId, norm, excludeId);
    }

    private async Task<SectionDto> ToDtoAsync(Section section, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(section.BranchId, ct);
        return ToDto(
            section,
            branch?.Name,
            await ListCircuitsAsync(ct),
            await _users.ListUsersBySectionAsync(section.Id, ct));
    }

    /// <summary>
    /// إسقاط الشعبة الموحّد (عدّاد الدوائر + رئيسها الشاغل) — نقطة واحدة للقراءة
    /// ولمعالجات التسمية/التفعيل فلا تنحرف الحقول بين المسارات.
    /// </summary>
    internal static SectionDto ToDto(
        Section section,
        string? branchName,
        IReadOnlyList<ExecutionCircuit> circuits,
        IReadOnlyList<User> users)
    {
        var head = users
            .Where(u => u.IsActive && u.Role == UserRole.SubHead)
            .OrderBy(u => u.Id)
            .FirstOrDefault();
        return new SectionDto(
            section.Id,
            section.BranchId,
            branchName,
            section.Name,
            section.IsActive,
            circuits.Count(c => c.SectionId == section.Id),
            head?.FullName,
            section.Version);
    }
}
