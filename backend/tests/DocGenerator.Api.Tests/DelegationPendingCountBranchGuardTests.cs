using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس عدّاد طلبات الإنابة لرئيس بلا فرع: رفض صريح بدل نطاق مفتوح. يُختبر بهوية
/// مطالبات مصنوعة وخدمة مزيفة، لأن قيد القاعدة يجعل تجسيد الصف الشاذ عبر EF
/// مستحيلًا. (تغيير سلوكي صفري هنا — الحارس قائم؛ النقل للوحدة لاستحالة التكامل.)
/// </summary>
public sealed class DelegationPendingCountBranchGuardTests
{
    private sealed class FakeDelegationService : IDocumentDelegationService
    {
        public Task<DelegationDto> CreateAsync(int sourceDocumentId, UpsertDelegationRequest request, int userId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DelegationDto?> UpdateAsync(int delegationId, UpsertDelegationRequest request, int userId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int delegationId, int userId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<DelegationDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<DelegationDto>> ListPendingForHeadAsync(int branchId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountPendingForHeadAsync(int branchId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> IsPartyAsync(int delegationId, int userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DelegationDto?> AssignAsync(int delegationId, AssignDelegationRequest request, int userId, int? headBranchId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DelegationDto?> RegisterAsync(int delegationId, RegisterDelegationRequest request, int userId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DelegationDto?> CompleteAsync(int delegationId, CompleteDelegationRequest request, int userId, string? actorName, CancellationToken ct = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task PendingCount_HeadWithoutBranch_BadRequest()
    {
        // رئيس يمرّ بفحص الدور (`CanApproveDelegations`) فيبلغ حارس الفرع —
        // اعتماد `_documents` غير مستخدم في هذا المسار فيُمرَّر `null!` صراحة.
        var controller = new DelegationsController(new FakeDelegationService(), null!);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Name, "headless"),
            new Claim(ClaimTypes.Role, "head"),
        }, "test"));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };

        var result = await controller.PendingCount(CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
