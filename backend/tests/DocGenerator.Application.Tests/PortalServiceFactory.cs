using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// يبني PortalService — بقيت فيه بعد `P4-01co` قراءة التفاصيل المدقّقة والتصدير
/// المدقّق — على قاعدة اختبارية واحدة، ليُصار إليه من اختبارات البوابة بدل تكرار
/// ربط الكائنات. معالجات القراءات الصرفة يبنيها <see cref="PortalReadHandlerFactory"/>.
/// </summary>
public static class PortalServiceFactory
{
    public static IPortalService Create(DocGeneratorDbContext db, IAuditLogger audit, int maxRows = 10_000, TimeProvider? clock = null)
        => new PortalService(
            new PortalRepository(db),
            new Repository<Document>(db),
            new ExcelExportService(),
            audit,
            Options.Create(new ExportOptions { MaxRows = maxRows }),
            clock ?? TestClock.Provider,
            TestClock.TimeZone);
}
