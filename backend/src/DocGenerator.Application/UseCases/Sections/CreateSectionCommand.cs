using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.UseCases.Sections;

/// <summary>
/// إنشاء شعبة في فرع — يملك معاملته وتدقيقه وإبطال كاش الشعب، ويحوّل انتهاك
/// الوحدانية إلى `DocumentConflictException` كما الأصل.
/// </summary>
public sealed record CreateSectionCommand(
    int BranchId,
    string? Name,
    string? ActorName) : ICommand<SectionDto>, ITransactionalRequest;

public sealed class CreateSectionCommandHandler
    : CommandHandlerBase<CreateSectionCommand, SectionDto>
{
    private readonly IRepository<Section> _sections;
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IReferenceDataCache? _cache;

    public CreateSectionCommandHandler(
        IRepository<Section> sections,
        IRepository<Branch> branches,
        IUnitOfWork uow,
        IAuditLogger audit,
        IDbExceptionClassifier dbErrors,
        IEnumerable<IPipelineBehavior<CreateSectionCommand, SectionDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _sections = sections;
        _branches = branches;
        _uow = uow;
        _audit = audit;
        _dbErrors = dbErrors;
        _cache = cache;
    }

    protected override async Task<SectionDto> HandleCoreAsync(
        CreateSectionCommand command, CancellationToken ct)
    {
        var branch = await _branches.GetByIdAsync(command.BranchId, ct)
            ?? throw new ArgumentException("الفرع غير موجود");
        var trimmed = SectionProjection.NormalizeName(command.Name);
        var norm = ArabicNameNormalizer.Normalize(trimmed);
        if (string.IsNullOrEmpty(norm))
            throw new ArgumentException("اسم الشعبة مطلوب");

        if (SectionProjection.NameExists(
                await _sections.ListAsync(ct), command.BranchId, norm, excludeId: null))
            throw new ArgumentException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك");

        var section = new Section
        {
            BranchId = command.BranchId,
            Name = trimmed,
            NameNorm = norm,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        try
        {
            await _sections.AddAsync(section, ct);
            await _uow.SaveChangesAsync(ct);
            _cache?.Invalidate(ReferenceDataGroups.Sections);
            await _audit.LogAsync(command.ActorName, "create_section", null, null,
                $"أنشأ شعبة «{trimmed}» في فرع {branch.Name}", ct);
        }
        catch (Exception ex) when (_dbErrors.IsUniqueViolation(ex))
        {
            throw new DocumentConflictException($"الشعبة «{trimmed}» موجودة مسبقًا في فرعك", ex);
        }
        return new SectionDto(section.Id, section.BranchId, branch.Name, section.Name, section.IsActive, 0, null, section.Version);
    }
}
