using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.UseCases.Correspondences;

/// <summary>مراسلة برسائلها وتوثيق مشاهداتها — بعد التحقق من حق الوصول (نطاق الشعبة §10).</summary>
public sealed record GetCorrespondenceQuery(
    int Id,
    int ActorUserId,
    UserRole Role,
    int? ActorBranchId,
    int? ActorSectionId = null) : IQuery<CorrespondenceDto>;

internal sealed class GetCorrespondenceQueryHandler
    : QueryHandlerBase<GetCorrespondenceQuery, CorrespondenceDto>
{
    private readonly ICorrespondenceRepository _letters;
    private readonly IDocumentRepository _documents;
    private readonly IRepository<ExecutionCircuit> _circuits;
    private readonly IBranchRepository _branches;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public GetCorrespondenceQueryHandler(
        ICorrespondenceRepository letters,
        IDocumentRepository documents,
        IRepository<ExecutionCircuit> circuits,
        IBranchRepository branches,
        TimeProvider clock,
        TimeZoneInfo timeZone,
        IEnumerable<IPipelineBehavior<GetCorrespondenceQuery, CorrespondenceDto>> behaviors)
        : base(behaviors)
    {
        _letters = letters;
        _documents = documents;
        _circuits = circuits;
        _branches = branches;
        _clock = clock;
        _timeZone = timeZone;
    }

    protected override async Task<CorrespondenceDto> HandleCoreAsync(
        GetCorrespondenceQuery query, CancellationToken ct)
    {
        var letter = await _letters.GetByIdWithDetailsAsync(query.Id, ct)
            ?? throw new KeyNotFoundException("المراسلة غير موجودة");

        if (!await CorrespondenceService.CanViewAsync(
                letter, query.ActorUserId, query.Role, query.ActorBranchId, query.ActorSectionId,
                _documents, _circuits, _branches, ct))
            throw new UnauthorizedAccessException("لا تملك صلاحية الاطلاع على هذه المراسلة");

        return CorrespondenceService.ToDto(letter, query.ActorUserId, _clock, _timeZone);
    }
}
