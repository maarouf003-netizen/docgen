using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// تفعيل/تعطيل دائرة تنفيذ — يملك معاملته وتدقيقه وإبطال كاش الدوائر.
/// نطاق الملكية (قسم/شعبة) والتفاؤلية كما الأصل.
/// </summary>
public sealed record SetExecutionCircuitActiveCommand(
    int CircuitId,
    int BranchId,
    int? OwnerSectionId,
    string? ActorName,
    bool IsActive,
    long? Version) : ICommand<ExecutionCircuitDto>, ITransactionalRequest;

public sealed class SetExecutionCircuitActiveCommandHandler
    : CommandHandlerBase<SetExecutionCircuitActiveCommand, ExecutionCircuitDto>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IDocumentRepository _documents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public SetExecutionCircuitActiveCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IDocumentRepository documents,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<SetExecutionCircuitActiveCommand, ExecutionCircuitDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _documents = documents;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<ExecutionCircuitDto> HandleCoreAsync(
        SetExecutionCircuitActiveCommand command, CancellationToken ct)
    {
        ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(command.BranchId, ct));

        var tracked = await _circuits.GetByIdAsync(command.CircuitId, ct)
            ?? throw new ArgumentException("الدائرة غير موجودة");
        ExecutionCircuitRules.EnsureOwned(tracked, command.BranchId, command.OwnerSectionId);
        if (command.Version is not null && tracked.Version != command.Version.Value)
            throw new DocumentConflictException("تعارض تزامن — أعد تحميل الدائرة وحاول مجددًا");
        tracked.IsActive = command.IsActive;
        tracked.UpdatedAt = DateTime.UtcNow;
        tracked.Version++;
        _circuits.Update(tracked);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Circuits);
        await _audit.LogAsync(command.ActorName, command.IsActive ? "activate_circuit" : "deactivate_circuit",
            null, null, $"{(command.IsActive ? "فعّل" : "عطّل")} دائرة التنفيذ «{tracked.Name}»", ct);
        return await ExecutionCircuitRules.BuildDtoAsync(tracked, _documents, _sections, ct);
    }
}
