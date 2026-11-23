using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Branches;

/// <summary>إنشاء فرع جديد — يملك معاملته وتدقيقه وإبطال كاش الفروع.</summary>
public sealed record CreateBranchCommand(CreateBranchRequest Request, string? ActorName)
    : ICommand<BranchDto>, ITransactionalRequest;

public sealed class CreateBranchCommandHandler : CommandHandlerBase<CreateBranchCommand, BranchDto>
{
    private readonly IBranchRepository _branches;
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogger _audit;
    private readonly IReferenceDataCache? _cache;

    public CreateBranchCommandHandler(
        IBranchRepository branches,
        IUnitOfWork uow,
        IAuditLogger audit,
        IEnumerable<IPipelineBehavior<CreateBranchCommand, BranchDto>> behaviors,
        IReferenceDataCache? cache = null)
        : base(behaviors)
    {
        _branches = branches;
        _uow = uow;
        _audit = audit;
        _cache = cache;
    }

    protected override async Task<BranchDto> HandleCoreAsync(CreateBranchCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var name = BranchManagementService.NormalizeRequired(request.Name, "اسم الفرع مطلوب");
        var code = BranchManagementService.NormalizeRequired(request.Code, "رمز الفرع مطلوب");
        // المحافظة إلزامية: تُشتق الدوائر من محافظة الفرع، وفرعٌ بلا محافظة يُعطّل الإنابة.
        var governorate = BranchManagementService.NormalizeRequired(
            request.Governorate, "المحافظة مطلوبة — تُشتق دوائر الفرع من محافظتها");
        if (!GovernorateCatalog.IsGovernorate(governorate))
            throw new ArgumentException("المحافظة غير معتمدة — اختر محافظة من القائمة المعتمدة");

        if (await _branches.NameExistsAsync(name, null, ct))
            throw new ArgumentException("اسم الفرع مستخدم مسبقًا");
        if (await _branches.CodeExistsAsync(code, null, ct))
            throw new ArgumentException("رمز الفرع مستخدم مسبقًا");

        var branch = new Branch
        {
            Name = name,
            Code = code,
            Address = BranchManagementService.NormalizeOptional(request.Address),
            Phone = BranchManagementService.NormalizeOptional(request.Phone),
            Governorate = governorate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _branches.AddAsync(branch, ct);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Branches);
        await _audit.LogAsync(command.ActorName, "create_branch",
            details: $"أنشأ الفرع: {branch.Name} ({branch.Code})", ct: ct);

        return BranchManagementService.ToDto(branch, 0, 0);
    }
}
