using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>
/// تفعيل/تعطيل شعبة — يملك معاملته وتدقيقه وإبطال كاش الشعب؛ التعطيل ممنوع مع وجود
/// دوائر، والتعطيل إخفاء من القوائم فقط (بنفس رسالة الأصل).
/// </summary>
public sealed record SetSectionActiveCommand(
    int SectionId,
    bool IsActive,
    string? ActorName,
    long? Version = null) : ICommand<SectionDto?>, ITransactionalRequest;

public sealed class SetSectionActiveCommandHandler
    : CommandHandlerBase<SetSectionActiveCommand, SectionDto?>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public SetSectionActiveCommandHandler(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SetSectionActiveCommand, SectionDto?>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _branches = branches;
        _circuits = circuits;
        _users = users;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<SectionDto?> HandleCoreAsync(
        SetSectionActiveCommand command, CancellationToken ct)
    {
        var section = await _sections.GetByIdAsync(command.SectionId, ct);
        if (section is null)
            return null;
        OptimisticConcurrencyGuard.EnsureCurrent(section, command.Version, "الشعبة");
        if (!command.IsActive)
        {
            var circuitCount = (await _circuits.ListAsync(ct)).Count(c => c.SectionId == section.Id);
            if (circuitCount > 0)
                throw new ArgumentException($"لا يمكن تعطيل الشعبة — انقل دوائرها أولًا (بها {circuitCount} دائرة)");
        }

        section.IsActive = command.IsActive;
        section.UpdatedAt = DateTime.UtcNow;
        _sections.Update(section);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Sections);
        await _audit.LogAsync(command.ActorName, command.IsActive ? "activate_section" : "deactivate_section",
            null, null,
            $"{(command.IsActive ? "فعّل" : "عطّل")} الشعبة «{section.Name}» — التعطيل إخفاء من القوائم فقط ولا يوقف عمل رئيسها", ct);
        var branch = await _branches.GetByIdAsync(section.BranchId, ct);
        return SectionProjection.ToDto(
            section,
            branch?.Name,
            await _circuits.ListAsync(ct),
            await _users.ListUsersBySectionAsync(section.Id, ct));
    }
}
