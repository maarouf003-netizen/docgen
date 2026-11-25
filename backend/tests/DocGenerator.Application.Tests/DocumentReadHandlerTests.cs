using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Application.UseCases.Documents;

namespace DocGenerator.Application.Tests;

/// <summary>
/// `P4-01cq`: توصيل معالجَي القراءة المفردة الجديدين (`GetDocumentQuery`/
/// `GetDocumentAccessInfoQuery`) — المفقود يعيد `null` بلا استثناء، وهو العقد الذي
/// يعتمد عليه المتحكم في ترجمة `404`.
/// </summary>
public sealed class DocumentReadHandlerTests : PostgresDbTest
{
    [PostgresFact]
    public async Task GetDocument_Missing_ReturnsNull()
    {
        var reads = DocumentServiceFactory.Create(_db, new FakeAuditLogger()).Reads;

        var result = await reads.GetDocument.HandleAsync(new GetDocumentQuery(999_999));

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task GetAccessInfo_Missing_ReturnsNull()
    {
        var reads = DocumentServiceFactory.Create(_db, new FakeAuditLogger()).Reads;

        var result = await reads.GetAccessInfo.HandleAsync(new GetDocumentAccessInfoQuery(999_999));

        Assert.Null(result);
    }
}
