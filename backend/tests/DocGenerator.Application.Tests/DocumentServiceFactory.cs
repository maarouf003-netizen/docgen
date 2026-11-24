using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مصنع اختباري يعزل مواقع بناء `DocumentService` (مشروع الاختبارات فقط):
/// كل مواقع الاستدعاء تستخدم معاملات مسماة/اختيارية، فيبقى أي تقليص لاحق للبواني
/// داخل جسم المصنع فقط لا في مواقع الاستدعاء.
/// المستودعات مغلفات عديمة الحالة فوق `DbContext` نفسه فسلوك المثيلات الجديدة
/// مكافئ — ما يُمرَّر صراحةً يُستخدم كما هو (كمشاركة `HeadAlertService` مع خدمة
/// شقيقة أو مستودعات الدوائر/الفروع أو مصنّف مخصص).
/// </summary>
public static class DocumentServiceFactory
{
    public static (IDocumentService Documents, IExecutionActionService Actions, IDocumentSearchService Search, IDocumentStatusService Status, DocumentStatusHandlers StatusCommands, ICommandHandler<UpdateDocumentCommand, DocumentResponse?> UpdateDocument, ICommandHandler<DeleteDocumentCommand, bool> DeleteDocument, ICommandHandler<TransferDocumentCommand, DocumentResponse> TransferDocument, ICommandHandler<TransferAllDocumentsCommand, int> TransferAllDocuments, ICommandHandler<SaveDocumentBaseNumbersCommand, bool> SaveBaseNumbers) Create(
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
            documentsValue,
            uowValue,
            audit,
            clockValue,
            timeZoneValue);

        var appealsValue = appeals ?? new AppealRepository(db);
        var searchService = new DocumentSearchService(
            documentsValue,
            appealsValue,
            audit,
            Microsoft.Extensions.Options.Options.Create(exportOptionsValue),
            clockValue,
            timeZoneValue,
            new DocGenerator.Infrastructure.Security.KeysetCursorCodec(new JwtOptions
            {
                Secret = "test-keyset-secret-0123456789abcdef",
            }));

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
            dbErrorsValue,
            documentsValue,
            usersValue,
            registrationDatesValue,
            occurrencesValue,
            uowValue,
            txValue,
            audit,
            alertServiceValue,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), txContext),
            clockValue,
            timeZoneValue,
            numberRegistry: numberRegistry);

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

        var documentService = new DocumentService(
            documentsValue,
            usersValue,
            baseNumbersValue,
            registrationDatesValue,
            uowValue,
            txValue,
            audit,
            clockValue,
            timeZoneValue,
            actionService,
            delegationService,
            statusService,
            writeGuard,
            numberRegistry,
            idempotency,
            circuits);

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

        return (documentService, actionService, searchService, statusService, statusCommands, updateDocument, deleteDocument, transferDocument, transferAllDocuments, saveBaseNumbers);
    }
}

/// <summary>معالجات انتقالات حالة المستند (`P4-01bd`) فوق سياق واحد ومصانع الخدمة نفسها.</summary>
public sealed record DocumentStatusHandlers(
    ICommandHandler<UpdateDocumentStatusCommand, bool> UpdateStatus,
    ICommandHandler<RevertDocumentStatusCommand, bool> RevertStatus,
    ICommandHandler<ReturnFromReferredToStartCommand, bool> ReturnFromReferredToStart,
    ICommandHandler<ConsiderExecutedByDelegationCommand, bool> ConsiderExecutedByDelegation,
    ICommandHandler<UpdateExecutedStatusCommand, bool> UpdateExecutedStatus,
    ICommandHandler<RestoreStruckOffDocumentCommand, bool> RestoreStruckOff);
