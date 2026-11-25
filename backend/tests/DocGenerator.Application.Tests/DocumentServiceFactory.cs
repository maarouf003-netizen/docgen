using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Application.UseCases.ExecutionActions;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مصنع اختباري يعزل مواقع بناء معالجات المستند وخدماته المساندة (مشروع الاختبارات فقط):
/// كل مواقع الاستدعاء تستخدم معاملات مسماة/اختيارية، فيبقى أي تقليص لاحق للبواني
/// داخل جسم المصنع فقط لا في مواقع الاستدعاء.
/// المستودعات مغلفات عديمة الحالة فوق `DbContext` نفسه فسلوك المثيلات الجديدة
/// مكافئ — ما يُمرَّر صراحةً يُستخدم كما هو (كمشاركة `HeadAlertService` مع خدمة
/// شقيقة أو مستودعات الدوائر/الفروع أو مصنّف مخصص).
/// </summary>
public static class DocumentServiceFactory
{
    public static (DocumentReadHandlers Reads, ExecutionActionHandlers Actions, DocumentSearchHandlers Search, IDocumentStatusService Status, DocumentStatusHandlers StatusCommands, ICommandHandler<UpdateDocumentCommand, DocumentResponse?> UpdateDocument, ICommandHandler<DeleteDocumentCommand, bool> DeleteDocument, ICommandHandler<TransferDocumentCommand, DocumentResponse> TransferDocument, ICommandHandler<TransferAllDocumentsCommand, int> TransferAllDocuments, ICommandHandler<SaveDocumentBaseNumbersCommand, bool> SaveBaseNumbers, ICommandHandler<CreateDocumentCommand, DocumentResponse> CreateDocument) Create(
        DocGeneratorDbContext db,
        IAuditLogger audit,
        TimeProvider? clock = null,
        TimeZoneInfo? timeZone = null,
        ExportOptions? exportOptions = null,
        IDbExceptionClassifier? dbErrors = null,
        IDocumentRepository? documents = null,
        IUserRepository? users = null,
        IRepository<ExecutionAction>? actions = null,
        IRepository<DocumentBaseNumber>? baseNumbers = null,
        IRepository<DocumentRegistrationDate>? registrationDates = null,
        IRepository<DocumentOccurrence>? occurrences = null,
        IDelegationRepository? delegations = null,
        IAppealRepository? appeals = null,
        IHeadAlertService? alertService = null,
        IUnitOfWork? uow = null,
        ITransactionRunner? tx = null,
        IRepository<ExecutionCircuit>? circuits = null,
        IRepository<Branch>? branches = null,
        IIdempotencyStore? idempotency = null)
    {
        var clockValue = clock ?? TestClock.Provider;
        var timeZoneValue = timeZone ?? TestClock.TimeZone;
        var exportOptionsValue = exportOptions ?? new ExportOptions();
        var dbErrorsValue = dbErrors ?? new DbExceptionClassifier();
        var documentsValue = documents ?? new DocumentRepository(db);
        var usersValue = users ?? new UserRepository(db);
        var actionsValue = actions ?? new Repository<ExecutionAction>(db);
        var uowValue = uow ?? new UnitOfWork(db);
        var txValue = tx ?? new TransactionRunner(db);
        var alertServiceValue = alertService ?? new HeadAlertService(
            new HeadAlertRepository(db),
            documentsValue,
            usersValue,
            branches ?? new Repository<Branch>(db),
            new DelegationRepository(db),
            uowValue,
            txValue,
            audit);

        var actionService = new ExecutionActionService(
            actionsValue,
            uowValue,
            audit,
            clockValue,
            timeZoneValue);
        var actionHandlers = new ExecutionActionHandlers(
            actionService,
            new GetExecutionActionsQueryHandler(
                documentsValue,
                Array.Empty<IPipelineBehavior<GetExecutionActionsQuery, List<ExecutionActionDto>>>()));

        var appealsValue = appeals ?? new AppealRepository(db);
        var cursorCodec = new DocGenerator.Infrastructure.Security.KeysetCursorCodec(new JwtOptions
        {
            Secret = "test-keyset-secret-0123456789abcdef",
        });
        var search = new DocumentSearchHandlers(
            new SearchDocumentsQueryHandler(
                documentsValue, appealsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchDocumentsQuery, PagedResult<DocumentResponse>>>()),
            new SearchDocumentListQueryHandler(
                documentsValue, appealsValue, cursorCodec, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchDocumentListQuery, PagedResult<DocumentListItemDto>>>()),
            new SearchDeletedDocumentsQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchDeletedDocumentsQuery, PagedResult<DocumentResponse>>>()),
            new SearchDeletedDocumentListQueryHandler(
                documentsValue, cursorCodec, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchDeletedDocumentListQuery, PagedResult<DocumentListItemDto>>>()),
            new SearchStruckOffDocumentsQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchStruckOffDocumentsQuery, PagedResult<DocumentResponse>>>()),
            new SearchStruckOffDocumentListQueryHandler(
                documentsValue, cursorCodec, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchStruckOffDocumentListQuery, PagedResult<DocumentListItemDto>>>()),
            new SearchExecutedDocumentsQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchExecutedDocumentsQuery, PagedResult<DocumentResponse>>>()),
            new SearchExecutedDocumentListQueryHandler(
                documentsValue, cursorCodec, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchExecutedDocumentListQuery, PagedResult<DocumentListItemDto>>>()),
            new SearchReferredToStartDocumentsQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchReferredToStartDocumentsQuery, PagedResult<DocumentResponse>>>()),
            new SearchReferredToStartDocumentListQueryHandler(
                documentsValue, cursorCodec, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<SearchReferredToStartDocumentListQuery, PagedResult<DocumentListItemDto>>>()),
            new ExportDocumentsQueryHandler(
                documentsValue, audit, Microsoft.Extensions.Options.Options.Create(exportOptionsValue),
                clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<ExportDocumentsQuery, List<DocumentExportRow>>>()),
            new GetDocumentFilterOptionsQueryHandler(
                documentsValue,
                Array.Empty<IPipelineBehavior<GetDocumentFilterOptionsQuery, DocumentFilterOptions>>()));

        var delegationsValue = delegations ?? new DelegationRepository(db);
        var registrationDatesValue = registrationDates ?? new Repository<DocumentRegistrationDate>(db);
        var occurrencesValue = occurrences ?? new Repository<DocumentOccurrence>(db);
        var baseNumbersValue = baseNumbers ?? new Repository<DocumentBaseNumber>(db);
        var txContext = txValue as ITransactionContext
            ?? throw new InvalidOperationException(
                "DocumentServiceFactory يتطلب ITransactionRunner يُنفّذ ITransactionContext.");
        var baseNumberRepository = new DocumentBaseNumberRepository(db);
        var numberRegistry = new DocumentNumberRegistry(
            documentsValue,
            baseNumberRepository,
            circuits ?? new Repository<ExecutionCircuit>(db),
            uowValue,
            txValue,
            txContext,
            audit,
            new DocGenerator.Infrastructure.Security.NumberWarningTokenService(),
            new NumberRegistryOptions { AllowCircuitlessLegacy = true },
            clockValue,
            timeZoneValue);
        var writeGuard = new DocumentWriteGuard(
            documentsValue,
            dbErrorsValue,
            audit,
            occurrencesValue,
            uowValue,
            circuits);
        var delegationService = new DocumentDelegationService(
            delegationsValue,
            uowValue,
            txValue,
            audit,
            alertServiceValue,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), txContext),
            clockValue,
            timeZoneValue);

        var statusService = new DocumentStatusService(
            occurrencesValue,
            baseNumbersValue,
            clockValue,
            timeZoneValue,
            numberRegistry);

        var statusCommands = new DocumentStatusHandlers(
            new UpdateDocumentStatusCommandHandler(
                documentsValue, delegationsValue, writeGuard, txValue, uowValue, occurrencesValue,
                clockValue, timeZoneValue, delegationService, alertServiceValue,
                Array.Empty<IPipelineBehavior<UpdateDocumentStatusCommand, bool>>()),
            new RevertDocumentStatusCommandHandler(
                documentsValue, delegationsValue, writeGuard, txValue, uowValue, occurrencesValue,
                clockValue, timeZoneValue, delegationService,
                Array.Empty<IPipelineBehavior<RevertDocumentStatusCommand, bool>>()),
            new ReturnFromReferredToStartCommandHandler(
                documentsValue, delegationsValue, writeGuard, txValue, uowValue, occurrencesValue,
                clockValue, timeZoneValue, statusService,
                Array.Empty<IPipelineBehavior<ReturnFromReferredToStartCommand, bool>>()),
            new ConsiderExecutedByDelegationCommandHandler(
                documentsValue, delegationsValue, writeGuard, txValue, uowValue, occurrencesValue,
                clockValue, timeZoneValue, delegationService, alertServiceValue,
                Array.Empty<IPipelineBehavior<ConsiderExecutedByDelegationCommand, bool>>()),
            new UpdateExecutedStatusCommandHandler(
                documentsValue, writeGuard, txValue, uowValue, occurrencesValue, statusService,
                Array.Empty<IPipelineBehavior<UpdateExecutedStatusCommand, bool>>()),
            new RestoreStruckOffDocumentCommandHandler(
                documentsValue, writeGuard, txValue, uowValue, statusService, delegationService,
                clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<RestoreStruckOffDocumentCommand, bool>>()));

        var reads = new DocumentReadHandlers(
            new GetDocumentQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<GetDocumentQuery, DocumentResponse?>>()),
            new GetDocumentAccessInfoQueryHandler(
                documentsValue,
                Array.Empty<IPipelineBehavior<GetDocumentAccessInfoQuery, DocumentAccessInfo?>>()),
            new GetDeletedDocumentQueryHandler(
                documentsValue, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<GetDeletedDocumentQuery, DocumentResponse?>>()),
            new CountFilesByOwnerQueryHandler(
                usersValue, documentsValue,
                Array.Empty<IPipelineBehavior<CountFilesByOwnerQuery, int>>()),
            new GetDocumentRotationListQueryHandler(
                documentsValue, numberRegistry, clockValue, timeZoneValue,
                Array.Empty<IPipelineBehavior<GetDocumentRotationListQuery, PagedResult<RotationDocumentDto>>>()),
            new GetDocumentBaseNumberHistoryQueryHandler(
                numberRegistry,
                Array.Empty<IPipelineBehavior<GetDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>>()),
            new GetDeletedDocumentBaseNumberHistoryQueryHandler(
                numberRegistry,
                Array.Empty<IPipelineBehavior<GetDeletedDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>>>()));

        var createDocument = new CreateDocumentCommandHandler(
            documentsValue,
            usersValue,
            registrationDatesValue,
            uowValue,
            txValue,
            audit,
            actionService,
            statusService,
            writeGuard,
            numberRegistry,
            clockValue,
            timeZoneValue,
            Array.Empty<IPipelineBehavior<CreateDocumentCommand, DocumentResponse>>(),
            idempotency);

        var updateDocument = new UpdateDocumentCommandHandler(
            documentsValue,
            registrationDatesValue,
            uowValue,
            txValue,
            audit,
            actionService,
            delegationService,
            statusService,
            writeGuard,
            numberRegistry,
            clockValue,
            timeZoneValue,
            Array.Empty<IPipelineBehavior<UpdateDocumentCommand, DocumentResponse?>>());

        var deleteDocument = new DeleteDocumentCommandHandler(
            documentsValue,
            appealsValue,
            uowValue,
            txValue,
            audit,
            numberRegistry,
            Array.Empty<IPipelineBehavior<DeleteDocumentCommand, bool>>());

        var transferDocument = new TransferDocumentCommandHandler(
            usersValue,
            documentsValue,
            delegationService,
            audit,
            txValue,
            clockValue,
            timeZoneValue,
            Array.Empty<IPipelineBehavior<TransferDocumentCommand, DocumentResponse>>());

        var transferAllDocuments = new TransferAllDocumentsCommandHandler(
            usersValue,
            documentsValue,
            delegationService,
            audit,
            txValue,
            clockValue,
            timeZoneValue,
            Array.Empty<IPipelineBehavior<TransferAllDocumentsCommand, int>>(),
            idempotency);

        var saveBaseNumbers = new SaveDocumentBaseNumbersCommandHandler(
            numberRegistry,
            Array.Empty<IPipelineBehavior<SaveDocumentBaseNumbersCommand, bool>>());

        return (reads, actionHandlers, search, statusService, statusCommands, updateDocument, deleteDocument, transferDocument, transferAllDocuments, saveBaseNumbers, createDocument);
    }
}

