using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
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
    public static (IDocumentService Documents, IExecutionActionService Actions, IDocumentSearchService Search, IDocumentStatusService Status) Create(
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
        var clockValue = clock ?? TimeProvider.System;
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
            uowValue,
            txValue,
            audit);

        var actionService = new ExecutionActionService(
            actionsValue,
            documentsValue,
            uowValue,
            txValue,
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
            timeZoneValue);

        var delegationsValue = delegations ?? new DelegationRepository(db);
        var registrationDatesValue = registrationDates ?? new Repository<DocumentRegistrationDate>(db);
        var occurrencesValue = occurrences ?? new Repository<DocumentOccurrence>(db);
        var baseNumbersValue = baseNumbers ?? new Repository<DocumentBaseNumber>(db);
        var writeGuard = new DocumentWriteGuard(
            documentsValue,
            dbErrorsValue,
            audit,
            occurrencesValue,
            uowValue,
            circuits);
        var txContext = txValue as ITransactionContext
            ?? throw new InvalidOperationException(
                "DocumentServiceFactory يتطلب ITransactionRunner يُنفّذ ITransactionContext.");
        var delegationService = new DocumentDelegationService(
            delegationsValue,
            new DelegationReservationRepository(db),
            dbErrorsValue,
            documentsValue,
            usersValue,
            branches ?? new Repository<Branch>(db),
            registrationDatesValue,
            occurrencesValue,
            uowValue,
            txValue,
            audit,
            alertServiceValue,
            new HeadAlertOutbox(new HeadAlertOutboxStore(db), txContext),
            clockValue,
            timeZoneValue);

        var statusService = new DocumentStatusService(
            documentsValue,
            occurrencesValue,
            baseNumbersValue,
            alertServiceValue,
            delegationsValue,
            txValue,
            uowValue,
            clockValue,
            timeZoneValue,
            delegationService,
            writeGuard);

        var documentService = new DocumentService(
            documentsValue,
            usersValue,
            baseNumbersValue,
            registrationDatesValue,
            appealsValue,
            uowValue,
            txValue,
            audit,
            clockValue,
            timeZoneValue,
            actionService,
            delegationService,
            statusService,
            writeGuard,
            idempotency,
            circuits);

        return (documentService, actionService, searchService, statusService);
    }
}
