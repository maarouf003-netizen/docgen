using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>
/// إعادة تسمية شعبة — يملك معاملته وتدقيقه وإبطال كاش الشعب، ويسجّل تعاقب الرئيس
/// عند وجود رئيس شاغل، ويحوّل انتهاك الوحدانية إلى `DocumentConflictException`.
/// </summary>
public sealed record RenameSectionCommand(
    int SectionId,
    string? Name,
    string? ActorName,
    long? Version = null) : ICommand<SectionDto?>, ITransactionalRequest;

public sealed class RenameSectionCommandHandler
    : CommandHandlerBase<RenameSectionCommand, SectionDto?>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IRepository<HeadSuccession> _successions;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IReferenceDataCache? _cache;

    public RenameSectionCommandHandler(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IRepository<HeadSuccession> successions,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<RenameSectionCommand, SectionDto?>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _successions = successions;
        _uow = uow;
        _audit = audit;
        _dbErrors = dbErrors;
        _cache = cache;
    }

    protected override async Task<SectionDto?> HandleCoreAsync(
        RenameSectionCommand command, CancellationToken ct)
    {
        var section = await _sections.GetByIdAsync(command.SectionId, ct);
        if (section is null)
            return null;
        OptimisticConcurrencyGuard.EnsureCurrent(section, command.Version, "الشعبة");
        var trimmed = SectionService.NormalizeName(command.Name);
        if (section.Name == trimmed)
            return await ToDtoAsync(section, ct);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الشعبة مطلوب");
        if (SectionService.NameExists(
                await _sections.ListAsync(ct), section.BranchId, norm, section.Id))
            throw new ArgumentException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك");

        try
        {
            var oldName = section.Name;
            section.Name = trimmed;
            section.NameNorm = norm;
            section.UpdatedAt = DateTime.UtcNow;
            _sections.Update(section);
            await _uow.SaveChangesAsync(ct);
            _cache?.Invalidate(ReferenceDataGroups.Sections);
            await _audit.LogAsync(command.ActorName, "rename_section", null, null,
                $"أعاد تسمية الشعبة من «{oldName}» إلى «{trimmed}»", ct);
            // سجل التعاقب عند وجود رئيس شاغل (§9.5 — تعديل اسم)؛ بلا رئيس
            // يُكتفى بالتدقيق (لا صف بلا موضوع).
            var head = (await _users.ListUsersBySectionAsync(section.Id, ct))
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
                    ActorName = command.ActorName,
                    Reason = $"أُعيدت تسمية الشعبة من «{oldName}» إلى «{trimmed}»",
                }, ct);
                await _uow.SaveChangesAsync(ct);
                _cache?.Invalidate(ReferenceDataGroups.Sections);
            }
            return await ToDtoAsync(section, ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
    }

    private async Task<SectionDto> ToDtoAsync(Section section, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(section.BranchId, ct);
        return SectionService.ToDto(
            section,
            branch?.Name,
            await _circuits.ListAsync(ct),
            await _users.ListUsersBySectionAsync(section.Id, ct));
    }
}
