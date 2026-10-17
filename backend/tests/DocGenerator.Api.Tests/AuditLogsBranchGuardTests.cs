using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حراسة نطاق سجل التدقيق (L-05، 2026-10-17): رئيس القسم بلا رمز فرع يُرفض صراحةً
/// (403) بدل النطاق الإجمالي — تحوّط دفاعي موازٍ لمانع القاعدة
/// (`CK_Users_BranchRequiredForBranchRoles`) وحارس الدخول `login_branch_required`.
/// تُختبر بهوية مطالبات مصنوعة وخدمة مزيفة، لأن قيد القاعدة يجعل تجسيد الصف
/// الشاذ عبر EF مستحيلًا — وهذا مقصود: المتحكم يحرس قرار الرفض، والقاعدة تحرس التخزين.
/// </summary>
public sealed class AuditLogsBranchGuardTests
{
    private sealed class FakeAuditLogService : IAuditLogService
    {
        public int Calls { get; private set; }
        public int? ReceivedScopeBranchId { get; private set; }
        public int? ReceivedScopeSectionId { get; private set; }

        public Task<PagedResult<AuditLogDto>> SearchAsync(
            string? userName, string? actionType, int page, int perPage, CancellationToken ct = default,
            int? scopeBranchId = null, int? scopeSectionId = null)
        {
            Calls++;
            ReceivedScopeBranchId = scopeBranchId;
            ReceivedScopeSectionId = scopeSectionId;
            return Task.FromResult(new PagedResult<AuditLogDto>
            {
                Items = [],
                Page = page,
                PerPage = perPage,
                TotalCount = 0,
            });
        }

        public Task<PagedResult<DocumentChangeGroupDto>> GetDocumentChangesAsync(
            int documentId, int page, int perPage, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private static (AuditLogsController Controller, FakeAuditLogService Service) Build(string role, int? branchId = null, int? sectionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11"),
            new(ClaimTypes.Name, "tester"),
            new(ClaimTypes.Role, role),
        };
        if (branchId.HasValue)
            claims.Add(new Claim("branch_id", branchId.Value.ToString()));
        if (sectionId.HasValue)
            claims.Add(new Claim("section_id", sectionId.Value.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var service = new FakeAuditLogService();
        var controller = new AuditLogsController(service);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal },
        };
        return (controller, service);
    }

    [Fact]
    public async Task Head_WithoutBranchClaim_Forbidden_ServiceNotCalled()
    {
        var (controller, service) = Build("head", branchId: null);

        var result = await controller.Search(userName: null, actionType: null, page: 1, perPage: 20, ct: CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Equal(0, service.Calls);
    }

    [Fact]
    public async Task Head_WithBranch_PassesBranchScopeToService()
    {
        var (controller, service) = Build("head", branchId: 3);

        var result = await controller.Search(userName: null, actionType: null, page: 1, perPage: 20, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal((int)HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(1, service.Calls);
        Assert.Equal(3, service.ReceivedScopeBranchId);
        Assert.Null(service.ReceivedScopeSectionId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(9)]
    public async Task SubHead_SectionScope_MirrorExistingContract(int? sectionId)
    {
        var (controller, service) = Build("subhead", sectionId: sectionId);

        var result = await controller.Search(userName: null, actionType: null, page: 1, perPage: 20, ct: CancellationToken.None);

        if (sectionId.HasValue)
        {
            Assert.Equal(1, service.Calls);
            Assert.Equal(9, service.ReceivedScopeSectionId);
            Assert.Null(service.ReceivedScopeBranchId);
        }
        else
        {
            // F6 (قرار §2.16): شعبة بلا رمز شعبة مرفوضة — سلوك قائم قبل L-05 ويبقى.
            Assert.IsType<ForbidResult>(result.Result);
            Assert.Equal(0, service.Calls);
        }
    }

    [Fact]
    public async Task Manager_WithoutBranch_PassesThroughWithNullScope()
    {
        // الضابط الحدودي: `null` بمعنى «الكل» محفوظة لوصول المدير/المشرف الكامل (تصميم RF-007).
        var (controller, service) = Build("manager");

        var result = await controller.Search(userName: null, actionType: null, page: 1, perPage: 20, ct: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(1, service.Calls);
        Assert.Null(service.ReceivedScopeBranchId);
        Assert.Null(service.ReceivedScopeSectionId);
    }
}
