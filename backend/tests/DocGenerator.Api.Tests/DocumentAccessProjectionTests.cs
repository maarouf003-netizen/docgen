using System.Security.Claims;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Options;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// انحدار F-01: نقاط المتدخلين الثلاث تفحص الوصول عبر الإسقاط الخفيف
/// <see cref="DocumentAccessInfo"/> ولا تستدعي التحميل الكامل للمستند في المتحكم.
/// يثبت الاختبار أن <c>GetAsync</c> لا يُستدعى أبدًا، وأن الحالات 404/403/200 محفوظة.
/// </summary>
public sealed class DocumentAccessProjectionTests
{
    private const int UserId = 7;

    [Fact]
    public async Task AddIntervention_UsesAccessProjection_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService
        {
            AccessInfo = new DocumentAccessInfo(10, UserId, 3, null, null),
        };
        var interventions = new StubInterventionService();
        var controller = CreateController(documents, interventions);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(interventions.AddCalled);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task AddIntervention_OtherLawyer_Forbids_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService
        {
            AccessInfo = new DocumentAccessInfo(10, UserId + 1, 3, null, null),
        };
        var interventions = new StubInterventionService();
        var controller = CreateController(documents, interventions);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.False(interventions.AddCalled);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task AddIntervention_MissingDocument_ReturnsNotFound_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService { AccessInfo = null };
        var interventions = new StubInterventionService();
        var controller = CreateController(documents, interventions);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(interventions.AddCalled);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task UpdateAndDeleteIntervention_UseAccessProjection_AndNeverLoadFullDocument()
    {
        var documents = new StubDocumentService
        {
            AccessInfo = new DocumentAccessInfo(10, UserId, 3, null, null),
        };
        var interventions = new StubInterventionService();
        var controller = CreateController(documents, interventions);

        await controller.UpdateIntervention(
            10, 5, new DocumentInterventionUpsertRequest(), CancellationToken.None);
        await controller.DeleteIntervention(10, 5, CancellationToken.None);

        Assert.True(interventions.UpdateCalled);
        Assert.True(interventions.DeleteCalled);
        Assert.False(documents.GetAsyncCalled);
    }

    private static DocumentsController CreateController(
        StubDocumentService documents, StubInterventionService interventions)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim(ClaimTypes.Role, "lawyer"),
            new Claim("branch_id", "3"),
        ], "test");

        return new DocumentsController(
            documents,
            null!,
            interventions,
            null!,
            null!,
            null!,
            Options.Create(new DocumentGenerationOptions()),
            null!,
            null!,
            null!,
            null!,
            null!,
            TimeProvider.System,
            TimeZoneInfo.Utc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    /// <summary>بديل صريح: أي استدعاء للتحميل الكامل يفشل الاختبار فورًا.</summary>
    private sealed class StubDocumentService : IDocumentService
    {
        public DocumentAccessInfo? AccessInfo { get; init; }
        public bool GetAsyncCalled { get; private set; }

        public Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
        {
            GetAsyncCalled = true;
            throw new InvalidOperationException(
                "F-01: يجب ألا يستدعي المتحكم التحميل الكامل للمستند في مسار فحص الوصول");
        }

        public Task<DocumentAccessInfo?> GetAccessInfoAsync(
            int documentId, CancellationToken ct = default)
            => Task.FromResult(AccessInfo);

        public Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null)
            => throw new NotSupportedException();
        public Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> RestoreAsync(int documentId, string? actorName, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<DocumentResponse> TransferAsync(int documentId, int targetLawyerId, string? actorName, CancellationToken ct = default, int? actorUserId = null)
            => throw new NotSupportedException();
        public Task<int> CountFilesByOwnerAsync(int ownerId, int? scopeBranchId, CancellationToken ct = default, int? ownerSectionId = null)
            => throw new NotSupportedException();
        public Task<int> TransferAllAsync(int sourceLawyerId, int targetLawyerId, int? scopeBranchId, string? actorName, CancellationToken ct = default, string? idempotencyKey = null, int? ownerSectionId = null, int? actorUserId = null)
            => throw new NotSupportedException();
        public Task<PagedResult<RotationDocumentDto>> GetRotationListAsync(int userId, int page, int perPage, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<List<BaseNumberHistoryDto>> GetBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<List<BaseNumberHistoryDto>> GetDeletedBaseNumberHistoryAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task CorrectBaseNumberAsync(int documentId, int year, CorrectBaseNumberRequest request, int userId, string? actorName, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task UndoRotationAsync(int documentId, int year, int userId, string? actorName, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task SaveBaseNumbersAsync(int userId, List<BaseNumberEntry> entries, string? actorName, string? warningToken = null, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubInterventionService : IDocumentInterventionService
    {
        public bool AddCalled { get; private set; }
        public bool UpdateCalled { get; private set; }
        public bool DeleteCalled { get; private set; }

        public Task<DocumentInterventionDto> AddAsync(
            int documentId, DocumentInterventionUpsertRequest request, int userId,
            string? actorName, CancellationToken ct = default)
        {
            AddCalled = true;
            return Task.FromResult<DocumentInterventionDto>(null!);
        }

        public Task<DocumentInterventionDto?> UpdateAsync(
            int documentId, int interventionId, DocumentInterventionUpsertRequest request,
            int userId, string? actorName, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult<DocumentInterventionDto?>(null);
        }

        public Task<bool> DeleteAsync(
            int documentId, int interventionId, string? actorName,
            CancellationToken ct = default)
        {
            DeleteCalled = true;
            return Task.FromResult(false);
        }
    }
}
