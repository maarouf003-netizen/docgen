using System.Security.Claims;
using DocGenerator.Api.Authorization;
using DocGenerator.Api.Authorization.Documents;
using DocGenerator.Api.Controllers;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Options;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Interventions;
using DocGenerator.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DocGenerator.Api.Tests;

/// <summary>
/// انحدار F-01: نقاط المتدخلين الثلاث تفحص الوصول عبر الإسقاط الخفيف
/// <see cref="DocumentAccessInfo"/> ولا تستدعي التحميل الكامل للمستند في المتحكم.
/// يثبت الاختبار أن <c>GetAsync</c> لا يُستدعى أبدًا، وأن الحالات 404/403/200 محفوظة،
/// وأن حلّ المورد انتقل إلى سياسة <c>IDocumentAuthorization</c> (P2-01a) وليس المتحكم.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DocumentAccessProjectionTests
{
    private const int UserId = 7;

    [Fact]
    public async Task AddIntervention_UsesAccessProjection_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService();
        var add = new StubHandler<AddDocumentInterventionCommand, DocumentInterventionDto>(null!);
        var authorization = new StubDocumentAuthorization
        {
            AccessInfo = new DocumentAccessInfo(10, UserId, 3, null, null),
        };
        var controller = CreateController(documents, authorization, add);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.True(add.Called);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task AddIntervention_OtherLawyer_Forbids_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService();
        var add = new StubHandler<AddDocumentInterventionCommand, DocumentInterventionDto>(null!);
        var authorization = new StubDocumentAuthorization
        {
            AccessInfo = new DocumentAccessInfo(10, UserId + 1, 3, null, null),
        };
        var controller = CreateController(documents, authorization, add);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.False(add.Called);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task AddIntervention_MissingDocument_ReturnsNotFound_AndNeverLoadsFullDocument()
    {
        var documents = new StubDocumentService();
        var add = new StubHandler<AddDocumentInterventionCommand, DocumentInterventionDto>(null!);
        var authorization = new StubDocumentAuthorization { AccessInfo = null };
        var controller = CreateController(documents, authorization, add);

        var result = await controller.AddIntervention(
            10, new DocumentInterventionUpsertRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(add.Called);
        Assert.False(documents.GetAsyncCalled);
    }

    [Fact]
    public async Task UpdateAndDeleteIntervention_UseAccessProjection_AndNeverLoadFullDocument()
    {
        var documents = new StubDocumentService();
        var update = new StubHandler<UpdateDocumentInterventionCommand, DocumentInterventionDto?>(null);
        var delete = new StubHandler<DeleteDocumentInterventionCommand, bool>(false);
        var authorization = new StubDocumentAuthorization
        {
            AccessInfo = new DocumentAccessInfo(10, UserId, 3, null, null),
        };
        var controller = CreateController(
            documents, authorization,
            add: new StubHandler<AddDocumentInterventionCommand, DocumentInterventionDto>(null!),
            update: update, delete: delete);

        await controller.UpdateIntervention(
            10, 5, new DocumentInterventionUpsertRequest(), CancellationToken.None);
        await controller.DeleteIntervention(10, 5, CancellationToken.None);

        Assert.True(update.Called);
        Assert.True(delete.Called);
        Assert.False(documents.GetAsyncCalled);
    }

    private static DocumentsController CreateController(
        StubDocumentService documents,
        StubDocumentAuthorization authorization,
        ICommandHandler<AddDocumentInterventionCommand, DocumentInterventionDto>? add = null,
        ICommandHandler<UpdateDocumentInterventionCommand, DocumentInterventionDto?>? update = null,
        ICommandHandler<DeleteDocumentInterventionCommand, bool>? delete = null)
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
            null!,
            null!,
            null!,
            Options.Create(new DocumentGenerationOptions()),
            null!,
            null!,
            null!,
            null!,
            null!,
            authorization,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            delete!,
            add!,
            update!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            ApiTestClock.Provider,
            TimeZoneInfo.Utc)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
            },
        };
    }

    /// <summary>
    /// بديل صريح: أي استدعاء للتحميل الكامل أو لحلّ المورد من المتحكم يفشل الاختبار فورًا
    /// (حلّ المورد ملك السياسة).
    /// </summary>
    private sealed class StubDocumentService : IDocumentService
    {
        public bool GetAsyncCalled { get; private set; }

        public Task<DocumentResponse?> GetAsync(int documentId, CancellationToken ct = default)
        {
            GetAsyncCalled = true;
            throw new InvalidOperationException(
                "F-01: يجب ألا يستدعي المتحكم التحميل الكامل للمستند في مسار فحص الوصول");
        }

        public Task<DocumentAccessInfo?> GetAccessInfoAsync(
            int documentId, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "P2-01a: حلّ المورد ملك سياسة IDocumentAuthorization لا المتحكم");

        public Task<DocumentResponse?> GetDeletedAsync(int documentId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<DocumentResponse> CreateAsync(DocumentUpsertRequest request, int userId, string? actorName, int? branchId, CancellationToken ct = default, string? idempotencyKey = null)
            => throw new NotSupportedException();
        public Task<DocumentResponse?> UpdateAsync(int documentId, DocumentUpsertRequest request, string? actorName, int? userId = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int documentId, string? actorName, CancellationToken ct = default)
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

    /// <summary>
    /// بديل معالج أمر: يثبت أن المتحكم فوّض العملية إلى الأمر (P4-01) بعد فحص الوصول،
    /// وأن الحالات 404/403/200 محفوظة.
    /// </summary>
    private sealed class StubHandler<TCommand, TResult>(TResult result)
        : ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        public bool Called { get; private set; }

        public Task<TResult> HandleAsync(TCommand command, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// بديل السياسة في اختبار الوحدة: يثبت أن المتحكم يترجم نتيجة السياسة فقط
    /// (200/403/404)، وأن قاعدة النطاق نفسها تُختبر في <c>DocumentAccessHandlerTests</c>
    /// و<b>لا</b> تُكرَّر هنا.
    /// </summary>
    private sealed class StubDocumentAuthorization : IDocumentAuthorization
    {
        public DocumentAccessInfo? AccessInfo { get; init; }

        public Task<DocumentAccessOutcome> AuthorizeAsync(
            ClaimsPrincipal user, int documentId, DocumentAction action, CancellationToken ct = default)
        {
            if (AccessInfo is null)
            {
                return Task.FromResult(DocumentAccessOutcome.NotFound);
            }

            var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Task.FromResult(AccessInfo.CreatedById == userId
                ? DocumentAccessOutcome.Allowed
                : DocumentAccessOutcome.Forbidden);
        }

        public Task<DocumentAccessOutcome> AuthorizeAsync(
            ClaimsPrincipal user, DocumentResponse document, DocumentAction action, CancellationToken ct = default)
            => Task.FromResult(DocumentAccessOutcome.Allowed);

        public bool Has(ClaimsPrincipal user, UserCapability capability) => false;

        public DocumentListScope? ResolveListScope(ClaimsPrincipal user) => null;
    }
}
