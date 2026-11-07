using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// فرادة ترتيب الملحق داخل الملف: الفهرس الفريد (DocumentId, Sequence) يمنع تكرار
/// الترتيب في الملف نفسه، ويسمح بتكراره بين ملفات مختلفة. لا علاقة له بتكرار اسم
/// الممثل الشرعي (المخزَّن في Heirs/ExecutedHeirs) عبر الورثة أو الملفات.
/// </summary>
public class DocumentAnnexTests
{
    [Fact]
    public async Task UniqueIndex_RejectsDuplicateSequenceWithinSameDocument()
    {
        using var db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        var user = EntityBuilders.NewUser("annex_u1", "annex_u1", UserRole.Lawyer, branch.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var doc = new Document { CreatedById = user.Id, BranchId = branch.Id };
        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        db.DocumentAnnexes.Add(new DocumentAnnex
        {
            DocumentId = doc.Id,
            Sequence = 1,
            AnnexNumber = "A-1",
        });
        db.DocumentAnnexes.Add(new DocumentAnnex
        {
            DocumentId = doc.Id,
            Sequence = 1,
            AnnexNumber = "A-2",
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SameSequenceAcrossDifferentDocuments_IsAllowed()
    {
        using var db = TestDb.Create();
        var branch = new Branch { Name = "حلب", Code = "ALP", Governorate = "حلب" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        var user = EntityBuilders.NewUser("annex_u2", "annex_u2", UserRole.Lawyer, branch.Id);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var first = new Document { CreatedById = user.Id, BranchId = branch.Id };
        var second = new Document { CreatedById = user.Id, BranchId = branch.Id };
        db.Documents.AddRange(first, second);
        await db.SaveChangesAsync();

        db.DocumentAnnexes.Add(new DocumentAnnex
        {
            DocumentId = first.Id,
            Sequence = 1,
            AnnexNumber = "A-1",
        });
        db.DocumentAnnexes.Add(new DocumentAnnex
        {
            DocumentId = second.Id,
            Sequence = 1,
            AnnexNumber = "B-1",
        });

        await db.SaveChangesAsync();
        Assert.Equal(2, await db.DocumentAnnexes.CountAsync());
    }
}