/// <summary>
/// معالجات قراءة المستند (`P4-01cq`) فوق سياق واحد ومصانع الخدمة نفسها — تُحقن
/// صراحةً في الاختبارات بدل <c>IDocumentService</c> المحذوفة.
/// </summary>
public sealed record DocumentReadHandlers(
    IQueryHandler<GetDocumentQuery, DocumentResponse?> GetDocument,
    IQueryHandler<GetDocumentAccessInfoQuery, DocumentAccessInfo?> GetAccessInfo,
    IQueryHandler<GetDeletedDocumentQuery, DocumentResponse?> GetDeletedDocument,
    IQueryHandler<CountFilesByOwnerQuery, int> CountFilesByOwner,
    IQueryHandler<GetDocumentRotationListQuery, PagedResult<RotationDocumentDto>> RotationList,
    IQueryHandler<GetDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>> BaseNumberHistory,
    IQueryHandler<GetDeletedDocumentBaseNumberHistoryQuery, List<BaseNumberHistoryDto>> DeletedBaseNumberHistory);

/// <summary>
/// معالجات قراءة البحث/القوائم/التصدير (`P4-01cr`) فوق سياق واحد ومصانع الخدمة نفسها —
/// تُحقن صراحةً في الاختبارات بدل خدمة البحث القديمة المحذوفة.
/// </summary>
public sealed record DocumentSearchHandlers(
    IQueryHandler<SearchDocumentsQuery, PagedResult<DocumentResponse>> Search,
    IQueryHandler<SearchDocumentListQuery, PagedResult<DocumentListItemDto>> SearchList,
    IQueryHandler<SearchDeletedDocumentsQuery, PagedResult<DocumentResponse>> SearchDeleted,
    IQueryHandler<SearchDeletedDocumentListQuery, PagedResult<DocumentListItemDto>> SearchDeletedList,
    IQueryHandler<SearchStruckOffDocumentsQuery, PagedResult<DocumentResponse>> SearchStruckOff,
    IQueryHandler<SearchStruckOffDocumentListQuery, PagedResult<DocumentListItemDto>> SearchStruckOffList,
    IQueryHandler<SearchExecutedDocumentsQuery, PagedResult<DocumentResponse>> SearchExecuted,
    IQueryHandler<SearchExecutedDocumentListQuery, PagedResult<DocumentListItemDto>> SearchExecutedList,
    IQueryHandler<SearchReferredToStartDocumentsQuery, PagedResult<DocumentResponse>> SearchReferredToStart,
    IQueryHandler<SearchReferredToStartDocumentListQuery, PagedResult<DocumentListItemDto>> SearchReferredToStartList,
    IQueryHandler<ExportDocumentsQuery, List<DocumentExportRow>> Export,
    IQueryHandler<GetDocumentFilterOptionsQuery, DocumentFilterOptions> FilterOptions);

/// <summary>معالجات انتقالات حالة المستند (`P4-01bd`) فوق سياق واحد ومصانع الخدمة نفسها.</summary>
public sealed record DocumentStatusHandlers(
    ICommandHandler<UpdateDocumentStatusCommand, bool> UpdateStatus,
    ICommandHandler<RevertDocumentStatusCommand, bool> RevertStatus,
    ICommandHandler<ReturnFromReferredToStartCommand, bool> ReturnFromReferredToStart,
    ICommandHandler<ConsiderExecutedByDelegationCommand, bool> ConsiderExecutedByDelegation,
    ICommandHandler<UpdateExecutedStatusCommand, bool> UpdateExecutedStatus,
    ICommandHandler<RestoreStruckOffDocumentCommand, bool> RestoreStruckOff);
