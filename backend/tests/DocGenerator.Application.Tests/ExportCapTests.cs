using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Documents;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سقف التصدير: يُرفض التصدير الواسع بعدّ مسبق قبل جلب أي صف إلى الذاكرة،
/// ويُسمح عند الحد تمامًا وتحته — حماية ذاكرة الخادم دون تغيير شكل النتائج.
/// </summary>
public class ExportCapTests : PostgresDbTest
{
    private readonly FakeAuditLogger _audit = new();
    private int _userId;


    private (IDocumentService Documents, IExecutionActionService Actions, IDocumentSearchService Search, IDocumentStatusService Status, ICommandHandler<UpdateDocumentCommand, DocumentResponse?> UpdateDocument, ICommandHandler<DeleteDocumentCommand, bool> DeleteDocument, ICommandHandler<TransferDocumentCommand, DocumentResponse> TransferDocument, ICommandHandler<TransferAllDocumentsCommand, int> TransferAllDocuments) Build(int maxRows)
    {
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي أول",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        _userId = _db.Users.First(u => u.Username == "lawyer1").Id;

        return DocumentServiceFactory.Create(_db, _audit, exportOptions: new ExportOptions { MaxRows = maxRows });
    }

    private async Task CreateDocsAsync(IDocumentService svc, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var request = new DocumentUpsertRequest
            {
                BorrowerName = $"مقترض {i}",
                Guarantors = new List<GuarantorDto>(),
                Assets = new List<AssetDto>(),
                BorrowerHeirs = new List<HeirDto>(),
                ExecutionApplicants = new List<ExecutionApplicantDto>(),
                ExecutedPublicEntities = new List<ExecutedPublicEntityDto>(),
                ExecutedNaturalPersons = new List<ExecutedNaturalPersonDto>(),
            };
            await svc.CreateAsync(request, _userId, "tester", branchId: 1);
        }
    }

    [PostgresFact]
    public async Task ExportAsync_WhenResultsExceedCap_ThrowsWithFriendlyMessage()
    {
        var bundle = Build(maxRows: 1);
        await CreateDocsAsync(bundle.Documents, 2);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            bundle.Search.ExportAsync(null, null, null, null, null, null, null, null, null));

        Assert.Contains("الحد الأقصى للتصدير", ex.Message);
        Assert.Contains("طبّق فلترًا أضيق", ex.Message);
    }

    [PostgresFact]
    public async Task ExportAsync_AtExactCap_Passes()
    {
        var bundle = Build(maxRows: 2);
        await CreateDocsAsync(bundle.Documents, 2);

        var rows = await bundle.Search.ExportAsync(null, null, null, null, null, null, null, null, null);

        Assert.Equal(2, rows.Count);
    }

    [PostgresFact]
    public async Task ExportAsync_UnderCap_Passes()
    {
        var bundle = Build(maxRows: 10);
        await CreateDocsAsync(bundle.Documents, 1);

        var rows = await bundle.Search.ExportAsync(null, null, null, null, null, null, null, null, null);

        Assert.Single(rows);
    }
}
