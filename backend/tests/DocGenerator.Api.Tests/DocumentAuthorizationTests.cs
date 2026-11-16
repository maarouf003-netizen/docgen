using System.Security.Claims;
using DocGenerator.Api.Authorization.Documents;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P2-01a — واجهة التفويض الموردي: اشتقاق نطاق القوائم لكل دور، وترجمة نتيجة
/// التقييم إلى <c>NotFound</c>/<c>Forbidden</c>/<c>Allowed</c> بترتيب «حلّ المورد
/// قبل التقييم» (`ADR-0007`).
/// </summary>
[Trait("Category", "Unit")]
public sealed class DocumentAuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Lawyer, 3, 7, null, false)]
    [InlineData(UserRole.Head, 3, null, null, false)]
    [InlineData(UserRole.SubHead, 3, null, 2, false)]
    [InlineData(UserRole.Manager, null, null, null, false)]
    [InlineData(UserRole.Admin, null, null, null, false)]
    [InlineData(UserRole.SubHead, 3, null, null, true)] // رئيس شعبة بلا شعبة في الرمز.
    public void ResolveListScope_MatchesTheDecidedScopes(
        UserRole role,
        int? expectedBranch,
        int? expectedUser,
        int? expectedSection,
        bool expectForbidden)
    {
        var sectionId = role == UserRole.SubHead && expectForbidden ? (int?)null : 2;
        var user = UserCapabilitiesTests.Principal(role, branchId: 3, sectionId: sectionId);
        var authorization = CreateAuthorization(new StubReader(null), allow: true);

        var scope = authorization.ResolveListScope(user);

        if (expectForbidden)
        {
            Assert.Null(scope);
            return;
        }

        Assert.NotNull(scope);
        Assert.Equal(expectedBranch, scope.VisibleBranchId);
        Assert.Equal(expectedUser, scope.VisibleUserId);
        Assert.Equal(expectedSection, scope.OwnerSectionId);
    }

    [Fact]
    public async Task MissingDocument_ReturnsNotFound_BeforeAnyAuthorizationCall()
    {
        var authorizationService = new StubAuthorizationService(allow: false);
        var authorization = CreateAuthorization(new StubReader(null), authorizationService);

        var outcome = await authorization.AuthorizeAsync(
            UserCapabilitiesTests.Principal(UserRole.Lawyer), 10, DocumentAction.Read);

        Assert.Equal(DocumentAccessOutcome.NotFound, outcome);
        Assert.Null(authorizationService.LastResource);
    }

    [Theory]
    [InlineData(true, DocumentAccessOutcome.Allowed)]
    [InlineData(false, DocumentAccessOutcome.Forbidden)]
    public async Task LoadedDocument_MapsTheAuthorizationResult(
        bool allow, DocumentAccessOutcome expected)
    {
        var document = new DocumentAccessInfo(10, 7, 3, null, null);
        var authorization = CreateAuthorization(
            new StubReader(document), new StubAuthorizationService(allow));

        var outcome = await authorization.AuthorizeAsync(
            UserCapabilitiesTests.Principal(UserRole.Lawyer), 10, DocumentAction.Edit);

        Assert.Equal(expected, outcome);
    }

    /// <summary>
    /// الحمل الثقيل يُحوَّل إلى الإسقاط الخمسة حقول نفسها (F-01) بلا قراءة إضافية،
    /// والمعالج يستلم الإسقاط المطابق للاستجابة.
    /// </summary>
    [Fact]
    public async Task LoadedResponse_IsMappedToTheFiveFieldProjection()
    {
        var authorizationService = new StubAuthorizationService(allow: true);
        var authorization = CreateAuthorization(new StubReader(null), authorizationService);
        var response = new DocumentResponse
        {
            Id = 21,
            CreatedById = 9,
            BranchId = 4,
            ExecutionCircuitId = 6,
            SectionId = 3,
        };

        var outcome = await authorization.AuthorizeAsync(
            UserCapabilitiesTests.Principal(UserRole.Manager), response, DocumentAction.Read);

        Assert.Equal(DocumentAccessOutcome.Allowed, outcome);
        var context = Assert.IsType<DocumentAccessContext>(authorizationService.LastResource);
        Assert.Equal(new DocumentAccessInfo(21, 9, 4, 6, 3), context.Document);
    }

    [Fact]
    public void Has_DelegatesToTheCatalog()
    {
        var authorization = CreateAuthorization(new StubReader(null), allow: true);

        Assert.True(authorization.Has(
            UserCapabilitiesTests.Principal(UserRole.Manager), UserCapability.HasFullAccess));
        Assert.False(authorization.Has(
            UserCapabilitiesTests.Principal(UserRole.Lawyer), UserCapability.HasFullAccess));
    }

    private static DocumentAuthorization CreateAuthorization(
        IDocumentAccessReader reader, bool allow)
        => CreateAuthorization(reader, new StubAuthorizationService(allow));

    private static DocumentAuthorization CreateAuthorization(
        IDocumentAccessReader reader, StubAuthorizationService authorizationService)
        => new(authorizationService, new UserCapabilities(), reader);

    private sealed class StubReader(DocumentAccessInfo? accessInfo) : IDocumentAccessReader
    {
        public Task<DocumentAccessInfo?> GetAccessInfoAsync(int documentId, CancellationToken ct = default)
            => Task.FromResult(accessInfo);

        public Task<bool> HasForwardedAppealAsync(int documentId, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> IsAssignedFollowerAsync(int documentId, int userId, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    private sealed class StubAuthorizationService(bool allow) : IAuthorizationService
    {
        public object? LastResource { get; private set; }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            LastResource = resource;
            return Task.FromResult(allow
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user, object? resource, string policyName)
            => throw new NotSupportedException("السياسة الموردية لا تستخدم سياسة اسمية.");
    }
}
