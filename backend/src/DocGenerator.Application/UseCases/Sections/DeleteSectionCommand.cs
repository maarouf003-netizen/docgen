using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>
/// حذف شعبة — يملك معاملته وتدقيقه وإبطال كاش الشعب. غياب الشعبة يعيد `false`،
/// والتعطيل/الحذف ممنوعان مع وجود دوائر أو حسابات بالرسائل الأصلية.
/// </summary>
public sealed record DeleteSectionCommand(
    int SectionId,
    string? ActorName) : ICommand<bool>, ITransactionalRequest;

public sealed class DeleteSectionCommandHandler
    : CommandHandlerBase<DeleteSectionCommand, bool>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public DeleteSectionCommandHandler(
        IRepository<Section> sections,
        IRepository<ExecutionCircuit> circuits,
        IUserRepository users,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<DeleteSectionCommand, bool>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _circuits = circuits;
        _users = users;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<bool> HandleCoreAsync(
        DeleteSectionCommand command, CancellationToken ct)
    {
        var section = await _sections.GetByIdAsync(command.SectionId, ct);
        if (section is null)
            return false;
        var circuitCount = (await _circuits.ListAsync(ct)).Count(c => c.SectionId == section.Id);
        if (circuitCount > 0)
            throw new ArgumentException($"لا يمكن حذف الشعبة — انقل دوائرها أولًا (بها {circuitCount} دائرة)");
        var users = await _users.ListUsersBySectionAsync(section.Id, ct);
        if (users.Count > 0)
            throw new ArgumentException("لا يمكن حذف الشعبة — انقل رئيسها/حساباتها أولًا");

        _sections.Remove(section);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Sections);
        await _audit.LogAsync(command.ActorName, "delete_section", null, null,
            $"حذف الشعبة «{section.Name}»", ct);
        return true;
    }
}
