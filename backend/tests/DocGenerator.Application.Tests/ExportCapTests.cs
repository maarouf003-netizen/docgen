using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سقف التصدير: يُرفض التصدير الواسع بعدّ مسبق قبل جلب أي صف إلى الذاكرة،
/// ويُسمح عند الحد تمامًا وتحته — حماية ذاكرة الخادم دون تغيير شكل النتائج.
/// </summary>
public class ExportCapTests : IDisposable
{
    private readonly DocGeneratorDbContext _db = TestDb.Create();
    private readonly FakeAuditLogger _audit = new();
    private int _userId;

    public void Dispose() => _db.Dispose();

    private IDocumentService Build(int maxRows)
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

        return new DocumentService(
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db),
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new DelegationRepository(_db),
            new AppealRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            Options.Create(new ExportOptions { MaxRows = maxRows }));
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

    [Fact]
    public async Task ExportAsync_WhenResultsExceedCap_ThrowsWithFriendlyMessage()
    {
        var svc = Build(maxRows: 1);
        await CreateDocsAsync(svc, 2);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ExportAsync(null, null, null, null, null, null, null, null, null));

        Assert.Contains("الحد الأقصى للتصدير", ex.Message);
        Assert.Contains("طبّق فلترًا أضيق", ex.Message);
    }

    [Fact]
    public async Task ExportAsync_AtExactCap_Passes()
    {
        var svc = Build(maxRows: 2);
        await CreateDocsAsync(svc, 2);

        var rows = await svc.ExportAsync(null, null, null, null, null, null, null, null, null);

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task ExportAsync_UnderCap_Passes()
    {
        var svc = Build(maxRows: 10);
        await CreateDocsAsync(svc, 1);

        var rows = await svc.ExportAsync(null, null, null, null, null, null, null, null, null);

        Assert.Single(rows);
    }
}
