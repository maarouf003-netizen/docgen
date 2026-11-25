using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// يبني PortalService كامل الاعتماديات (بما فيها خدمتا الاستئناف والإنابة المعاد
/// استعمالهما في تفاصيل البوابة) على قاعدة اختبارية واحدة، ليُصار إليه من كل
/// اختبارات البوابة بدل تكرار ربط الكائنات.
/// </summary>
public static class PortalServiceFactory
{
    public static IPortalService Create(DocGeneratorDbContext db, IAuditLogger audit, int maxRows = 10_000, TimeProvider? clock = null)
    {
        var documents = new DocumentRepository(db);
        var users = new UserRepository(db);
        var branches = new Repository<Branch>(db);
        var registrationDates = new Repository<DocumentRegistrationDate>(db);
        var occurrences = new Repository<DocumentOccurrence>(db);
        var uow = new UnitOfWork(db);
        var tx = new TransactionRunner(db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(db),
            documents,
            users,
            branches,
            new DelegationRepository(db),
            uow,
            tx,
            audit);
        var headAlertsOutbox = new HeadAlertOutbox(new HeadAlertOutboxStore(db), tx);

        var appeals = new DocumentAppealService(
            new AppealRepository(db),
            documents,
            users,
            uow,
            tx,
            audit,
            headAlerts,
            headAlertsOutbox,
            TestClock.Provider,
            TestClock.TimeZone);

        var delegations = new GetDocumentDelegationsQueryHandler(
            documents,
            new DelegationRepository(db),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<GetDocumentDelegationsQuery, List<DelegationDto>>>());

        return new PortalService(
            new PortalRepository(db),
            new Repository<Document>(db),
            new AppealRepository(db),
            appeals,
            delegations,
            new ExcelExportService(),
            audit,
            Options.Create(new ExportOptions { MaxRows = maxRows }),
            clock ?? TestClock.Provider,
            TestClock.TimeZone);
    }
}
