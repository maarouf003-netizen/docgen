using System.Net;
using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
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
        public Task<List<DelegationDto>> ListForDocumentAsync(int documentId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SyncTargetsTransferredAsync(IReadOnlyCollection<int> targetDocumentIds, int newLawyerId, string? actorName, int? actorUserId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<DelegationDto>> ListPendingForHeadAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> CountPendingForHeadAsync(int branchId, int? ownerSectionId = null, bool rejectedOnly = false, CancellationToken ct = default) => throw new NotImplementedException();
        public TargetEditReport ValidateTargetEdit(Document doc, DocumentUpsertRequest request) => throw new NotImplementedException();
        public Task ValidateSeizureLockAsync(Document doc, DocumentUpsertRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task SyncSnapshotsAsync(Document doc, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<MirrorNotification>> MirrorSourceEditsAsync(Document doc, CancellationToken ct) => throw new NotImplementedException();
        public Task FireMirrorNotificationsAsync(IReadOnlyList<MirrorNotification> notifications, CancellationToken ct) => throw new NotImplementedException();
        public Task RecordLocalAdditionsAndFireReportAsync(Document target, TargetEditReport report, CancellationToken ct) => throw new NotImplementedException();
        public Task FireStatusChangeAlertsAsync(Document doc, DelegationStatusAlertKind kind, CancellationToken ct) => throw new NotImplementedException();
    }

    [Fact]
    public async Task PendingCount_HeadWithoutBranch_Forbidden()
    {
        // رئيس يمرّ بفحص الدور (`CanApproveDelegations`) فيبلغ حارس الفرع —
        // اعتماد `_documents` غير مستخدم في هذا المسار فيُمرَّر `null!` صراحة.
        var controller = new DelegationsController(
            new FakeDelegationService(), null!, new DocGenerator.Api.Authorization.UserCapabilities(),
            null!, null!, null!, null!, null!, null!, null!, null!, null!);
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

        // D11 الموحّد (2026-11-17): العدّاد قراءة ⇒ 403 لا 400، مع رمز العقد الموحّد.
        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal((int)HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(forbidden.Value));
        Assert.Equal("branch_required", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("دون فرع", json.RootElement.GetProperty("message").GetString());
    }
}
