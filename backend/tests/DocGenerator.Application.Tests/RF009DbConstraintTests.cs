using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// المرحلة 3 (3.1): إثبات أن قيد التفرد يُرفَض من القاعدة نفسها —
/// إدخال مباشر عبر <c>DbContext</c> يتجاوز فحص الخدمة المسبق، فيبقى
/// الفهرس الفريد (`CourtNorm` + الرقم + النوع + السنة، بفلتر `NOT IsDeleted`)
/// هو خط الدفاع الأخير. بلا هجرات جديدة.
/// </summary>
public class RF009DbConstraintTests : IDisposable
{
    private readonly Infrastructure.Persistence.DocGeneratorDbContext _db = TestDb.Create();

    public RF009DbConstraintTests()
    {
        // بذر أدنى لعبور مفاتيح FK (المنشئ/الفرع) — القيد المفحوص هو الفريد وحده.
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = "seed",
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private static Document Numbered(string norm, string number, bool deleted = false) => new()
    {
        CourtNorm = norm,
        FileNumber = number,
        FileType = "عادي",
        FileYear = "2026",
        IsDeleted = deleted,
        CreatedById = 1,
        BranchId = 1,
    };

    [Fact]
    public async Task DuplicateActiveNumber_DirectInsert_ThrowsDbUpdateException()
    {
        _db.Documents.Add(Numbered("دمشق", "99111"));
        await _db.SaveChangesAsync();

        _db.Documents.Add(Numbered("دمشق", "99111"));
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    [Fact]
    public async Task DeletedDuplicate_DirectInsert_Succeeds()
    {
        // فلتر القيد يستثني المحذوف: إعادة استعمال رقم محذوف مسموحة.
        _db.Documents.Add(Numbered("دمشق", "99222", deleted: true));
        await _db.SaveChangesAsync();

        _db.Documents.Add(Numbered("دمشق", "99222"));
        await _db.SaveChangesAsync();
    }
}
