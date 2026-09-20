using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات حتمية لمصنّف أخطاء القيد (B3): تعارض فريد حقيقي على SQLite يُصنَّف
/// موجبًا، وغير الفريد سالبًا — يثبت نصف مسار الترجمة (النصف الآخر: موضع الالتقاط
/// حول حفظ الحجوزات، مثبت بالمراجعة وجولات السلامة).
/// </summary>
public class DbExceptionClassifierTests : IDisposable
{
    private readonly DocGeneratorDbContext _db = TestDb.Create();
    private readonly IDbExceptionClassifier _classifier = new DbExceptionClassifier();

    public void Dispose() => _db.Dispose();

    private async Task<(int SourceId, int DelegationId)> SeedAsync()
    {
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var lawyer = new User
        {
            Username = "classifier_lawyer",
            FullName = "محامي المصنف",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        _db.Users.Add(lawyer);
        await _db.SaveChangesAsync();

        var source = new Document
        {
            CreatedById = lawyer.Id,
            BranchId = branch.Id,
            IsDraft = false,
        };
        _db.Documents.Add(source);
        await _db.SaveChangesAsync();

        var delegation = new DocumentDelegation
        {
            SourceDocumentId = source.Id,
            CreatedById = lawyer.Id,
            DelegatedCourt = "دائرة تنفيذ حلب",
            Status = DelegationStatusCatalog.PendingHead,
        };
        _db.DocumentDelegations.Add(delegation);
        await _db.SaveChangesAsync();

        return (source.Id, delegation.Id);
    }

    [Fact]
    public async Task IsUniqueViolation_DuplicateReservation_True()
    {
        // تعارض فريد حقيقي من المزود (لا محاكاة): صفّان بنفس (المنيب + الأصل).
        var (sourceId, delegationId) = await SeedAsync();
        _db.DelegationAssetReservations.Add(new DelegationAssetReservation
        {
            DelegationId = delegationId,
            SourceDocumentId = sourceId,
            AssetId = 5,
        });
        await _db.SaveChangesAsync();

        _db.DelegationAssetReservations.Add(new DelegationAssetReservation
        {
            DelegationId = delegationId,
            SourceDocumentId = sourceId,
            AssetId = 5,
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());

        Assert.True(_classifier.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_NonUniqueDbError_False()
    {
        var ex = new DbUpdateException("فشل حفظ", new InvalidOperationException("سبب داخلي غير فريد"));
        Assert.False(_classifier.IsUniqueViolation(ex));
    }

    [Fact]
    public void IsUniqueViolation_PlainException_False()
    {
        Assert.False(_classifier.IsUniqueViolation(new InvalidOperationException("عطل عام")));
    }
}
