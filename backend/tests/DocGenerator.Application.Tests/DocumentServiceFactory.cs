using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// مصنع اختباري يعزل مواقع بناء `DocumentService` (المرحلة 1أ — مشروع الاختبارات فقط):
/// كل مواقع الاستدعاء تستخدم معاملات مسماة/اختيارية، فيبقى تقليص البواني لاحقًا
/// (المراحل 2–5) داخل جسم المصنع فقط لا في مواقع الاستدعاء.
/// المستودعات مغلفات عديمة الحالة فوق `DbContext` نفسه فسلوك المثيلات الجديدة
/// مكافئ — ما يُمرَّر صراحةً يُستخدم كما هو (كمشاركة `HeadAlertService` مع خدمة
/// شقيقة أو مستودعات الدوائر/الفروع أو مصنّف مخصص).
/// </summary>
public static class DocumentServiceFactory
{
    public static (IDocumentService Documents, IExecutionActionService Actions) Create(
        DocGeneratorDbContext db,
        IAuditLogger audit,
        TimeProvider? clock = null,
        TimeZoneInfo? timeZone = null,
        ExportOptions? exportOptions = null,
        IDbExceptionClassifier? dbErrors = null,
        IDocumentRepository? documents = null,
        IUserRepository? users = null,
        IRepository<Guarantor>? guarantors = null,
        IRepository<Asset>? assets = null,
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

        var documentService = new DocumentService(
            documentsValue,
            usersValue,
            guarantors ?? new Repository<Guarantor>(db),
            assets ?? new Repository<Asset>(db),
            actionsValue,
            baseNumbers ?? new Repository<DocumentBaseNumber>(db),
            registrationDates ?? new Repository<DocumentRegistrationDate>(db),
            occurrences ?? new Repository<DocumentOccurrence>(db),
            delegations ?? new DelegationRepository(db),
            appeals ?? new AppealRepository(db),
            alertServiceValue,
            uowValue,
            txValue,
            audit,
            Microsoft.Extensions.Options.Options.Create(exportOptionsValue),
            clockValue,
            timeZoneValue,
            dbErrorsValue,
            actionService,
            idempotency,
            circuits,
            branches);

        return (documentService, actionService);
    }
}
