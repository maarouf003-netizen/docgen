using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Appeals;
using DocGenerator.Application.UseCases.Delegations;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// يبني PortalService كامل الاعتماديات (بما فيها معالجا قراءة الاستئنافات والإنابة
/// المعاد استعمالهما في تفاصيل البوابة) على قاعدة اختبارية واحدة، ليُصار إليه من كل
/// اختبارات البوابة بدل تكرار ربط الكائنات.
/// </summary>
public static class PortalServiceFactory
{
    public static IPortalService Create(DocGeneratorDbContext db, IAuditLogger audit, int maxRows = 10_000, TimeProvider? clock = null)
    {
        var documents = new DocumentRepository(db);

        var appealReads = new ListAppealsForDocumentQueryHandler(
            documents,
            new AppealRepository(db),
            TestClock.Provider,
            TestClock.TimeZone,
            Array.Empty<IPipelineBehavior<ListAppealsForDocumentQuery, List<AppealDto>>>());

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
            appealReads,
            delegations,
            new ExcelExportService(),
            audit,
            Options.Create(new ExportOptions { MaxRows = maxRows }),
            clock ?? TestClock.Provider,
            TestClock.TimeZone);
    }
}
