using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Application.Tests;

/// <summary>
/// توصيل استعلامات البحث/القوائم/التصدير (`P4-01cr`) في حاوية التطبيق: كل استعلام
/// مسجَّل مرة واحدة بالنوع الدقيق — يحمي خط الأنابيب بعد حذف خدمة البحث القديمة.
/// </summary>
public class DocumentSearchHandlerRegistrationTests
{
    [Fact]
    public void AddApplication_RegistersEveryDocumentSearchQueryHandlerOnce()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var expected = new[]
        {
            typeof(IQueryHandler<SearchDocumentsQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<SearchDocumentListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<SearchDeletedDocumentsQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<SearchDeletedDocumentListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<SearchStruckOffDocumentsQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<SearchStruckOffDocumentListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<SearchExecutedDocumentsQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<SearchExecutedDocumentListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<SearchReferredToStartDocumentsQuery, PagedResult<DocumentResponse>>),
            typeof(IQueryHandler<SearchReferredToStartDocumentListQuery, PagedResult<DocumentListItemDto>>),
            typeof(IQueryHandler<ExportDocumentsQuery, List<DocumentExportRow>>),
            typeof(IQueryHandler<GetDocumentFilterOptionsQuery, DocumentFilterOptions>),
        };

        foreach (var contract in expected)
        {
            var matches = services.Where(d => d.ServiceType == contract).ToList();
            Assert.Single(matches);
        }
    }
}
