using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
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
        var name = BranchProjection.NormalizeRequired(request.Name, "اسم الفرع مطلوب");
        var code = BranchProjection.NormalizeRequired(request.Code, "كود الفرع مطلوب");
        // المحافظة إجبارية لأنها تحدد نطاق رئيس القسم في سجل الجهات العامة (Branches.Governorate).
        var governorate = BranchProjection.NormalizeRequired(
            request.Governorate, "المحافظة مطلوبة — اختر محافظة الفرع من القائمة");
        if (!GovernorateCatalog.IsGovernorate(governorate))
            throw new ArgumentException("المحافظة غير معتمدة — اختر محافظة الفرع من القائمة");

        if (await _branches.NameExistsAsync(name, null, ct))
            throw new ArgumentException("اسم الفرع مستخدم مسبقاً");
        if (await _branches.CodeExistsAsync(code, null, ct))
            throw new ArgumentException("كود الفرع مستخدم مسبقاً");

        var branch = new Branch
        {
            Name = name,
            Code = code,
            Address = BranchProjection.NormalizeOptional(request.Address),
            Phone = BranchProjection.NormalizeOptional(request.Phone),
            Governorate = governorate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        await _branches.AddAsync(branch, ct);
        await _uow.SaveChangesAsync(ct);
        _cache?.Invalidate(ReferenceDataGroups.Branches);
        await _audit.LogAsync(command.ActorName, "create_branch",
            details: $"أنشأ فرعاً: {branch.Name} ({branch.Code})", ct: ct);

        return BranchProjection.ToDto(branch, 0, 0);
    }
}
