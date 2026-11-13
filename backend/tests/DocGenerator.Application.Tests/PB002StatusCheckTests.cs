using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات `PB-002` (`INT-015` ← `BQ-035`): قيود `Check` لآلات الحالة —
/// القيمة اليتيمة تُرفَض عند الحفظ. تفشل قبل الإصلاح (نجاح صامت) وتخضر بعده.
/// مباشر `DbContext` حتمي (القيد قاعدي لا خدمي).
/// </summary>
public class PB002StatusCheckTests : IDisposable
{
    private readonly DocGeneratorDbContext _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    private async Task<int> SeedUserAsync()
    {
        // المحامي بلا فرع يخالف قيد `CK_Users_BranchRequiredForBranchRoles` — فرع لازم.
        var branch = new Branch { Name = "فرع الفحص", Code = "CHK", Governorate = "دمشق" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();
        var user = new User { Username = "chk_user", FullName = "فاحص", Role = UserRole.Lawyer, BranchId = branch.Id, PasswordHash = "x" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Document_OrphanStatuses_Rejected()
    {
        var uid = await SeedUserAsync();
        _db.Documents.Add(new Document { CreatedById = uid, ExecStatus = "منفذ بالتسويه" });
        _db.Documents.Add(new Document { CreatedById = uid, ExecSubStatus = "يتيم" });
        _db.Documents.Add(new Document { CreatedById = uid, ExecutedStatus = "يتيم" });
        _db.Documents.Add(new Document { CreatedById = uid, GeneralEntitySide = "يتيم" });

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Document_ValidStatuses_Accepted()
    {
        var uid = await SeedUserAsync();
        foreach (var s in ExecutionStatusCatalog.ValidStatuses.Append(ExecutionStatusCatalog.StateStruckOff))
            _db.Documents.Add(new Document { CreatedById = uid, ExecStatus = s });
        foreach (var s in ExecutionStatusCatalog.ValidSubStatuses)
            _db.Documents.Add(new Document { CreatedById = uid, ExecSubStatus = s });
        foreach (var s in ExecutedStatusCatalog.ValidStatuses)
            _db.Documents.Add(new Document { CreatedById = uid, ExecutedStatus = s });
        foreach (var s in GeneralEntitySideCatalog.ValidSides)
            _db.Documents.Add(new Document { CreatedById = uid, GeneralEntitySide = s });

        await _db.SaveChangesAsync();
    }

    [Fact]
    public void GeneralEntitySide_Constraint_UsesDeterministicOrder()
    {
        // C-08: النص لا يعتمد على ترتيب HashSet غير الحتمي، بل على OrderedSides الصريح.
        var designTimeModel = _db.GetService<IDesignTimeModel>().Model;
        var table = designTimeModel.GetRelationalModel().Tables
            .Single(t => t.Name == "Documents");
        var constraint = table.CheckConstraints
            .Single(c => c.Name == "CK_Documents_GeneralEntitySide");

        Assert.Equal(
            "\"GeneralEntitySide\" IN ('applicant', 'executed', 'deposit')",
            constraint.Sql);
        Assert.Equal(
            new[] { "applicant", "executed", "deposit" },
            GeneralEntitySideCatalog.OrderedSides);
    }

    [Fact]
    public async Task Delegation_OrphanStatus_Rejected()
    {
        var uid = await SeedUserAsync();
        var doc = new Document { CreatedById = uid };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        _db.DocumentDelegations.Add(new DocumentDelegation { SourceDocumentId = doc.Id, CreatedById = uid, Status = "يتيمة" });
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Delegation_ValidStatuses_Accepted()
    {
        var uid = await SeedUserAsync();
        var doc = new Document { CreatedById = uid };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        foreach (var s in DelegationStatusCatalog.ValidStatuses)
            _db.DocumentDelegations.Add(new DocumentDelegation { SourceDocumentId = doc.Id, CreatedById = uid, Status = s });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Appeal_OrphanStatus_Rejected()
    {
        var uid = await SeedUserAsync();
        var doc = new Document { CreatedById = uid };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        _db.DocumentAppeals.Add(new DocumentAppeal { DocumentId = doc.Id, CreatedById = uid, Status = "يتيم" });
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task Appeal_ValidStatuses_Accepted()
    {
        var uid = await SeedUserAsync();
        var doc = new Document { CreatedById = uid };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        foreach (var s in AppealStatusCatalog.ValidStatuses)
            _db.DocumentAppeals.Add(new DocumentAppeal { DocumentId = doc.Id, CreatedById = uid, Status = s });
        await _db.SaveChangesAsync();
    }
}
