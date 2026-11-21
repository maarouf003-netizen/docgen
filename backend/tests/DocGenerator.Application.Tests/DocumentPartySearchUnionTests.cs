using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حارس `P3-02`: `UNION ALL` لمجموعات الأطراف يجب ألا يُكرّر مستندًا يطابق أكثر من فرع،
/// ويجب أن يبقى داخل نطاق الفرع/المستخدم كما كان مسند `OR` السابق.
/// </summary>
public sealed class DocumentPartySearchUnionTests : PostgresDbTest
{
    [PostgresFact]
    public async Task Search_MatchInSeveralBranches_ReturnsDocumentOnce_AndKeepsBranchScope()
    {
        var damascus = new Branch { Name = "فرع اتحاد البحث", Code = "UNQ1" };
        var aleppo = new Branch { Name = "فرع اتحاد البحث الثاني", Code = "UNQ2" };
        _db.Branches.AddRange(damascus, aleppo);
        await _db.SaveChangesAsync();

        var lawyer = new User
        {
            Username = "union_search_lawyer",
            FullName = "محامي اختبار الاتحاد",
            Role = UserRole.Lawyer,
            BranchId = damascus.Id,
            PasswordHash = "x",
        };
        _db.Users.Add(lawyer);
        await _db.SaveChangesAsync();

        var inScope = NewDocument(lawyer.Id, damascus.Id);
        var outOfScope = NewDocument(lawyer.Id, aleppo.Id);
        _db.Documents.AddRange(inScope, outOfScope);
        await _db.SaveChangesAsync();

        var repository = new DocumentRepository(_db);
        var (total, items) = await repository.SearchAsync(
            query: "أحمد الحلبي",
            status: null,
            applicant: null,
            court: null,
            lawyer: null,
            branch: null,
            administrativeBranch: null,
            executedEntity: null,
            publicEntityBranch: null,
            visibleBranchId: damascus.Id,
            visibleUserId: null,
            page: 1,
            perPage: 20);

        // المستند يطابق `SearchText` واسم المقترض واسم الكفيل معًا؛ `UNION ALL` لا يكرّره.
        Assert.Equal(1, total);
        Assert.Single(items);
        Assert.Equal(inScope.Id, items[0].Id);
        Assert.DoesNotContain(items, d => d.Id == outOfScope.Id);
    }

    private static Document NewDocument(int createdById, int branchId)
    {
        var document = new Document
        {
            CreatedById = createdById,
            BranchId = branchId,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "عبد الله",
            BorrowerFamily = "الحلبي",
            SearchText = "احمد عبد الله الحلبي",
            AmountNumeric = 1000,
            Currency = "ليرة سورية",
            ExecStatus = string.Empty,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
        };

        document.Guarantors.Add(new Guarantor
        {
            GuarantorNumber = 1,
            GuarantorName = "أحمد",
            GuarantorFather = "عبد الله",
            GuarantorFamily = "الحلبي",
        });

        return document;
    }
}
