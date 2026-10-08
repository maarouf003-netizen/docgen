using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس الفرع في متحكم الدوائر (S6.b): غياب claim الفرع فشل تفويض (403)
/// لا طلب مشوَّه (400). يُختبر بهوية مطالبات مصنوعة لأن تسجيل الدخول نفسه
/// يشترط الفرع، فيستحيل تجسيد الصف الشاذ عبر HTTP.
/// </summary>
public sealed class ExecutionCircuitsBranchGuardTests
{
    private sealed class FakeCircuitService : IExecutionCircuitService
    {
        public Task<List<ExecutionCircuitDto>> ListMineAsync(int branchId, int? ownerSectionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExecutionCircuitDto> CreateAsync(int branchId, int actorUserId, string? actorName, string name, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExecutionCircuitDto> RenameAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, string name, long? version, CancellationToken ct = default, int? actorUserId = null) => throw new NotImplementedException();
        public Task<ExecutionCircuitDto> SetActiveAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, bool isActive, long? version, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(int circuitId, int branchId, int? ownerSectionId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ExecutionCircuitDto>> ListForLawyerAsync(int branchId, int? ownerSectionId, bool limitToOwner, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ExecutionCircuitDto>> ListForDelegationAsync(string? governorate, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ReferCircuitFilesResult> ReferFilesAsync(int sourceCircuitId, int branchId, int? ownerSectionId, int actorUserId, string? actorName, ReferCircuitFilesRequest request, CancellationToken ct = default, string? idempotencyKey = null) => throw new NotImplementedException();
        public Task<List<PendingRegistrationDto>> MyPendingRegistrationsAsync(int lawyerId, int? circuitId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CompleteRegistrationsAsync(int lawyerId, string? actorName, CompleteRegistrationsRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<CircuitStatsDto>> CircuitStatsAsync(int? branchId, int? ownerSectionId, bool fullAccess, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteByPendingRegistrationAsync(int lawyerId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SyncPendingAlertsAfterTransferAsync(int sourceOwnerId, int targetOwnerId, int branchId, int actorUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ExecutionCircuitDto> TransferCircuitAsync(int circuitId, int? targetSectionId, int actorUserId, string? actorName, CancellationToken ct = default, long? version = null) => throw new NotImplementedException();
    }

    private static ExecutionCircuitsController ControllerWith(string role)
    {
        var controller = new ExecutionCircuitsController(new FakeCircuitService(), null!, null!, TimeProvider.System, TimeZoneInfo.Utc);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "branchless"),
            new Claim(ClaimTypes.Role, role),
        }, "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return controller;
    }

    [Fact]
    public async Task Mine_HeadWithoutBranch_Forbids()
    {
        var result = await ControllerWith("head").Mine(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ForLawyer_LawyerWithoutBranch_Forbids()
    {
        var result = await ControllerWith("lawyer").ForLawyer(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ForDelegation_LawyerWithoutBranch_Forbids()
    {
        var result = await ControllerWith("lawyer").ForDelegation(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }
}
