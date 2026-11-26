using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.ExecutionCircuits;

/// <summary>
/// إنشاء دائرة تنفيذ — يملك معاملته وتدقيقه وإبطال كاش الدوائر، ومالك الدائرة
/// يُشتق من دور المنشئ (قسم/شعبة) كما الأصل.
/// </summary>
public sealed record CreateExecutionCircuitCommand(
    int BranchId,
    int ActorUserId,
    string? ActorName,
    string Name) : ICommand<ExecutionCircuitDto>, ITransactionalRequest;

public sealed class CreateExecutionCircuitCommandHandler
    : CommandHandlerBase<CreateExecutionCircuitCommand, ExecutionCircuitDto>
{
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Section> _sections;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IReferenceDataCache? _cache;

    public CreateExecutionCircuitCommandHandler(
        IRepository<ExecutionCircuit> circuits,
        IRepository<Branch> branches,
        IRepository<Section> sections,
        IUserRepository users,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<CreateExecutionCircuitCommand, ExecutionCircuitDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _circuits = circuits;
        _branches = branches;
        _sections = sections;
        _users = users;
        _uow = uow;
        _audit = audit;
        _dbErrors = dbErrors;
        _cache = cache;
    }

    protected override async Task<ExecutionCircuitDto> HandleCoreAsync(
        CreateExecutionCircuitCommand command, CancellationToken ct)
    {
        ExecutionCircuitRules.RequireBranchWithGovernorate(
            await _branches.GetByIdAsync(command.BranchId, ct));
        var trimmed = ExecutionCircuitRules.NormalizeName(command.Name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الدائرة مطلوب");

        // ملكية المنشئ (§8.4): دائرة رئيس الشعبة تلحق بشعبته تلقائيًا، ودائرة
        // رئيس القسم ملك القسم (`null`)؛ وغير الرؤساء مرفوض (الإدارة تنقل فقط).
        var ownerSection = await ExecutionCircuitRules.ResolveOwnerSectionAsync(
            command.BranchId, command.ActorUserId, _users, _sections, ct);

        var circuit = new ExecutionCircuit
        {
            BranchId = command.BranchId,
            SectionId = ownerSection?.Id,
            Name = trimmed,
            NameNorm = norm,
            IsActive = true,
            CreatedById = command.ActorUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        try
        {
            // فحص ودّي قبل الإدراج لبيان المالك في الرسالة؛ القيد الفريد في القاعدة
            // يبقى الحارس النهائي ضد السباق (والالتقاط أدناه يغطي هذه الحالة النادرة).
            var existing = ExecutionCircuitRules.FindCircuitByName(
                await _circuits.ListAsync(ct), command.BranchId, norm, excludeCircuitId: null);
            if (existing is not null)
                throw new DocumentConflictException(
                    await ExecutionCircuitRules.DuplicateCircuitMessageAsync(
                        existing, ownerSection?.Id, _sections, ct));
            await _circuits.AddAsync(circuit, ct);
            await _uow.SaveChangesAsync(ct);
            _cache?.Invalidate(ReferenceDataGroups.Circuits);
            await _audit.LogAsync(command.ActorName, "create_circuit", null, null,
                $"أدخل دائرة تنفيذ «{trimmed}» في فرعه", ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            // سباق حقيقي بين فحصين متزامنين: الرسالة العامة تحمل القيد الفرعي نفسه.
            throw new DocumentConflictException(
                $"الدائرة «{trimmed}» موجودة مسبقًا في فرعك — لا يمكن تكرار اسم الدائرة داخل الفرع", ex);
        }
        return new ExecutionCircuitDto(circuit.Id, circuit.BranchId, null, circuit.Name, circuit.IsActive, 0, 0, circuit.Version,
            circuit.SectionId, ownerSection?.Name);
    }
}
