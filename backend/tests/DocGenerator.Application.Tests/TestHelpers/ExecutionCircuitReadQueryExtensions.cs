using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.ExecutionCircuits;

namespace DocGenerator.Application.Tests.TestHelpers;

/// <summary>
/// سجل معالجات قراءات دوائر التنفيذ (`P4-01ct`) فوق سياق واحد ومصانع الخدمة نفسها —
/// يُحقن صراحةً في الاختبارات بدل قراءات الخدمة القديمة.
/// </summary>
public sealed record ExecutionCircuitReadHandlers(
    IQueryHandler<ListMineExecutionCircuitsQuery, List<ExecutionCircuitDto>> Mine,
    IQueryHandler<ListCircuitFilesQuery, List<CircuitFileDto>> Files,
    IQueryHandler<ListExecutionCircuitsForLawyerQuery, List<ExecutionCircuitDto>> ForLawyer,
    IQueryHandler<ListExecutionCircuitsForDelegationQuery, List<ExecutionCircuitDto>> ForDelegation,
    IQueryHandler<ListMyPendingRegistrationsQuery, List<PendingRegistrationDto>> PendingRegistrations,
    IQueryHandler<GetCircuitStatsQuery, List<CircuitStatsDto>> Stats);

/// <summary>
/// جسر اختباري ضيق يحافظ على توقيعات قراءات خدمة الدوائر القديمة فوق سجل المعالجات —
/// منطق الاختبارات كما هو.
/// </summary>
public static class ExecutionCircuitReadQueryExtensions
{
    public static Task<List<ExecutionCircuitDto>> ListMineAsync(
        this ExecutionCircuitReadHandlers handlers,
        int branchId,
        int? ownerSectionId,
        CancellationToken ct = default)
        => handlers.Mine.HandleAsync(new ListMineExecutionCircuitsQuery(branchId, ownerSectionId), ct);

    public static Task<List<CircuitFileDto>> ListFilesAsync(
        this ExecutionCircuitReadHandlers handlers,
        int circuitId,
        int branchId,
        int? ownerSectionId,
        CancellationToken ct = default)
        => handlers.Files.HandleAsync(new ListCircuitFilesQuery(circuitId, branchId, ownerSectionId), ct);

    public static Task<List<ExecutionCircuitDto>> ListForLawyerAsync(
        this ExecutionCircuitReadHandlers handlers,
        int branchId,
        int? ownerSectionId,
        bool limitToOwner,
        CancellationToken ct = default)
        => handlers.ForLawyer.HandleAsync(
            new ListExecutionCircuitsForLawyerQuery(branchId, ownerSectionId, limitToOwner), ct);

    public static Task<List<ExecutionCircuitDto>> ListForDelegationAsync(
        this ExecutionCircuitReadHandlers handlers,
        string? governorate,
        CancellationToken ct = default)
        => handlers.ForDelegation.HandleAsync(new ListExecutionCircuitsForDelegationQuery(governorate), ct);

    public static Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(
        this ExecutionCircuitReadHandlers handlers,
        int lawyerId,
        int? circuitId,
        CancellationToken ct = default)
        => handlers.PendingRegistrations.HandleAsync(
            new ListMyPendingRegistrationsQuery(lawyerId, circuitId), ct);

    public static Task<List<CircuitStatsDto>> CircuitStatsAsync(
        this ExecutionCircuitReadHandlers handlers,
        int? branchId,
        int? ownerSectionId,
        bool fullAccess,
        CancellationToken ct = default)
        => handlers.Stats.HandleAsync(new GetCircuitStatsQuery(branchId, ownerSectionId, fullAccess), ct);
}
