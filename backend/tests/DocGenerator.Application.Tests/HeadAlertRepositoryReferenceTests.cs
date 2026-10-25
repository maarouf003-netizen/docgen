using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة فحص المراجع قبل الإدراج (الخطة §8.1): كل نوع مرجع مفقود يُكتشف قبل إنشاء
/// التنبيه، ووجود المراجع كلها يعيد <c>null</c>.
/// </summary>
public class HeadAlertRepositoryReferenceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly HeadAlertRepository _repository;
    private readonly int _branchId;
    private readonly User _user;

    public HeadAlertRepositoryReferenceTests()
    {
        _db = TestDb.Create();

        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();
        _branchId = branch.Id;

        _user = EntityBuilders.NewUser("ref_user", "مستخدم مراجع", UserRole.Head, _branchId);
        _db.Users.Add(_user);
        _db.SaveChanges();

        _repository = new HeadAlertRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private CreateSystemHeadAlertRequest BaseRequest() => new(
        HeadAlertKindCatalog.DelegationPendingApproval,
        "تنبيه فحص مراجع",
        HeadAlertAudience.SingleHead,
        _branchId,
        _user.Id,
        RecipientUserId: _user.Id);

    [Fact]
    public async Task FindMissingReferenceAsync_MissingBranch_ReturnsBranch()
    {
        var request = BaseRequest() with { BranchId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.Branch,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingDocument_ReturnsDocument()
    {
        var request = BaseRequest() with { DocumentId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.Document,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingDelegation_ReturnsDelegation()
    {
        var request = BaseRequest() with { DelegationId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.Delegation,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingAppeal_ReturnsAppeal()
    {
        var request = BaseRequest() with { AppealId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.Appeal,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingReviewLetter_ReturnsReviewLetter()
    {
        var request = BaseRequest() with { ReviewLetterId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.ReviewLetter,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingPublicEntity_ReturnsPublicEntity()
    {
        var request = BaseRequest() with { PublicEntityId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.PublicEntity,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_MissingExecutionCircuit_ReturnsExecutionCircuit()
    {
        var request = BaseRequest() with { ExecutionCircuitId = 9999 };

        Assert.Equal(
            HeadAlertReferenceKind.ExecutionCircuit,
            await _repository.FindMissingReferenceAsync(request));
    }

    [Fact]
    public async Task FindMissingReferenceAsync_AllReferencesPresent_ReturnsNull()
    {
        var document = new Document
        {
            BranchId = _branchId,
            CreatedById = _user.Id,
            IsDraft = false,
        };
        _db.Documents.Add(document);
        _db.SaveChanges();

        var delegation = new DocumentDelegation
        {
            SourceDocumentId = document.Id,
            CreatedById = _user.Id,
        };
        var appeal = new DocumentAppeal
        {
            DocumentId = document.Id,
            CreatedById = _user.Id,
        };
        var reviewLetter = new ReviewLetter
        {
            BranchId = _branchId,
            CreatedById = _user.Id,
            LetterNumber = "RL-1",
        };
        var group = new PublicEntityGroup
        {
            CanonicalName = "وزارة العدل",
            CanonicalNameNorm = "وزارة العدل",
        };
        _db.AddRange(delegation, appeal, reviewLetter, group);
        _db.SaveChanges();

        var entity = new PublicEntity
        {
            GroupId = group.Id,
            Governorate = "دمشق",
            BranchName = "مديرية العدل",
            CreatedById = _user.Id,
        };
        var circuit = new ExecutionCircuit
        {
            BranchId = _branchId,
            Name = "دائرة التنفيذ الأولى",
            NameNorm = "دائرة التنفيذ الأولى",
            CreatedById = _user.Id,
        };
        _db.AddRange(entity, circuit);
        _db.SaveChanges();

        var request = BaseRequest() with
        {
            DocumentId = document.Id,
            DelegationId = delegation.Id,
            AppealId = appeal.Id,
            ReviewLetterId = reviewLetter.Id,
            PublicEntityId = entity.Id,
            ExecutionCircuitId = circuit.Id,
        };

        Assert.Null(await _repository.FindMissingReferenceAsync(request));
    }
}
