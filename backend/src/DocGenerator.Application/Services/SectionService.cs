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
    Task<SectionDto> CreateSectionAsync(int branchId, string? name, string? actorName, CancellationToken ct = default);
    Task<SectionDto?> RenameSectionAsync(int sectionId, string? name, string? actorName, CancellationToken ct = default);
    Task<SectionDto?> SetActiveAsync(int sectionId, bool isActive, string? actorName, CancellationToken ct = default);
    Task<bool> DeleteSectionAsync(int sectionId, string? actorName, CancellationToken ct = default);
}

/// <summary>
/// إدارة الشعب (إضافة/تسمية/تفعيل/حذف) — مشرف/مدير (قرار §2.15).
/// التحقق من الصلاحية في المتحكم، والتحقق المنطقي والكتابة هنا ضمن معاملة مع سجل التدقيق.
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
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IUnitOfWork _uow;
    private readonly ITransactionRunner _tx;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;

    public SectionService(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IRepository<HeadSuccession> successions,
        IUnitOfWork uow,
        ITransactionRunner tx,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _successions = successions;
        _uow = uow;
        _tx = tx;
        _audit = audit;
        _dbErrors = dbErrors;
    }

    public async Task<List<SectionDto>> ListSectionsAsync(int branchId, CancellationToken ct = default)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        var all = await _sections.ListAsync(ct);
        var sections = all.Where(s => s.BranchId == branchId).OrderBy(s => s.Name).ToList();
        if (sections.Count == 0)
            return new List<SectionDto>();

        var circuits = await _circuits.ListAsync(ct);
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
            headNames.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SectionDto?> GetSectionAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await _sections.GetByIdAsync(sectionId, ct);
        return section is null ? null : await ToDtoAsync(section, ct);
    }

    public async Task<SectionDto> CreateSectionAsync(int branchId, string? name, string? actorName, CancellationToken ct = default)
    {
        var branch = await _branches.GetByIdAsync(branchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        var trimmed = NormalizeName(name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الشعبة مطلوب");

        if (await NameExistsAsync(branchId, norm, null, ct))
            throw new ArgumentException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك");

        var section = new Section
        {
            BranchId = branchId,
            Name = trimmed,
            NameNorm = norm,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        try
        {
            await _tx.RunAsync(async token =>
            {
                await _sections.AddAsync(section, token);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "create_section", null, null,
                    $"أنشأ شعبة «{trimmed}» في فرع {branch.Name}", token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
        return new SectionDto(section.Id, section.BranchId, branch.Name, section.Name, section.IsActive, 0, null);
    }

    public async Task<SectionDto?> RenameSectionAsync(int sectionId, string? name, string? actorName, CancellationToken ct = default)
    {
        var section = await _sections.GetByIdAsync(sectionId, ct);
        if (section is null)
            return null;
        var trimmed = NormalizeName(name);
        if (section.Name == trimmed)
            return await ToDtoAsync(section, ct);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الشعبة مطلوب");
        if (await NameExistsAsync(section.BranchId, norm, section.Id, ct))
            throw new ArgumentException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك");

        SectionDto result;
        try
        {
            result = await _tx.RunAsync(async token =>
            {
                var oldName = section.Name;
                section.Name = trimmed;
                section.NameNorm = norm;
                section.UpdatedAt = DateTime.UtcNow;
                _sections.Update(section);
                await _uow.SaveChangesAsync(token);
                await _audit.LogAsync(actorName, "rename_section", null, null,
                    $"أعاد تسمية الشعبة من «{oldName}» إلى «{trimmed}»", token);
                // سجل التعاقب عند وجود رئيس شاغل (§9.5 — تعديل اسم)؛ بلا رئيس
                // يُكتفى بالتدقيق (لا صف بلا موضوع).
                var head = (await _users.ListUsersBySectionAsync(section.Id, token))
                    .Where(u => u.IsActive && u.Role == UserRole.SubHead)
                    .OrderBy(u => u.Id)
                    .FirstOrDefault();
                if (head is not null)
                {
                    await _successions.AddAsync(new HeadSuccession
                    {
                        BranchId = section.BranchId,
                        SectionId = section.Id,
                        UserId = head.Id,
                        Role = UserRole.SubHead,
                        Event = HeadSuccessionEventCatalog.Renamed,
                        At = DateTime.UtcNow,
                        ActorName = actorName,
                        Reason = $"أُعيدت تسمية الشعبة من «{oldName}» إلى «{trimmed}»",
                    }, token);
                    await _uow.SaveChangesAsync(token);
                }
                return await ToDtoAsync(section, token);
            }, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
        return result;
    }

    public async Task<SectionDto?> SetActiveAsync(int sectionId, bool isActive, string? actorName, CancellationToken ct = default)
    {
        var section = await _sections.GetByIdAsync(sectionId, ct);
        if (section is null)
            return null;
        if (!isActive)
        {
            var circuitCount = (await _circuits.ListAsync(ct)).Count(c => c.SectionId == section.Id);
            if (circuitCount > 0)
                throw new ArgumentException($"لا يمكن تعطيل الشعبة — انقل دوائرها أولًا (بها {circuitCount} دائرة)");
        }

        return await _tx.RunAsync(async token =>
        {
            section.IsActive = isActive;
            section.UpdatedAt = DateTime.UtcNow;
            _sections.Update(section);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, isActive ? "activate_section" : "deactivate_section",
                null, null,
                $"{(isActive ? "فعّل" : "عطّل")} الشعبة «{section.Name}» — التعطيل إخفاء من القوائم فقط ولا يوقف عمل رئيسها", token);
            return await ToDtoAsync(section, token);
        }, ct);
    }

    public async Task<bool> DeleteSectionAsync(int sectionId, string? actorName, CancellationToken ct = default)
    {
        var section = await _sections.GetByIdAsync(sectionId, ct);
        if (section is null)
            return false;
        var circuitCount = (await _circuits.ListAsync(ct)).Count(c => c.SectionId == section.Id);
        if (circuitCount > 0)
            throw new ArgumentException($"لا يمكن حذف الشعبة — انقل دوائرها أولًا (بها {circuitCount} دائرة)");
        var users = await _users.ListUsersBySectionAsync(section.Id, ct);
        if (users.Count > 0)
            throw new ArgumentException("لا يمكن حذف الشعبة — انقل رئيسها/حساباتها أولًا");

        return await _tx.RunAsync(async token =>
        {
            _sections.Remove(section);
            await _uow.SaveChangesAsync(token);
            await _audit.LogAsync(actorName, "delete_section", null, null,
                $"حذف الشعبة «{section.Name}»", token);
            return true;
        }, ct);
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم الشعبة مطلوب");
        var trimmed = name.Trim();
        if (trimmed.Length > 200)
            throw new ArgumentException("اسم الشعبة أطول من 200 حرف");
        return trimmed;
    }

    private async Task<bool> NameExistsAsync(int branchId, string norm, int? excludeId, CancellationToken ct)
    {
        var all = await _sections.ListAsync(ct);
        return all.Any(s => s.BranchId == branchId
            && s.NameNorm == norm
            && (excludeId == null || s.Id != excludeId.Value));
    }

    private async Task<SectionDto> ToDtoAsync(Section section, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(section.BranchId, ct);
        var circuits = await _circuits.ListAsync(ct);
        var head = (await _users.ListUsersBySectionAsync(section.Id, ct))
            .Where(u => u.IsActive && u.Role == UserRole.SubHead)
            .OrderBy(u => u.Id)
            .FirstOrDefault();
        return new SectionDto(
            section.Id,
            section.BranchId,
            branch?.Name,
            section.Name,
            section.IsActive,
            circuits.Count(c => c.SectionId == section.Id),
            head?.FullName);
    }
}
