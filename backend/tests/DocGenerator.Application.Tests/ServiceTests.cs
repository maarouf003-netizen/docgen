using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class FakeAuditLogger : IAuditLogger
{
    public List<string> Actions { get; } = new();

    public Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default)
    {
        Actions.Add(actionType);
        return Task.CompletedTask;
    }

    public Task LogManyAsync(IReadOnlyList<AuditLogEntry> entries, CancellationToken ct = default)
    {
        foreach (var entry in entries)
            Actions.Add(entry.ActionType);
        return Task.CompletedTask;
    }
}

public class DocumentServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public DocumentServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var guarantors = new Repository<Guarantor>(_db);
        var estates = new Repository<Asset>(_db);
        var actions = new Repository<ExecutionAction>(_db);
        var baseNumbers = new Repository<DocumentBaseNumber>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(documents, users, guarantors, estates, actions, baseNumbers, registrationDates, occurrences, new DelegationRepository(_db), uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private static DocumentUpsertRequest Sample() => new()
    {
        BorrowerName = "أحمد",
        BorrowerFather = "خالد",
        BorrowerFamily = "الخطيب",
        AmountNumeric = 1250,
        Currency = "ليرة سورية",
        ContractNumber = "12/2024",
        Court = "دمشق",
        Applicant = "المدعي",
        FileNumber = "520",
        FileYear = "2024",
        FileRegistrationDate = "1/1/2024",
        Guarantors = new()
        {
            new GuarantorDto(null, 1, "سمير", "حسن", "علي", null, null, null, null, "حلب", "موطن مختار", null, null, null, null, null, null, new()),
        },
        Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.RealEstate, new List<string> { "المدعى عليه" }, "تمام العقار",
                "بيت", "12345", "المزة", "الصالحية",
                null, null, null, null,
                null, null, null, null, null,
                null,
                null, null, null,
                null),
        },
    };

    [Fact]
    public async Task Create_FillsDerivedFields()
    {
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);

        Assert.True(doc.Id > 0);
        Assert.False(doc.IsDraft);
        Assert.False(string.IsNullOrWhiteSpace(doc.AmountWords));
        Assert.Contains("ليرة سورية", doc.AmountWords!);
        Assert.False(string.IsNullOrWhiteSpace(doc.DocumentType));
        Assert.Contains("أحمد", doc.DocumentType!);
        Assert.Single(doc.Guarantors);
        Assert.Single(doc.Assets);
        Assert.Contains("create", _audit.Actions);
    }

    [Fact]
    public async Task Create_WithThirdBankingAndInclusionAmounts_PersistsAndFillsWords()
    {
        // المصرفي يملك حتى ثلاثة مبالغ (Amount/Amount2/Amount3) والعادي كذلك
        // (InclusionAmount/InclusionAmount2/InclusionAmount3)، وكلماتها تُولَّد تلقائيًا.
        var req = Sample();
        req.Amount2Numeric = 2000m;
        req.Currency2 = "دولار أمريكي";
        req.Amount3Numeric = 300m;
        req.Currency3 = "يورو";
        req.InclusionAmountNumeric = 400m;
        req.InclusionCurrency = "ليرة سورية";
        req.InclusionAmount2Numeric = 500m;
        req.InclusionCurrency2 = "دولار أمريكي";
        req.InclusionAmount3Numeric = 60m;
        req.InclusionCurrency3 = "يورو";

        var doc = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2000m, loaded!.Amount2Numeric);
        Assert.Equal(300m, loaded.Amount3Numeric);
        Assert.Equal("يورو", loaded.Currency3);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Amount3Words));
        Assert.Contains("يورو", loaded.Amount3Words!);
        Assert.Equal(400m, loaded.InclusionAmountNumeric);
        Assert.Equal(500m, loaded.InclusionAmount2Numeric);
        Assert.Equal("دولار أمريكي", loaded.InclusionCurrency2);
        Assert.Equal(60m, loaded.InclusionAmount3Numeric);
        Assert.Equal("يورو", loaded.InclusionCurrency3);
        Assert.False(string.IsNullOrWhiteSpace(loaded.InclusionAmount2Words));
        Assert.False(string.IsNullOrWhiteSpace(loaded.InclusionAmount3Words));
    }

    [Fact]
    public async Task Create_WithoutFileNumber_IsDraft()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var doc = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);

        Assert.True(doc.IsDraft);
        Assert.Contains("تحت رفع", doc.DocumentType!);
    }

    [Fact]
    public async Task Create_WithVeryLongFields_TruncatesSearchTextToMaxLength()
    {
        // SearchText معرف بحد 1000 (HasMaxLength)؛ PostgreSQL يرفض القيم الأطول فوجب القص.
        var req = Sample();
        req.BorrowerName = new string('أ', 800);
        req.BorrowerFamily = new string('ب', 800);
        var created = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);

        var stored = await _db.Documents.AsNoTracking().FirstAsync(d => d.Id == created.Id);
        Assert.True(stored.SearchText!.Length <= 1000);
        Assert.False(string.IsNullOrWhiteSpace(stored.SearchText));
        // ما بعد الحد منصوع فعلًا (يبدأ بحقول المقترض) لا تفريغ كامل.
        Assert.Contains("أ", stored.SearchText);
    }

    [Fact]
    public async Task Update_AddingFileNumberAndYear_BecomesMutadawal()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var draft = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);
        Assert.True(draft.IsDraft);
        Assert.Contains("تحت رفع", draft.DocumentType!);

        var update = Sample();
        update.DocumentType = draft.DocumentType;
        var doc = await _service.UpdateAsync(draft.Id, update, actorName: "lawyer1");

        Assert.False(doc!.IsDraft);
        Assert.StartsWith("متداول", doc.DocumentType!);
        Assert.DoesNotContain("تحت رفع", doc.DocumentType!);
    }

    [Fact]
    public async Task Create_RegisteredWithoutRegistrationDate_Throws()
    {
        var req = Sample();
        req.FileRegistrationDate = null;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ قيد الملف مطلوب", ex.Message);
    }

    [Fact]
    public async Task Create_RegisteredWithInvalidRegistrationDate_Throws()
    {
        var req = Sample();
        req.FileRegistrationDate = "ليس تاريخاً";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("غير صالح", ex.Message);
    }

    [Fact]
    public async Task Create_DraftWithoutRegistrationDate_IsAllowed()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        req.FileRegistrationDate = null;

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.True(doc.IsDraft);
    }

    [Fact]
    public async Task Update_BecomingRegisteredWithoutRegistrationDate_Throws()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var draft = await _service.CreateAsync(req, 1, "lawyer1", 1);
        Assert.True(draft.IsDraft);

        var update = Sample();
        update.FileRegistrationDate = null;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(draft.Id, update, "lawyer1"));
        Assert.Contains("تاريخ قيد الملف مطلوب", ex.Message);
    }

    [Fact]
    public async Task Search_ByQuery_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.BorrowerName = "سعاد";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("أحمد", result.Items[0].BorrowerName);
    }

    [Fact]
    public async Task Search_ByGuarantorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("سمير", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByBorrowerHeirName_FiltersResults()
    {
        var req = Sample();
        req.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "محمود الحلبي", null, null, "أصالة", "عنوان", "المزة"),
        };
        await _service.CreateAsync(req, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("محمود الحلبي", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("أحمد", result.Items[0].BorrowerName);
    }

    [Fact]
    public async Task Search_ByGuarantorHeirName_FiltersResults()
    {
        var req = Sample();
        req.Guarantors[0] = req.Guarantors[0] with
        {
            Heirs = new List<HeirDto>
            {
                new(null, "فارس الخالد", null, null, "أصالة", null, "حلب الجديدة"),
            },
        };
        await _service.CreateAsync(req, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("فارس الخالد", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("أحمد", result.Items[0].BorrowerName);
    }

    [Fact]
    public async Task Search_ByExecutedHeirName_FiltersResults()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants[0] = req.ExecutionApplicants[0] with
        {
            RepresentationType = "إضافة لتركة",
            Heirs = new List<ExecutedHeirDto>
            {
                new(null, "وريث أول", "والد1", "عائلة1", "عنوان", "دمشق"),
            },
        };
        await _service.CreateAsync(req, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("وريث أول", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(GeneralEntitySideCatalog.Executed, result.Items[0].GeneralEntitySide);
    }

    [Fact]
    public async Task Search_ByExecutedHeirTripleName_FiltersResults()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants[0] = req.ExecutionApplicants[0] with
        {
            RepresentationType = "إضافة لتركة",
            Heirs = new List<ExecutedHeirDto>
            {
                new(null, "وريث أول", "والد1", "عائلة1", null, null),
            },
        };
        await _service.CreateAsync(req, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("وريث أول والد1 عائلة1", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_RespectsPagination()
    {
        for (int i = 0; i < 5; i++)
        {
            var req = Sample();
            req.BorrowerName = $"شخص {i}";
            await _service.CreateAsync(req, 1, "lawyer1", 1);
        }

        var page = await _service.SearchAsync(null, null, null, null, null, null, null, null, null, 2, 2);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Search_ByBinaryDebtorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد الخطيب", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByTripleDebtorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد خالد الخطيب", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByContractNumber_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("12/2024", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByCourt_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("دمشق", null, null, null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByApplicantFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Applicant = "مدعي آخر";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, "المدعي", null, null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByCourtFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Court = "حلب";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, "حلب", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByLawyerFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Users.Add(new User
        {
            Username = "lawyer2",
            FullName = "محامي آخر",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();
        await _service.CreateAsync(Sample(), 2, "lawyer2", 1);

        var result = await _service.SearchAsync(null, null, null, null, "محامي آخر", null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("محامي آخر", result.Items[0].Lawyer);
    }

    [Fact]
    public async Task Create_SetsLawyerFromActorFullName()
    {
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);

        Assert.Equal("محامي", doc.Lawyer);
    }

    [Fact]
    public async Task Update_PreservesLawyer()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        Assert.Equal("محامي", created.Lawyer);

        var req = Sample();
        req.Notes = "تعديل لاحق على بيانات الملف";
        var updated = await _service.UpdateAsync(created.Id, req, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("محامي", updated!.Lawyer);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsDistinctApplicantsAndCourts()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Applicant = "مدعي آخر";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var options = await _service.GetFilterOptionsAsync(null, null, null, null, null, null, null, null);
        Assert.Contains("المدعي", options.Applicants);
        Assert.Contains("مدعي آخر", options.Applicants);
        Assert.Contains("دمشق", options.Courts);
        Assert.Contains("محامي", options.Lawyers);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsExecutedEntities()
    {
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var req2 = ExecutedSample();
        req2.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف التجاري", "فرع الشام"),
        };
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var options = await _service.GetFilterOptionsAsync(null, null, null, null, null, null, null, null);
        Assert.Contains("المصرف العقاري", options.ExecutedEntities);
        Assert.Contains("المصرف التجاري", options.ExecutedEntities);
    }

[Fact]
    public async Task Search_ByExecutedEntity_FiltersOnlyPublicEntities()
    {
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var req2 = ExecutedSample();
        req2.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف التجاري", "فرع الشام"),
        };
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, "المصرف العقاري", null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        var doc = Assert.Single(result.Items);
        Assert.Equal("المصرف العقاري", doc.ExecutedPublicEntities[0].EntityName);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsPublicEntityBranches_FromBothSides()
    {
        // جهة عامة منفذ عليها بطبيعة public في عائلة «منفذ عليه».
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        // شخص اعتباري (legal) لا يُدرج فرعه في خيارات «الفرع».
        var req2 = ExecutedSample();
        req2.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "شركة الهدى", "فرع الشام", PartyNatureCatalog.Legal),
        };
        await _service.CreateAsync(req2, 1, "lawyer1", 1);
        // جهة عامة طالبة للتنفيذ في نظام «طالبة تنفيذ».
        var req3 = Sample();
        req3.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "المصرف التجاري السوري", "فرع حلب"),
        };
        await _service.CreateAsync(req3, 1, "lawyer1", 1);

        var options = await _service.GetFilterOptionsAsync(null, null, null, null, null, null, null, null);
        Assert.Contains("فرع المزة", options.PublicEntityBranches);
        Assert.Contains("فرع حلب", options.PublicEntityBranches);
        Assert.DoesNotContain("فرع الشام", options.PublicEntityBranches);
    }

    [Fact]
    public async Task Search_ByPublicEntityBranch_FiltersApplicantAndExecutedBranches()
    {
        // ملف عائلة «منفذ عليه» بفرع «فرع المزة».
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        // ملف «طالبة تنفيذ» بفرع جهة طالبة «فرع حلب».
        var req2 = Sample();
        req2.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "المصرف التجاري السوري", "فرع حلب"),
        };
        await _service.CreateAsync(req2, 1, "lawyer1", 1);
        // ملف آخر بفرع مختلف لا يُطابق.
        var req3 = Sample();
        req3.ApplicantPublicEntities = new()
        {
            new ApplicantPublicEntityDto(null, "مصرف التوفير", "فرع حمص"),
        };
        await _service.CreateAsync(req3, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, null, "فرع المزة", 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(GeneralEntitySideCatalog.Executed, Assert.Single(result.Items).GeneralEntitySide);

        var result2 = await _service.SearchAsync(null, null, null, null, null, null, null, null, "فرع حلب", 1, 20);
        Assert.Equal(1, result2.TotalCount);
        Assert.Equal(GeneralEntitySideCatalog.Applicant, Assert.Single(result2.Items).GeneralEntitySide);
    }

    [Fact]
    public async Task Search_ByPublicEntityBranch_IgnoresLegalNatureBranch()
    {
        // شخص اعتباري (legal) بفرع «فرع الشام» — فرعه لا يُحتسب في فلتر «الفرع».
        var req = ExecutedSample();
        req.ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "شركة الهدى", "فرع الشام", PartyNatureCatalog.Legal),
        };
        await _service.CreateAsync(req, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, null, "فرع الشام", 1, 20);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsFilteredByOtherFilters_NotByOwnFilter()
    {
        // تأكيد أسلوب إكسل: كل قائمة تقيّد بباقي الفلاتر النشطة ما عدا فلتر الحقل نفسه.
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        // فلترة الباقي بدائرة معينة لا تحذف الفرع المطابق في «المصرف العقاري» (دمشق).
        var options = await _service.GetFilterOptionsAsync(null, null, "دمشق", null, null, null, null, "فرع المزة");
        Assert.Contains("فرع المزة", options.PublicEntityBranches);
    }

    [Fact]
    public async Task Search_ReturnsAdministrativeBranchName()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, null, null, 1, 20);
        var doc = Assert.Single(result.Items);
        Assert.Equal("دمشق", doc.AdministrativeBranchName);
    }

    [Fact]
    public async Task Transfer_MovesDocumentToTargetLawyer()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Users.Add(new User
        {
            Username = "lawyer2",
            FullName = "محامي ثانٍ",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        var transferred = await _service.TransferAsync(created.Id, 2, "head1");

        Assert.Equal(2, transferred.CreatedById);
        Assert.Equal("محامي ثانٍ", transferred.Lawyer);
        Assert.Equal("محامي ثانٍ", transferred.CreatedByName);
        Assert.Equal("محامي", transferred.ReferredFromLawyer);
        Assert.NotNull(transferred.ReferredAt);
        Assert.Contains("transfer", _audit.Actions);
    }

    [Fact]
    public async Task Transfer_ToLawyerInAnotherBranch_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Branches.Add(new Branch { Name = "حلب", Code = "ALP" });
        _db.Users.Add(new User
        {
            Username = "lawyer3",
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = 2,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 2, "head1"));
    }

    [Fact]
    public async Task Transfer_ToInactiveLawyer_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Users.Add(new User
        {
            Username = "lawyer4",
            FullName = "محامي موقوف",
            Role = UserRole.Lawyer,
            BranchId = 1,
            IsActive = false,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 2, "head1"));
    }

    [Fact]
    public async Task Transfer_ToCurrentOwner_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 1, "head1"));
    }

    private async Task<int> AddLawyerInBranch1Async(string username, string fullName, bool isActive = true)
    {
        _db.Users.Add(new User
        {
            Username = username,
            FullName = fullName,
            Role = UserRole.Lawyer,
            BranchId = 1,
            IsActive = isActive,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();
        return _db.Users.Single(u => u.Username == username).Id;
    }

    [Fact]
    public async Task TransferAll_MovesAllFilesOfSourceToTarget()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.BorrowerName = "سامي";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);
        var targetId = await AddLawyerInBranch1Async("lawyer2", "محامي ثانٍ");

        var transferred = await _service.TransferAllAsync(1, targetId, scopeBranchId: 1, "head1");

        Assert.Equal(2, transferred);
        var docs = await _db.Documents.AsNoTracking().ToListAsync();
        Assert.Equal(2, docs.Count);
        Assert.All(docs, d => Assert.Equal(targetId, d.CreatedById));
        Assert.All(docs, d => Assert.Equal("محامي ثانٍ", d.Lawyer));
        Assert.All(docs, d => Assert.Equal("محامي", d.ReferredFromLawyer));
        Assert.All(docs, d => Assert.NotNull(d.ReferredAt));
        Assert.Equal(2, _audit.Actions.Count(a => a == "transfer"));
    }

    [Fact]
    public async Task TransferAll_ToLawyerInAnotherBranch_Throws()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Branches.Add(new Branch { Name = "حلب", Code = "ALP" });
        _db.Users.Add(new User
        {
            Username = "lawyer3",
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = 2,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAllAsync(1, 2, scopeBranchId: 1, "head1"));
    }

    [Fact]
    public async Task TransferAll_ToInactiveLawyer_Throws()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await AddLawyerInBranch1Async("lawyer4", "محامي موقوف", isActive: false);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAllAsync(1, 2, scopeBranchId: 1, "head1"));
    }

    [Fact]
    public async Task TransferAll_SourceInAnotherBranch_Throws()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await AddLawyerInBranch1Async("lawyer2", "محامي ثانٍ");

        // نطاق رئيس القسم فرع 2 بينما المحامي المصدر في فرع 1.
        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAllAsync(1, 2, scopeBranchId: 2, "head1"));
    }

    [Fact]
    public async Task TransferAll_SourceIsTarget_Throws()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAllAsync(1, 1, scopeBranchId: 1, "head1"));
    }

    [Fact]
    public async Task TransferAll_NoFiles_ReturnsZero()
    {
        var targetId = await AddLawyerInBranch1Async("lawyer2", "محامي ثانٍ");

        var transferred = await _service.TransferAllAsync(1, targetId, scopeBranchId: 1, "head1");

        Assert.Equal(0, transferred);
        Assert.DoesNotContain("transfer", _audit.Actions);
    }

    [Fact]
    public async Task TransferAll_ExcludesDeletedFiles()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(created.Id, "lawyer1");
        var targetId = await AddLawyerInBranch1Async("lawyer2", "محامي ثانٍ");

        var transferred = await _service.TransferAllAsync(1, targetId, scopeBranchId: 1, "head1");

        Assert.Equal(0, transferred);
        var doc = await _db.Documents.IgnoreQueryFilters().SingleAsync(d => d.Id == created.Id);
        Assert.Equal(1, doc.CreatedById);
        Assert.True(doc.IsDeleted);
    }

    [Fact]
    public async Task CountFilesByOwner_ReturnsOnlyActiveFiles()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(doc.Id, "lawyer1");

        var count = await _service.CountFilesByOwnerAsync(1, scopeBranchId: 1);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CountFilesByOwner_FromAnotherBranch_Throws()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CountFilesByOwnerAsync(1, scopeBranchId: 2));
    }

    [Fact]
    public async Task Update_ChangesBorrower()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req = Sample();
        req.BorrowerName = "محمود";

        var updated = await _service.UpdateAsync(doc.Id, req, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("محمود", updated!.BorrowerName);
        Assert.Contains("update", _audit.Actions);
    }

    [Fact]
    public async Task Create_WithBorrowerAndGuarantorHeirs_PersistsBoth()
    {
        var req = Sample();
        req.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "محمود الحلبي", null, null, "أصالة", "عنوان", "المزة"),
            new(null, "نور الدين", null, null, "أصالة", "وكيل", "المحامي سامر"),
        };
        req.Guarantors[0] = req.Guarantors[0] with
        {
            Heirs = new List<HeirDto>
            {
                new(null, "فارس الخالد", null, null, "أصالة", null, "حلب الجديدة"),
            },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded!.BorrowerHeirs.Count);
        Assert.Contains(reloaded.BorrowerHeirs, h => h.Name == "محمود الحلبي" && h.Address == "المزة" && h.AddressType == "عنوان");
        Assert.Contains(reloaded.BorrowerHeirs, h => h.Name == "نور الدين" && h.AddressType == "وكيل");
        var guarantor = Assert.Single(reloaded.Guarantors);
        var heir = Assert.Single(guarantor.Heirs ?? new List<HeirDto>());
        Assert.Equal("فارس الخالد", heir.Name);
        Assert.Equal("حلب الجديدة", heir.Address);
        Assert.Equal(3, _db.Heirs.Count());
    }

    [Fact]
    public async Task Create_IgnoresHeirsWithBlankNames()
    {
        var req = Sample();
        req.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "   ", null, null, "أصالة", "عنوان", "المزة"),
            new(null, "أحمد العلي", null, null, "أصالة", "وكيل", "وكيل قانوني"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var heir = Assert.Single(reloaded!.BorrowerHeirs);
        Assert.Equal("أحمد العلي", heir.Name);
        Assert.Equal(1, _db.Heirs.Count());
    }

    [Fact]
    public async Task Update_ReplacesHeirsAndNormalizesAddressType()
    {
        var req = Sample();
        req.BorrowerHeirs = new List<HeirDto> { new(null, "الوريث الأول", null, null, "أصالة", "عنوان", "المزة") };
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var update = Sample();
        update.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "الوريث الجديد", null, null, "أصالة", "", null),
            new(null, "الممثل", null, null, "أصالة", "غريب", "وكيل فريد"),
        };

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal(2, updated!.BorrowerHeirs.Count);
        Assert.All(updated.BorrowerHeirs, h => Assert.Equal("عنوان", h.AddressType));
        Assert.DoesNotContain(updated.BorrowerHeirs, h => h.Name == "الوريث الأول");
        Assert.Equal(2, _db.Heirs.Count());
    }

    [Fact]
    public async Task Create_PersistsMultipleOwnersInSelectionOrder()
    {
        var req = Sample();
        req.Assets[0] = req.Assets[0] with
        {
            Owners = new List<string> { "سمير حسن علي", "أحمد خالد الخطيب" },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var estate = Assert.Single(reloaded!.Assets);
        Assert.Equal(new List<string> { "سمير حسن علي", "أحمد خالد الخطيب" }, estate.Owners);
    }

    [Fact]
    public async Task Create_IgnoresBlankAndDuplicateOwnerNames()
    {
        var req = Sample();
        req.Assets[0] = req.Assets[0] with
        {
            Owners = new List<string> { "   ", "أحمد العلي", "أحمد العلي", null!, "محمد خالد" },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var estate = Assert.Single(reloaded!.Assets);
        Assert.Equal(new List<string> { "أحمد العلي", "محمد خالد" }, estate.Owners);
    }

    [Fact]
    public async Task Update_ReplacesOwnersWithNormalizedList()
    {
        var req = Sample();
        req.Assets[0] = req.Assets[0] with { Owners = new List<string> { "المالك الأول" } };
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var update = Sample();
        update.Assets[0] = update.Assets[0] with
        {
            Owners = new List<string> { "المالك الجديد", "المالك المشارك" },
        };

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        var estate = Assert.Single(updated!.Assets);
        Assert.Equal(new List<string> { "المالك الجديد", "المالك المشارك" }, estate.Owners);
    }

    [Fact]
    public async Task Create_WithMultipleOwners_ForcesShareTypeToShareOfShares()
    {
        var req = Sample();
        req.Assets[0] = req.Assets[0] with
        {
            Owners = new List<string> { "سمير حسن علي", "أحمد خالد الخطيب" },
            ShareType = "تمام العقار",
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var estate = Assert.Single(reloaded!.Assets);
        Assert.Equal("حصة سهمية", estate.ShareType);
    }

    [Fact]
    public async Task Create_WithSingleOwner_KeepsChosenShareType()
    {
        var req = Sample();
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var estate = Assert.Single(reloaded!.Assets);
        Assert.Equal("تمام العقار", estate.ShareType);
    }

    [Fact]
    public async Task Create_WithSingleOwnerAndExplicitShare_KeepsIt()
    {
        var req = Sample();
        req.Assets[0] = req.Assets[0] with { ShareType = "حصة سهمية" };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var reloaded = await _service.GetAsync(doc.Id);
        Assert.NotNull(reloaded);
        var estate = Assert.Single(reloaded!.Assets);
        Assert.Equal("حصة سهمية", estate.ShareType);
    }

    [Fact]
    public async Task Create_PersistsVehicleFields()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Vehicle, new List<string> { "المدعى عليه" }, "تمام المركبة",
                null, null, null, null,
                "سيارة", "خاصة", "12345", "دمشق",
                null, null, null, null, null,
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal(AssetKindCatalog.Vehicle, asset.AssetKind);
        Assert.Equal("سيارة", asset.VehicleType);
        Assert.Equal("خاصة", asset.VehicleClass);
        Assert.Equal("12345", asset.PlateNumber);
        Assert.Equal("دمشق", asset.VehicleGovernorate);
        Assert.Equal("تمام المركبة", asset.ShareType);
    }

    [Fact]
    public async Task Create_PersistsShopFields()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Shop, new List<string> { "المدعى عليه" }, "تمام المتجر",
                null, null, null, null,
                null, null, null, null,
                "888", "10/5/2024", "حلب", "متجر أقمشة", "شارع الجمهورية",
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal(AssetKindCatalog.Shop, asset.AssetKind);
        Assert.Equal("888", asset.RegisterNumber);
        Assert.Equal("2024-05-10", asset.RegistrationDate);
        Assert.Equal("حلب", asset.ShopGovernorate);
        Assert.Equal("متجر أقمشة", asset.ShopDescription);
        Assert.Equal("شارع الجمهورية", asset.ShopLocation);
        Assert.Equal("تمام المتجر", asset.ShareType);
    }

    [Fact]
    public async Task Create_SalaryGuarantee_HasSingleOwnerAndNoShare()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.SalaryGuarantee, new List<string> { "المدعى عليه" }, "تمام العقار",
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                "المصرف العقاري - دمشق",
                null, null, null,
                "ملاحظات الكفالة"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal(AssetKindCatalog.SalaryGuarantee, asset.AssetKind);
        Assert.Equal("المصرف العقاري - دمشق", asset.PublicEntity);
        Assert.Equal("ملاحظات الكفالة", asset.Notes);
        // كفالة الرواتب لا تحمل مقدار حصة حتى لو أُرسل نوع حصة.
        Assert.Null(asset.ShareType);
    }

    [Fact]
    public async Task Create_UnregisteredShop_HasOwnersAndNoShare()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.UnregisteredShop, new List<string> { "المدعى عليه", "شريك ثانٍ" }, "تمام العقار",
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                null,
                "456", "15/1/2023", "مديرية التجارة الداخلية",
                "ملاحظات المتجر"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal(AssetKindCatalog.UnregisteredShop, asset.AssetKind);
        Assert.Equal("456", asset.LicenseNumber);
        Assert.Equal("2023-01-15", asset.LicenseDate);
        Assert.Equal("مديرية التجارة الداخلية", asset.LicenseIssuer);
        Assert.Equal("ملاحظات المتجر", asset.Notes);
        Assert.Null(asset.ShareType);
    }

    [Fact]
    public async Task Create_ShopWithDashAndArabicDigitDates_ParsesFreeText()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Shop, new List<string> { "المدعى عليه" }, "تمام المتجر",
                null, null, null, null,
                null, null, null, null,
                "888", "10-5-2024", "حلب", "متجر أقمشة", "شارع الجمهورية",
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal("2024-05-10", asset.RegistrationDate);
    }

    [Fact]
    public async Task Create_UnregisteredShopWithArabicDigitDate_ParsesFreeText()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.UnregisteredShop, new List<string> { "المدعى عليه" }, null,
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                null,
                "456", "٢٩/٢/٢٠٢٤", "مديرية التجارة الداخلية",
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal("2024-02-29", asset.LicenseDate);
    }

    [Fact]
    public async Task Create_ShopWithInvalidRegistrationDate_Throws()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Shop, new List<string> { "المدعى عليه" }, "تمام المتجر",
                null, null, null, null,
                null, null, null, null,
                "888", "ليس تاريخاً", "حلب", "متجر أقمشة", "شارع الجمهورية",
                null,
                null, null, null,
                null),
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ تسجيل المتجر", ex.Message);
    }

    [Fact]
    public async Task Create_UnregisteredShopWithInvalidLicenseDate_Throws()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.UnregisteredShop, new List<string> { "المدعى عليه" }, null,
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                null,
                "456", "غير صحيح", "مديرية التجارة الداخلية",
                null),
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ الترخيص", ex.Message);
    }

    [Fact]
    public async Task Create_ShopWithEmptyDates_StoresNull()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Shop, new List<string> { "المدعى عليه" }, "تمام المتجر",
                null, null, null, null,
                null, null, null, null,
                "888", "", "حلب", "متجر أقمشة", "شارع الجمهورية",
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Null(asset.RegistrationDate);
    }

    [Fact]
    public async Task Create_UnregisteredShopWithEmptyLicenseDate_StoresNull()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.UnregisteredShop, new List<string> { "المدعى عليه" }, null,
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                null,
                "456", null, "مديرية التجارة الداخلية",
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Null(asset.LicenseDate);
    }

    [Fact]
    public async Task Create_VehicleWithMultipleOwners_ForcesShareOfShares()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.Vehicle, new List<string> { "سمير حسن علي", "أحمد خالد الخطيب" }, "تمام المركبة",
                null, null, null, null,
                "شاحنة", "تجارية", "77", "حلب",
                null, null, null, null, null,
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var reloaded = await _service.GetAsync(doc.Id);

        var asset = Assert.Single(reloaded!.Assets);
        Assert.Equal(new List<string> { "سمير حسن علي", "أحمد خالد الخطيب" }, asset.Owners);
        Assert.Equal("حصة سهمية", asset.ShareType);
    }

    [Fact]
    public async Task Create_WithInvalidAssetKind_Throws()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, "أصل-غير-معروف", new List<string> { "المدعى عليه" }, null,
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                null,
                null, null, null,
                null),
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task UpdateStatus_Forced_RejectsAssetIdFromAnotherFile()
    {
        var first = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var other = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var otherAssetId = (await _service.GetAsync(other.Id))!.Assets[0].Id;

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(first.Id, "منفذ جبريا",
                new Dictionary<string, string?>
                {
                    ["execSubStatus"] = "منفذ جزئيا",
                    ["soldAssetIds"] = otherAssetId.ToString(),
                    ["forcedExecutionDate"] = "1/1/2024",
                }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Forced_SellsAuctionableKindsAndExcludesSalaryGuarantee()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.RealEstate, new List<string> { "المدعى عليه" }, "تمام العقار",
                "بيت", "12345", "المزة", "الصالحية",
                null, null, null, null,
                null, null, null, null, null,
                null,
                null, null, null,
                null),
            new AssetDto(null, AssetKindCatalog.Vehicle, new List<string> { "المدعى عليه" }, "تمام المركبة",
                null, null, null, null,
                "سيارة", "خاصة", "99", "دمشق",
                null, null, null, null, null,
                null,
                null, null, null,
                null),
            new AssetDto(null, AssetKindCatalog.SalaryGuarantee, new List<string> { "المدعى عليه" }, null,
                null, null, null, null,
                null, null, null, null,
                null, null, null, null, null,
                "المصرف",
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);
        var estateId = loaded!.Assets[0].Id;
        var vehicleId = loaded.Assets[1].Id;
        var salaryId = loaded.Assets[2].Id;

        // بيع العقار والمركبة مقبول (معرّفا أموال قابلة للمزاد).
        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ كاملا",
                ["soldAssetIds"] = $"{estateId},{vehicleId}",
                ["forcedExecutionDate"] = "1/1/2024",
            }, "lawyer1");
        Assert.True(ok);

        // كفالة الرواتب لا تُقبل في «منفذ جبريا» لأنها ليست من الأموال القابلة للمزاد.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
                new Dictionary<string, string?>
                {
                    ["execSubStatus"] = "منفذ كاملا",
                    ["soldAssetIds"] = $"{estateId},{salaryId}",
                    ["forcedExecutionDate"] = "1/1/2024",
                }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Forced_StoresSoldAssetNamesPerKind()
    {
        var req = Sample();
        req.Assets = new()
        {
            new AssetDto(null, AssetKindCatalog.RealEstate, new List<string> { "المدعى عليه" }, "تمام العقار",
                "بيت", "12345", "المزة", "الصالحية",
                null, null, null, null,
                null, null, null, null, null,
                null,
                null, null, null,
                null),
            new AssetDto(null, AssetKindCatalog.Vehicle, new List<string> { "المدعى عليه" }, "تمام المركبة",
                null, null, null, null,
                "سيارة", "خاصة", "99", "دمشق",
                null, null, null, null, null,
                null,
                null, null, null,
                null),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);
        var ids = string.Join(",", loaded!.Assets.Select(a => a.Id));

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ كاملا",
                ["soldAssetIds"] = ids,
                ["forcedExecutionDate"] = "1/1/2024",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(2, updated!.SoldAssetIds.Count);
        Assert.Contains("بيت", updated.Occurrences
            .Where(o => o.Details != null)
            .Select(o => string.Join(' ', o.Details!.Values))
            .FirstOrDefault(v => v.Contains("بيت")) ?? "");
    }

    [Fact]
    public async Task Create_SavesFileRegistrationDate()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.NotNull(doc);
        Assert.Equal("1/8/2026", doc!.FileRegistrationDate);
        var reloaded = await _service.GetAsync(doc.Id);
        Assert.Equal("1/8/2026", reloaded!.FileRegistrationDate);
    }

    [Fact]
    public async Task Update_ChangesFileRegistrationDate()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var update = Sample();
        update.FileRegistrationDate = "2/8/2026";

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("2/8/2026", updated!.FileRegistrationDate);
        var reloaded = await _service.GetAsync(doc.Id);
        Assert.Equal("2/8/2026", reloaded!.FileRegistrationDate);
    }

    [Fact]
    public async Task Update_ClearingFileRegistrationDateRemovesRow()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var update = Sample();
        update.FileNumber = "";
        update.FileYear = "";
        update.FileRegistrationDate = "";

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Null(updated!.FileRegistrationDate);
        Assert.Equal(0, _db.DocumentRegistrationDates.Count());
    }

    [Fact]
    public async Task UpdateStatus_Settlement_SetsBaraetFields()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?>
            {
                ["baraetNumber"] = "77",
                ["baraetDate"] = "1/1/2024",
                ["collectedAmount"] = "500.50",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ بالتسوية", updated!.ExecStatus);
        Assert.Equal("77", updated.BaraetNumber);
        Assert.Equal(500.50m, updated.CollectedAmount);
        Assert.Contains("status", _audit.Actions);
    }

    [Fact]
    public async Task UpdateStatus_ForcedPartial_SetsSubStatusAndCollectedAmount()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var created = await _service.GetAsync(doc.Id);
        var estateId = created!.Assets[0].Id;

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ جزئيا",
                ["collectedAmount"] = "1000",
                ["soldAssetIds"] = estateId.ToString(),
                ["forcedExecutionDate"] = "1/1/2024",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ جبريا", updated!.ExecStatus);
        Assert.Equal("منفذ جزئيا", updated.ExecSubStatus);
        Assert.Equal(1000m, updated.CollectedAmount);
        Assert.Null(updated.BaraetNumber);
    }

    [Fact]
    public async Task UpdateStatus_InvalidCollectedAmount_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["collectedAmount"] = "abc" }, "lawyer1"));
    }

    [Theory]
    [InlineData("١٬٠٠٠", 1000d)]
    [InlineData("٥٠٠٫٥٠", 500.50d)]
    public async Task UpdateStatus_ArabicDigitCollectedAmount_Normalized(string raw, double expected)
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?>
            {
                ["baraetNumber"] = "77",
                ["baraetDate"] = "١/١/٢٠٢٤",
                ["collectedAmount"] = raw,
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal((decimal)expected, updated!.CollectedAmount);
        Assert.Equal("١/١/٢٠٢٤", updated.BaraetDate);
    }

    [Fact]
    public async Task UpdateStatus_NegativeCollectedAmount_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["collectedAmount"] = "-100" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_InvalidExecSubStatus_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
                new Dictionary<string, string?> { ["execSubStatus"] = "غير معروف" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Deferred_MissingTarithNumberDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "تريث",
                new Dictionary<string, string?> { ["tarithNumber"] = "5" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Settlement_MissingBaraetNumberDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["baraetNumber"] = "77" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_ForcedComplete_SetsCollectedAmountOnly()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var created = await _service.GetAsync(doc.Id);
        var estateId = created!.Assets[0].Id;

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ كاملا",
                ["collectedAmount"] = "1000",
                ["soldAssetIds"] = estateId.ToString(),
                ["forcedExecutionDate"] = "1/1/2024",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ كاملا", updated!.ExecSubStatus);
        Assert.Equal(1000m, updated.CollectedAmount);
        Assert.Null(updated.BaraetNumber);
        Assert.Null(updated.BaraetDate);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "حالة-غير-صالحة", new Dictionary<string, string?>(), "lawyer1"));
    }

    [Fact]
    public async Task RevertStatus_ClearsFieldsAndRecordsOccurrence()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?> { ["baraetNumber"] = "77", ["baraetDate"] = "1/1/2024" }, "lawyer1");

        await _service.RevertStatusAsync(doc.Id, new Dictionary<string, string?>
        {
            ["sayerNumber"] = "8",
            ["sayerDate"] = "2/2/2024",
            ["sayerRegNumber"] = "9",
            ["sayerRegDate"] = "3/3/2024",
        }, "lawyer1");

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(string.Empty, updated!.ExecStatus);
        Assert.Null(updated.BaraetNumber);
    }

    [Fact]
    public async Task UpdateStatus_FromDraft_ForbidsForcibleExecution()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
                new Dictionary<string, string?> { ["execSubStatus"] = "منفذ كاملا" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_FromDraft_AllowsDeferred()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.True(await _service.UpdateStatusAsync(doc.Id, "تريث",
            new Dictionary<string, string?> { ["tarithNumber"] = "5", ["tarithDate"] = "1/1/2024" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_FromCirculating_ToStruckOff_SetsDateAndHidesFromSearch()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        Assert.True(await _service.UpdateStatusAsync(doc.Id, "مشطوب",
            new Dictionary<string, string?> { ["struckOffDate"] = "1/2/2024" }, "lawyer1"));

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("مشطوب", updated!.ExecStatus);
        Assert.NotNull(updated.StruckOffDate);

        // يختفي من البحث العام ويظهر في صفحة «الملفات المشطوبة».
        var search = await _service.SearchAsync(null, null, null, null, null, null, null, null, null, 1, 20);
        Assert.DoesNotContain(doc.Id, search.Items.Select(d => d.Id));

        var struckOff = await _service.SearchStruckOffAsync(null, 1, 20);
        Assert.Contains(doc.Id, struckOff.Items.Select(d => d.Id));
    }

    [Fact]
    public async Task RevertStatus_FromDeferred_RequiresSayerFields()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.UpdateStatusAsync(doc.Id, "تريث",
            new Dictionary<string, string?> { ["tarithNumber"] = "5", ["tarithDate"] = "1/1/2024" }, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RevertStatusAsync(doc.Id, new Dictionary<string, string?>(), "lawyer1"));

        Assert.True(await _service.RevertStatusAsync(doc.Id, new Dictionary<string, string?>
        {
            ["sayerNumber"] = "8",
            ["sayerDate"] = "2/2/2024",
            ["sayerRegNumber"] = "9",
            ["sayerRegDate"] = "3/3/2024",
        }, "lawyer1"));

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(string.Empty, updated!.ExecStatus);
        Assert.Equal("8", updated.SayerNumber);
    }

    [Fact]
    public async Task UpdateStatus_Settlement_StoresThreeCollectedAmounts()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        Assert.True(await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?>
            {
                ["baraetNumber"] = "77",
                ["baraetDate"] = "1/1/2024",
                ["collectedAmount"] = "100",
                ["collectedCurrency"] = "ليرة سورية",
                ["collectedAmount2"] = "50",
                ["collectedCurrency2"] = "دولار أمريكي",
                ["collectedAmount3"] = "20",
                ["collectedCurrency3"] = "يورو",
            }, "lawyer1"));

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(100m, updated!.CollectedAmount);
        Assert.Equal("ليرة سورية", updated.CollectedCurrency);
        Assert.Equal(50m, updated.CollectedAmount2);
        Assert.Equal("دولار أمريكي", updated.CollectedCurrency2);
        Assert.Equal(20m, updated.CollectedAmount3);
        Assert.Equal("يورو", updated.CollectedCurrency3);
    }

    [Fact]
    public async Task UpdateStatus_Forcible_RejectsEstateNotOwnedByFile()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
                new Dictionary<string, string?>
                {
                    ["execSubStatus"] = "منفذ كاملا",
                    ["soldAssetIds"] = "999999",
                }, "lawyer1"));
    }

    [Fact]
    public async Task StatusChange_RecordsOccurrenceWithDetails()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await _service.UpdateStatusAsync(doc.Id, "تريث",
            new Dictionary<string, string?>
            {
                ["tarithNumber"] = "5",
                ["tarithDate"] = "1/1/2024",
                ["tarithRegNumber"] = "6",
                ["tarithRegDate"] = "2/1/2024",
            }, "lawyer1");

        var updated = await _service.GetAsync(doc.Id);
        var occurrence = Assert.Single(updated!.Occurrences);
        Assert.Equal(OccurrenceTypeCatalog.Deferred, occurrence.OccurrenceType);
        Assert.Equal("5", occurrence.Details!["tarithNumber"]);
        Assert.Equal("2/1/2024", occurrence.Details["tarithRegDate"]);
    }

    [Fact]
    public async Task RestoreStruckOff_ApplicantSide_ReinstatesWithNumberAndYear()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.UpdateStatusAsync(doc.Id, "مشطوب",
            new Dictionary<string, string?> { ["struckOffDate"] = "1/2/2024" }, "lawyer1");

        Assert.True(await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest
        {
            RenewalFileNumber = "999",
            RenewalYear = 2024,
            RenewalFileType = "س",
        }, "lawyer1"));

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(string.Empty, updated!.ExecStatus);
        Assert.Equal("999", updated.RenewalFileNumber);
        Assert.Contains(updated.Occurrences, o => o.OccurrenceType == OccurrenceTypeCatalog.Renewal);
    }

    [Fact]
    public async Task Delete_SoftDeletesDocument_AndHidesItFromQueries()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var ok = await _service.DeleteAsync(doc.Id, "head1");
        Assert.True(ok);
        Assert.Null(await _service.GetAsync(doc.Id));
        Assert.Contains("delete", _audit.Actions);

        // الصف لا يزال في القاعدة لكن موسوماً كمحذوف منطقياً
        var row = _db.Documents.IgnoreQueryFilters().First(d => d.Id == doc.Id);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Delete_ThenRestore_MakesDocumentVisibleAgain()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(doc.Id, "head1");
        Assert.Null(await _service.GetAsync(doc.Id));

        var restored = await _service.RestoreAsync(doc.Id, "head1");
        Assert.True(restored);
        Assert.NotNull(await _service.GetAsync(doc.Id));
        Assert.Contains("restore", _audit.Actions);

        var row = _db.Documents.IgnoreQueryFilters().First(d => d.Id == doc.Id);
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeletedAt);
    }

    [Fact]
    public async Task Restore_NonDeletedOrUnknownDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        Assert.False(await _service.RestoreAsync(doc.Id, "head1"));
        Assert.False(await _service.RestoreAsync(99_999, "head1"));
    }

    [Fact]
    public async Task SearchDeleted_ReturnsOnlyDeletedDocuments_WithDeletedAt()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync(null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains(result.Items, d => d.Id == deleted.Id);
        Assert.DoesNotContain(result.Items, d => d.Id == active.Id);
        Assert.NotNull(result.Items.Single().DeletedAt);
    }

    [Fact]
    public async Task SearchDeleted_AfterRestore_NoLongerListed()
    {
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");
        await _service.RestoreAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync(null, 1, 20);

        Assert.DoesNotContain(result.Items, d => d.Id == deleted.Id);
    }

    [Fact]
    public async Task SearchDeleted_ByQuery_FiltersByName()
    {
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync("أحمد خالد الخطيب", 1, 20);
        Assert.Contains(result.Items, d => d.Id == deleted.Id);

        var noMatch = await _service.SearchDeletedAsync("اسم غير موجود", 1, 20);
        Assert.Empty(noMatch.Items);
    }

    [Fact]
    public async Task SearchDeleted_WithNoDeletedDocuments_ReturnsEmpty()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchDeletedAsync(null, 1, 20);
        Assert.Empty(result.Items);
        Assert.NotNull(await _service.GetAsync(active.Id));
    }

    [Fact]
    public async Task GetDeleted_ReturnsOnlyDeletedDocuments()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        Assert.Null(await _service.GetDeletedAsync(active.Id));
        Assert.Null(await _service.GetDeletedAsync(99_999));

        var dto = await _service.GetDeletedAsync(deleted.Id);
        Assert.NotNull(dto);
        Assert.Equal(deleted.Id, dto!.Id);
        Assert.NotNull(dto.DeletedAt);
    }

    [Fact]
    public async Task Create_WithoutBorrowerName_Throws()
    {
        var req = Sample();
        req.BorrowerName = "  ";
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task AddAction_AddsExecutionAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "تم إشعار المنفذ عليه", ActionDate = "1/8/2026" },
            userId: 1, actorName: "lawyer1");

        Assert.True(action.Id > 0);
        Assert.Equal("تم إشعار المنفذ عليه", action.Text);
        Assert.Equal("1/8/2026", action.ActionDate);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Single(list);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task AddAction_SavesReminderDurationAndColor()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "متابعة مع المحكمة",
                ActionDate = "1/8/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أحمر",
            },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("أسبوع", action.ReminderDuration);
        Assert.Equal("أحمر", action.ReminderColor);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Single(list);
        Assert.Equal("أسبوع", list[0].ReminderDuration);
        Assert.Equal("أحمر", list[0].ReminderColor);
    }

    [Fact]
    public async Task AddAction_InvalidReminderDuration_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest
                {
                    Text = "إجراء",
                    ActionDate = "1/8/2026",
                    ReminderDuration = "سنة",
                    ReminderColor = "أحمر",
                },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_InvalidReminderColor_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest
                {
                    Text = "إجراء",
                    ActionDate = "1/8/2026",
                    ReminderDuration = "أسبوع",
                    ReminderColor = "أخضر",
                },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_PartialReminderWithoutColor_SavesDurationOnly()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "إجراء مع مدة فقط",
                ActionDate = "1/8/2026",
                ReminderDuration = "3 أيام",
            },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("3 أيام", action.ReminderDuration);
        Assert.Null(action.ReminderColor);
    }

    [Fact]
    public async Task UpdateAction_ChangesReminder()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "إجراء",
                ActionDate = "1/1/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أصفر",
            },
            userId: 1, actorName: "lawyer1");

        var updated = await _service.UpdateExecutionActionAsync(doc.Id, action.Id,
            new UpdateExecutionActionRequest
            {
                Type = "action",
                Text = "إجراء محدث",
                ActionDate = "2/2/2026",
                ReminderDuration = "شهر",
                ReminderColor = "بنفسجي",
            }, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("شهر", updated!.ReminderDuration);
        Assert.Equal("بنفسجي", updated.ReminderColor);
    }

    [Fact]
    public async Task AddAction_EmptyText_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id, new AddExecutionActionRequest { Text = "  " },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task GetActions_OrdersNewestFirst()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "أول", ActionDate = "1/1/2026" }, 1, "lawyer1");
        await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "ثان", ActionDate = "2/2/2026" }, 1, "lawyer1");

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal("ثان", list[0].Text);
        Assert.Equal("أول", list[1].Text);
    }

    [Fact]
    public async Task AddAction_MissingDocument_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.AddExecutionActionAsync(9999,
                new AddExecutionActionRequest { Text = "إجراء" }, userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddNote_WithoutDate_SetsTodayDate()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var note = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة بلا تاريخ" },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("note", note.Type);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), note.ActionDate);
    }

    [Fact]
    public async Task AddAction_WithoutDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest { Type = "action", Text = "إجراء بلا تاريخ" },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_InvalidType_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest { Type = "other", Text = "نص" },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task UpdateAction_ChangesTextAndType()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء قديم", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var updated = await _service.UpdateExecutionActionAsync(doc.Id, action.Id,
            new UpdateExecutionActionRequest { Type = "note", Text = "ملاحظة محدثة" }, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("note", updated!.Type);
        Assert.Equal("ملاحظة محدثة", updated.Text);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task UpdateAction_WrongDocument_ThrowsKeyNotFound()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateExecutionActionAsync(doc.Id + 9999, action.Id,
                new UpdateExecutionActionRequest { Type = "note", Text = "x" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateAction_WithoutDate_WhenAction_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة" },
            userId: 1, actorName: "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateExecutionActionAsync(doc.Id, action.Id,
                new UpdateExecutionActionRequest { Type = "action", Text = "تحويل إلى إجراء بلا تاريخ" }, "lawyer1"));
    }

    [Fact]
    public async Task DeleteAction_RemovesAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var deleted = await _service.DeleteExecutionActionAsync(doc.Id, action.Id, "lawyer1");

        Assert.True(deleted);
        Assert.Empty(await _service.GetExecutionActionsAsync(doc.Id));
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task DeleteAction_WrongDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var deleted = await _service.DeleteExecutionActionAsync(doc.Id + 9999, action.Id, "lawyer1");
        Assert.False(deleted);
    }

    [Fact]
    public async Task ClearReminder_ClearsReminderFieldsKeepsAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Type = "action",
                Text = "إجراء بموعد",
                ActionDate = "1/8/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أحمر",
            },
            userId: 1, actorName: "lawyer1");

        var cleared = await _service.ClearReminderAsync(doc.Id, action.Id, "lawyer1");

        Assert.True(cleared);
        var list = await _service.GetExecutionActionsAsync(doc.Id);
        var single = Assert.Single(list);
        Assert.Equal("إجراء بموعد", single.Text);
        Assert.Null(single.ReminderDuration);
        Assert.Null(single.ReminderColor);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task ClearReminder_WrongDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var cleared = await _service.ClearReminderAsync(doc.Id + 9999, action.Id, "lawyer1");
        Assert.False(cleared);
    }

    [Fact]
    public async Task ClearReminder_MissingAction_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var cleared = await _service.ClearReminderAsync(doc.Id, 9999, "lawyer1");
        Assert.False(cleared);
    }

    [Fact]
    public async Task Create_WithInitialActions_SeedsExecutionActions()
    {
        var req = Sample();
        req.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "action", Text = "تم إشعار المنفذ عليه", ActionDate = "1/8/2026" },
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة افتتاحية" },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Equal(2, list.Count);
        var action = list.Single(a => a.Type == "action");
        Assert.Equal("تم إشعار المنفذ عليه", action.Text);
        Assert.Equal("1/8/2026", action.ActionDate);
        var note = list.Single(a => a.Type == "note");
        Assert.Equal("ملاحظة افتتاحية", note.Text);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), note.ActionDate);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task Create_WithBlankInitialAction_IgnoresIt()
    {
        var req = Sample();
        req.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "action", Text = "   ", ActionDate = "1/8/2026" },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Empty(await _service.GetExecutionActionsAsync(doc.Id));
    }

    [Fact]
    public async Task Create_WithMaliciousInitialAction_RollsBackWholeSave()
    {
        var req = Sample();
        req.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "action", Text = "<script>alert(1)</script>", ActionDate = "1/8/2026" },
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));

        // الذرّية: رفض الإجراء الخبيث يُرجع كل معاملة الحفظ (لا مستند ولا إجراءات).
        Assert.Equal(0, _db.Documents.AsNoTracking().Count());
        Assert.Equal(0, _db.ExecutionActions.AsNoTracking().Count());
    }

    [Fact]
    public async Task Update_WithInitialActionMatchingExisting_SkipsDuplicate()
    {
        var req = Sample();
        req.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "action", Text = "إشعار أول", ActionDate = "1/8/2026" },
        };
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var update = Sample();
        update.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "action", Text = "إشعار أول", ActionDate = "2/8/2026" },
            new AddExecutionActionRequest { Type = "note", Text = "متابعة لاحقة" },
        };
        await _service.UpdateAsync(doc.Id, update, "lawyer1", userId: 1);

        // لا يتضاعف الإجراء المطابق، وتُضاف الملاحظة الجديدة فقط.
        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Equal(2, list.Count);
        Assert.Single(list.Where(a => a.Type == "action" && a.Text == "إشعار أول"));
        Assert.Single(list.Where(a => a.Type == "note" && a.Text == "متابعة لاحقة"));
    }

    [Fact]
    public async Task Update_WithNewInitialAction_AddsPreservingExisting()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء قائم", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var update = Sample();
        update.InitialActions = new()
        {
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة جديدة" },
        };
        await _service.UpdateAsync(doc.Id, update, "lawyer1", userId: 1);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Equal(2, list.Count);
        Assert.Single(list.Where(a => a.Type == "action" && a.Text == "إجراء قائم"));
        Assert.Single(list.Where(a => a.Type == "note" && a.Text == "ملاحظة جديدة"));
    }

    private async Task SeedUser2Async()
    {
        _db.Users.Add(new User
        {
            Username = "lawyer2",
            FullName = "محامي آخر",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Document> CreateDocForRotation(
        int ownerId,
        string? execStatus = null,
        string? execSubStatus = null,
        bool draft = false,
        bool deleted = false)
    {
        var req = Sample();
        if (draft)
        {
            req.FileNumber = null;
            req.FileYear = null;
        }
        req.FileType = "حقوق";
        var resp = await _service.CreateAsync(req, ownerId, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == resp.Id);
        if (execStatus is not null) doc.ExecStatus = execStatus;
        if (execSubStatus is not null) doc.ExecSubStatus = execSubStatus;
        if (deleted)
        {
            doc.IsDeleted = true;
            doc.DeletedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return doc;
    }

    private async Task AddPreviousYearBaseAsync(int documentId, string baseNumber)
    {
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = documentId,
            Year = DateTime.Today.Year - 1,
            BaseNumber = baseNumber,
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetRotationList_ReturnsOnlyEligibleOwnedFiles()
    {
        var active = await CreateDocForRotation(1);
        var partially = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedForcibly, ExecutionStatusCatalog.SubPartiallyExecuted);
        var settled = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedBySettlement);
        var forced = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedForcibly, ExecutionStatusCatalog.SubFullyExecuted);
        var draft = await CreateDocForRotation(1, draft: true);
        var deleted = await CreateDocForRotation(1, deleted: true);
        await SeedUser2Async();
        var otherOwner = await CreateDocForRotation(1);
        otherOwner.CreatedById = 2;
        await _db.SaveChangesAsync();

        var list = await _service.GetRotationListAsync(1, 1, 100);
        var ids = list.Items.Select(r => r.DocumentId).ToHashSet();

        Assert.Contains(active.Id, ids);
        Assert.Contains(partially.Id, ids);
        Assert.DoesNotContain(settled.Id, ids);
        Assert.DoesNotContain(forced.Id, ids);
        Assert.DoesNotContain(draft.Id, ids);
        Assert.DoesNotContain(deleted.Id, ids);
        Assert.DoesNotContain(otherOwner.Id, ids);
    }

    [Fact]
    public async Task GetRotationList_ExcludesFilesWithCurrentYearBaseNumber()
    {
        var current = DateTime.Today.Year;

        // دوّر للسنة الحالية فقط → مخفي.
        var rotated = await CreateDocForRotation(1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = rotated.Id,
            Year = current,
            BaseNumber = "2200",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // دوّر في سنة سابقة فقط → يظهر (لم يُدوَّر لهذا العام).
        var previousOnly = await CreateDocForRotation(1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = previousOnly.Id,
            Year = current - 1,
            BaseNumber = "1100",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // دوّر للسنتين معًا → مخفي لأنه يملك رقمًا للسنة الحالية.
        var both = await CreateDocForRotation(1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = both.Id,
            Year = current - 1,
            BaseNumber = "1100",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = both.Id,
            Year = current,
            BaseNumber = "2200",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

        // لم يُدوَّر أبدًا → يظهر.
        var never = await CreateDocForRotation(1);

        // مقيد في السنة الحالية دون أي تدوير → مخفي (رقم ملفه الأصلي هو نفسه رقم أساس سنته).
        var currentYearRegistered = await CreateDocForRotation(1);
        currentYearRegistered.FileYear = current.ToString();

        await _db.SaveChangesAsync();

        var list = await _service.GetRotationListAsync(1, 1, 100);
        var ids = list.Items.Select(r => r.DocumentId).ToHashSet();

        Assert.DoesNotContain(rotated.Id, ids);
        Assert.DoesNotContain(both.Id, ids);
        Assert.DoesNotContain(currentYearRegistered.Id, ids);
        Assert.Contains(previousOnly.Id, ids);
        Assert.Contains(never.Id, ids);

        // رقم السنة الحالية دائمًا فارغ في الاستجابة لأن الظاهرين فقط لم يُدوَّروا لها.
        Assert.All(list.Items, r => Assert.Null(r.BaseNumber));

        // بقية الحقول تصل سليمة.
        var row = Assert.Single(list.Items, r => r.DocumentId == previousOnly.Id);
        Assert.Equal(previousOnly.Court, row.Court);
        Assert.Equal("520", row.FileNumber);
        Assert.Equal("حقوق", row.FileType);
    }

    [Fact]
    public async Task GetRotationList_PaginatesResultsAcrossPages()
    {
        var doc1 = await CreateDocForRotation(1);
        var doc2 = await CreateDocForRotation(1);
        var doc3 = await CreateDocForRotation(1);

        var first = await _service.GetRotationListAsync(1, 1, 2);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(1, first.Page);
        Assert.Equal(2, first.PerPage);
        Assert.Equal(2, first.Items.Count);

        var second = await _service.GetRotationListAsync(1, 2, 2);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, second.Page);
        var secondDoc = Assert.Single(second.Items);
        Assert.Contains(secondDoc.DocumentId, new[] { doc1.Id, doc2.Id, doc3.Id });

        // الصفحة خارج النطاق تُرجع قائمة فارغة دون خطأ.
        var beyond = await _service.GetRotationListAsync(1, 10, 2);
        Assert.Equal(3, beyond.TotalCount);
        Assert.Empty(beyond.Items);
    }

    [Fact]
    public async Task GetAsync_DisplayFileNumber_ReplacesFileNumberWithCurrentYearBaseNumber()
    {
        var doc = await CreateDocForRotation(1);

        // دون تدوير: الرقم الظاهر = رقم الملف الأصلي.
        var before = await _service.GetAsync(doc.Id);
        Assert.Equal("520", before!.DisplayFileNumber);
        Assert.Equal("520", before.FileNumber);

        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1500") }, "lawyer1");

        // بعد التدوير: الرقم الظاهر يحل محل رقم الملف، والأصلي يبقى محفوظًا للتاريخ.
        var after = await _service.GetAsync(doc.Id);
        Assert.Equal("1500", after!.DisplayFileNumber);
        Assert.Equal("520", after.FileNumber);
    }

    [Fact]
    public async Task GetBaseNumberHistory_ReturnsAllYearsDescending()
    {
        var doc = await CreateDocForRotation(1);
        var current = DateTime.Today.Year;
        _db.BaseNumbers.AddRange(
            new DocumentBaseNumber { DocumentId = doc.Id, Year = current - 2, BaseNumber = "300", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new DocumentBaseNumber { DocumentId = doc.Id, Year = current, BaseNumber = "1500", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new DocumentBaseNumber { DocumentId = doc.Id, Year = current - 1, BaseNumber = "900", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var history = await _service.GetBaseNumberHistoryAsync(doc.Id);

        Assert.Equal(3, history.Count);
        Assert.Equal(new[] { current, current - 1, current - 2 }, history.Select(h => h.Year));
        Assert.Equal("1500", history[0].BaseNumber);
        Assert.Equal("900", history[1].BaseNumber);
        Assert.Equal("300", history[2].BaseNumber);
    }

    [Fact]
    public async Task GetBaseNumberHistory_UnknownDocument_ReturnsEmpty()
    {
        var history = await _service.GetBaseNumberHistoryAsync(999999);
        Assert.Empty(history);
    }

    [Fact]
    public async Task GetRotationList_ClampsPagingParameters()
    {
        var doc1 = await CreateDocForRotation(1);
        var doc2 = await CreateDocForRotation(1);

        // page=0 يُعتبر الصفحة الأولى، و perPage أكبر من الحد الأقصى يُقصّ إلى 100.
        var clamped = await _service.GetRotationListAsync(1, 0, 500);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(100, clamped.PerPage);
        Assert.Equal(2, clamped.TotalCount);
        Assert.Contains(doc1.Id, clamped.Items.Select(i => i.DocumentId));
    }

    [Fact]
    public async Task GetRotationList_IncludesTradingExecutedFamilyFilesWithPreviousBase()
    {
        // عائلتا «منفذ عليه» (Executed + Deposit) متداولتان وبها رقم أساس لسنة سابقة → مؤهلتان
        // وتظهران في قائمة التدوير باسم طالب العرض (DisplayName) بدل أعمدة المقترض الفارغة.
        var executed = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await AddPreviousYearBaseAsync(executed.Id, "999");
        var deposit = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await AddPreviousYearBaseAsync(deposit.Id, "888");

        var list = await _service.GetRotationListAsync(1, 1, 100);
        var ids = list.Items.Select(r => r.DocumentId).ToHashSet();

        Assert.Contains(executed.Id, ids);
        Assert.Contains(deposit.Id, ids);

        var row = list.Items.Single(r => r.DocumentId == executed.Id);
        Assert.Equal("أحمد خالد الخطيب", row.DisplayName); // اسم طالب العرض
        Assert.Null(row.BorrowerName);
        row = list.Items.Single(r => r.DocumentId == deposit.Id);
        Assert.Equal("هاني سامر النجار", row.DisplayName);
        Assert.Null(row.BorrowerName);
    }

    [Fact]
    public async Task GetRotationList_ExcludesNonTradingExecutedFamilyFiles()
    {
        // عائلة «منفذ عليه» بحالة «منفذ» أو «مشطوب» → مخفية حتى لو دُوِّرت سابقًا.
        var executed = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == executed.Id);
        doc.ExecutedStatus = ExecutedStatusCatalog.Executed;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(executed.Id, "999");

        var struck = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        doc = await _db.Documents.FirstAsync(d => d.Id == struck.Id);
        doc.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(struck.Id, "888");

        // عائلة متداولة لكن بدون رقم أساس لسنة سابقة → مخفية.
        var neverRotated = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var list = await _service.GetRotationListAsync(1, 1, 100);
        var ids = list.Items.Select(r => r.DocumentId).ToHashSet();

        Assert.DoesNotContain(executed.Id, ids);
        Assert.DoesNotContain(struck.Id, ids);
        Assert.DoesNotContain(neverRotated.Id, ids);
    }

    [Fact]
    public async Task SaveBaseNumbers_CreatesThenUpdatesSameYear()
    {
        var doc = await CreateDocForRotation(1);
        var year = DateTime.Today.Year;

        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, " 1500 ") }, "lawyer1");
        var row = await _db.BaseNumbers.AsNoTracking().SingleAsync(b => b.DocumentId == doc.Id);
        Assert.Equal(year, row.Year);
        Assert.Equal("1500", row.BaseNumber);
        Assert.Contains("rotate", _audit.Actions);

        // تحديث نفس السنة لا يُنشئ سجلًا مكررًا.
        _db.ChangeTracker.Clear();
        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1501") }, "lawyer1");
        var rows = await _db.BaseNumbers.AsNoTracking().Where(b => b.DocumentId == doc.Id).ToListAsync();
        Assert.Single(rows);
        Assert.Equal("1501", rows[0].BaseNumber);
        Assert.Equal(doc.Id, rows[0].DocumentId);
    }

    [Fact]
    public async Task SaveBaseNumbers_EmptyClearsCurrentYearPreservingPrevious()
    {
        var doc = await CreateDocForRotation(1);
        var year = DateTime.Today.Year;
        _db.BaseNumbers.AddRange(
            new DocumentBaseNumber { DocumentId = doc.Id, Year = year - 1, BaseNumber = "999", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new DocumentBaseNumber { DocumentId = doc.Id, Year = year, BaseNumber = "1500", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "   ") }, "lawyer1");

        var rows = await _db.BaseNumbers.AsNoTracking().Where(b => b.DocumentId == doc.Id).OrderBy(b => b.Year).ToListAsync();
        var single = Assert.Single(rows);
        Assert.Equal(year - 1, single.Year);
        Assert.Equal("999", single.BaseNumber);
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsNonOwnedFile()
    {
        await SeedUser2Async();
        var doc = await CreateDocForRotation(1);
        doc.CreatedById = 2;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsExecutedFile()
    {
        var doc = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedBySettlement);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsExecutedLikeFileWithoutPreviousRotation()
    {
        // عائلة «منفذ عليه» متداولة لكنها لم تُدوَّر قط (لا رقم أساس لسنة سابقة) → غير مؤهلة:
        // التدوير فيها استمرار لدوران سبق أن بدأ.
        var resp = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsDepositFileWithoutPreviousRotation()
    {
        // عائلة «منفذ عليه» متداولة لكنها لم تُدوَّر قط (لا رقم أساس لسنة سابقة) → غير مؤهلة.
        var resp = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_AcceptsTradingExecutedLikeFileWithPreviousBase()
    {
        // عائلة «منفذ عليه» متداولة (لا منفذ ولا مشطوب) وبها رقم أساس لسنة سابقة → مؤهلة.
        var resp = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await AddPreviousYearBaseAsync(resp.Id, "999");

        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1");

        var row = _db.BaseNumbers.AsNoTracking()
            .Single(b => b.DocumentId == resp.Id && b.Year == DateTime.Today.Year);
        Assert.Equal("1500", row.BaseNumber);
    }

    [Fact]
    public async Task SaveBaseNumbers_AcceptsTradingDepositFileWithPreviousBase()
    {
        var resp = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await AddPreviousYearBaseAsync(resp.Id, "999");

        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1");

        var row = _db.BaseNumbers.AsNoTracking()
            .Single(b => b.DocumentId == resp.Id && b.Year == DateTime.Today.Year);
        Assert.Equal("1500", row.BaseNumber);
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsExecutedFamilyFileMarkedExecuted()
    {
        // عائلة «منفذ عليه» بحالة «منفذ» حتى لو دُوِّرت سابقًا → غير مؤهلة.
        var resp = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == resp.Id);
        doc.ExecutedStatus = ExecutedStatusCatalog.Executed;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(resp.Id, "999");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1"));
        Assert.Equal(1, _db.BaseNumbers.AsNoTracking().Count()); // سجل التدوير القديم محفوظ
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsExecutedFamilyFileStruckOff()
    {
        var resp = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == resp.Id);
        doc.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(resp.Id, "999");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1"));
        Assert.Equal(1, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task Rotation_ExecutedFamilyIgnoresLegacyExecStatus()
    {
        // ملف عائلة «منفذ عليه» ببيانات قديمة تحمل حالة تنفيذ قديمة (منفذ بالتسوية) لكن حالة
        // الوضع الحالية «متداول» مع دوران سابق → تُحكم حالته بحالة الوضع (ExecutedStatus) فقط:
        // يظهر في قائمة التدوير ويُدوَّر — لا تعارض بين القائمة والحفظ.
        var resp = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == resp.Id);
        doc.ExecStatus = ExecutionStatusCatalog.ExecutedBySettlement; // بقايا نظام «طالبة تنفيذ»
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(resp.Id, "999");

        var list = await _service.GetRotationListAsync(1, 1, 100);
        Assert.Contains(resp.Id, list.Items.Select(r => r.DocumentId));

        await _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(resp.Id, "1500") }, "lawyer1");

        var row = _db.BaseNumbers.AsNoTracking()
            .Single(b => b.DocumentId == resp.Id && b.Year == DateTime.Today.Year);
        Assert.Equal("1500", row.BaseNumber);
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsDraftFile()
    {
        var doc = await CreateDocForRotation(1, draft: true);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsFileRegisteredInCurrentYear()
    {
        // قاعدة القيد: رقم الملف الأصلي هو نفسه رقم أساس سنة قيده، فالملف المقيد في السنة
        // الحالية يملك رقم أساس لها بالفعل فلا يُدوَّر.
        var doc = await CreateDocForRotation(1);
        doc.FileYear = DateTime.Today.Year.ToString();
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, "1500") }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsDuplicateEntry()
    {
        var doc = await CreateDocForRotation(1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1,
                new List<BaseNumberEntry> { new(doc.Id, "1500"), new(doc.Id, "1501") }, "lawyer1"));
    }
    [Fact]
    public async Task SaveBaseNumbers_RejectsNullEntry()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { null! }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_RejectsTooLongNumber()
    {
        var doc = await CreateDocForRotation(1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1, new List<BaseNumberEntry> { new(doc.Id, new string('9', 51)) }, "lawyer1"));
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_InvalidEntryRollsBackWholeSave()
    {
        var doc = await CreateDocForRotation(1);
        var executed = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedBySettlement);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SaveBaseNumbersAsync(1,
                new List<BaseNumberEntry> { new(doc.Id, "1500"), new(executed.Id, "1600") }, "lawyer1"));

        // الذرّية: لا يُحفظ أي سجل حتى للصف الصالح.
        Assert.Equal(0, _db.BaseNumbers.AsNoTracking().Count());
    }

    [Fact]
    public async Task SaveBaseNumbers_BatchesMultipleFilesInOneSave()
    {
        var doc1 = await CreateDocForRotation(1);
        var doc2 = await CreateDocForRotation(1);
        var doc3 = await CreateDocForRotation(1);
        var year = DateTime.Today.Year;

        _audit.Actions.Clear();

        await _service.SaveBaseNumbersAsync(1,
            new List<BaseNumberEntry>
            {
                new(doc1.Id, "1500"),
                new(doc2.Id, "1600"),
                new(doc3.Id, "1700"),
            }, "lawyer1");

        var rows = _db.BaseNumbers.AsNoTracking().OrderBy(b => b.DocumentId).ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(year, r.Year));
        Assert.Equal("1500", rows.Single(r => r.DocumentId == doc1.Id).BaseNumber);
        Assert.Equal("1600", rows.Single(r => r.DocumentId == doc2.Id).BaseNumber);
        Assert.Equal("1700", rows.Single(r => r.DocumentId == doc3.Id).BaseNumber);

        // دفعة تدقيق واحدة بنفس عدد العمليات — لكل ملف إدخال واحد.
        Assert.Equal(3, _audit.Actions.Count(a => a == "rotate"));
    }

    [Fact]
    public async Task NeedsRotation_ReflectsRedRule()
    {
        var current = DateTime.Today.Year;

        // 1) مقيد غير منفَّذ برقم أساس لسنة سابقة فقط → يحتاج تدوير (أحمر).
        var doc1 = await CreateDocForRotation(1);
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = doc1.Id, Year = current - 1, BaseNumber = "999", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // 2) لديه رقم أساس للسنة الحالية → لا يحتاج.
        var doc2 = await CreateDocForRotation(1);
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = doc2.Id, Year = current, BaseNumber = "1500", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // 3) منفَّذ برقم أساس لسنة سابقة فقط → لا يحتاج.
        var doc3 = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedBySettlement);
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = doc3.Id, Year = current - 1, BaseNumber = "888", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // 4) تحت رفع برقم أساس لسنة سابقة فقط → لا يحتاج.
        var doc4 = await CreateDocForRotation(1, draft: true);
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = doc4.Id, Year = current - 1, BaseNumber = "777", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // 5) جديد بلا أي رقم أساس → لا يحتاج.
        var doc5 = await CreateDocForRotation(1);

        // 6) منفذ جزئيا (متداول) برقم أساس لسنة سابقة فقط → يحتاج تدوير.
        var doc6 = await CreateDocForRotation(1, ExecutionStatusCatalog.ExecutedForcibly, ExecutionStatusCatalog.SubPartiallyExecuted);
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = doc6.Id, Year = current - 1, BaseNumber = "666", CreatedById = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        await _db.SaveChangesAsync();

        Assert.True((await _service.GetAsync(doc1.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(doc2.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(doc3.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(doc4.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(doc5.Id))!.NeedsRotation);
        Assert.True((await _service.GetAsync(doc6.Id))!.NeedsRotation);
    }

    [Fact]
    public async Task NeedsRotation_ExecutedFamilyRedRule()
    {
        // عائلة «منفذ عليه» متداولة بدوران سابق فقط → أحمر.
        var trading = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await AddPreviousYearBaseAsync(trading.Id, "999");

        // عائلة متداولة دوِّرت للسنة الحالية → لا أحمر.
        var rotatedCurrent = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = rotatedCurrent.Id,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        // عائلة بحالة «منفذ» بدوران سابق → لا أحمر.
        var executed = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var doc = await _db.Documents.FirstAsync(d => d.Id == executed.Id);
        doc.ExecutedStatus = ExecutedStatusCatalog.Executed;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(executed.Id, "888");

        // عائلة متداولة بدون دوران سابق → لا أحمر.
        var neverRotated = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        // عائلة «تحت رفع» بدوران سابق → لا أحمر أبدًا (القاعدة تقوم على ملف مقيد).
        var draft = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        var draftDoc = await _db.Documents.FirstAsync(d => d.Id == draft.Id);
        draftDoc.IsDraft = true;
        draftDoc.FileNumber = null;
        await _db.SaveChangesAsync();
        await AddPreviousYearBaseAsync(draft.Id, "777");

        Assert.True((await _service.GetAsync(trading.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(rotatedCurrent.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(executed.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(neverRotated.Id))!.NeedsRotation);
        Assert.False((await _service.GetAsync(draft.Id))!.NeedsRotation);
    }

    private static DocumentUpsertRequest ExecutedSample() => new()
    {
        GeneralEntitySide = GeneralEntitySideCatalog.Executed,
        DocumentType = "الجهة العامة منفذ عليها",
        FileNumber = "777",
        FileYear = "2024",
        FileRegistrationDate = null,
        ContractTypeSelector = "عادي",
        Court = "دمشق",
        Applicant = "المدعي",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1000m,
        ExecutedPaidAmount = null,
        ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "أحمد", "خالد", "الخطيب", null, "أصالة", null, null, null, null, null, null, null, null, new()),
        },
        ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف العقاري", "فرع المزة"),
        },
        ExecutedNaturalPersons = new()
        {
            new ExecutedNaturalPersonDto(null, "سامر", "حسن", "علي", "عنوان", "دمشق - المزة", "أصالة", null, null, null, null, null, null, null, null, null, new()),
        },
    };

    private static DocumentUpsertRequest DepositSample() => new()
    {
        GeneralEntitySide = GeneralEntitySideCatalog.Deposit,
        DocumentType = "عرض وايداع",
        FileNumber = "888",
        FileYear = "2024",
        FileRegistrationDate = null,
        ContractTypeSelector = "عادي",
        Court = "دمشق",
        Applicant = "معروض",
        FileReceiptDate = "5/1/2024",
        ExecutedRequiredAmount = 1500m,
        ExecutedPaidAmount = null,
        ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "هاني", "سامر", "النجار", null, "أصالة", null, null, null, null, null, null, null, null, new()),
        },
        ExecutedPublicEntities = new()
        {
            new ExecutedPublicEntityDto(null, "المصرف التجاري", "فرع دمشق"),
        },
        ExecutedNaturalPersons = new()
        {
            new ExecutedNaturalPersonDto(null, "رامي", "سالم", "عبد", "عنوان", "دمشق - المدينة", "أصالة", null, null, null, null, null, null, null, null, null, new()),
        },
    };

    [Fact]
    public async Task Create_ExecutedSide_SetsSideAndParties()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        Assert.Equal(GeneralEntitySideCatalog.Executed, doc.GeneralEntitySide);
        Assert.Equal("الجهة العامة منفذ عليها", doc.GeneralEntitySideLabel);
        Assert.False(doc.IsDraft);
        Assert.Equal(ExecutedStatusCatalog.None, doc.ExecutedStatus);
        Assert.Single(doc.ExecutionApplicants);
        Assert.Single(doc.ExecutedPublicEntities);
        Assert.Single(doc.ExecutedNaturalPersons);
        Assert.Equal("أحمد", doc.ExecutionApplicants[0].Name);
        Assert.Equal("المصرف العقاري", doc.ExecutedPublicEntities[0].EntityName);
        Assert.Equal(new DateTime(2024, 1, 5), doc.FileReceiptDate);
        Assert.Equal(1000m, doc.ExecutedRequiredAmount);
        Assert.Null(doc.ExecutedPaidAmount);
    }

    [Fact]
    public async Task Create_ExecutedSide_WithDeceasedAndHeirs_MapsThem()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants = new()
        {
            new ExecutionApplicantDto(null, "أحمد", "خالد", "الخطيب", "الوكيل", "إضافة لتركة",
                "مورث", "م1", "م2", null, null, null, null, null, new()
                {
                    new ExecutedHeirDto(null, "وريث أول", "والد1", "عائلة1", "عنوان", "دمشق"),
                    new ExecutedHeirDto(null, "وريث ثان", "والد2", "عائلة2", null, null),
                }),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var applicant = Assert.Single(doc.ExecutionApplicants);
        Assert.Equal("إضافة لتركة", applicant.RepresentationType);
        Assert.Equal("مورث", applicant.DeceasedName);
        Assert.NotNull(applicant.Heirs);
        Assert.Equal(2, applicant.Heirs.Count);
        Assert.Equal("عنوان", applicant.Heirs[0].AddressType);
        Assert.Equal("وريث أول", applicant.Heirs[0].HeirName);
        Assert.Equal("والد1", applicant.Heirs[0].HeirFather);
        Assert.Equal("عائلة1", applicant.Heirs[0].HeirFamily);
    }

    [Fact]
    public async Task Create_ExecutedSide_WithCombinedCapacity_MapsDeceased()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants[0] = req.ExecutionApplicants[0] with
        {
            RepresentationType = "أصالة وإضافة",
            DeceasedName = "المورث",
            DeceasedFather = "أب المورث",
            DeceasedFamily = "نسبة المورث",
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var applicant = Assert.Single(doc.ExecutionApplicants);
        Assert.Equal("أصالة وإضافة", applicant.RepresentationType);
        Assert.Equal("المورث", applicant.DeceasedName);
        Assert.Equal("أب المورث", applicant.DeceasedFather);
    }

    [Fact]
    public async Task Create_ExecutedSide_ApplicantWithRepresentative_MapsIt()
    {
        var req = ExecutedSample();
        req.ExecutionApplicants[0] = req.ExecutionApplicants[0] with
        {
            RepresentativeName = "الممثل الشرعي",
            RepresentativeFather = "أب",
            RepresentativeFamily = "نسبة",
            RepresentativeCapacity = "قيم",
            RepresentativeLegalRepresentative = "المحامي القانوني",
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var applicant = Assert.Single(doc.ExecutionApplicants);
        Assert.Equal("الممثل الشرعي", applicant.RepresentativeName);
        Assert.Equal("قيم", applicant.RepresentativeCapacity);
        Assert.Equal("المحامي القانوني", applicant.RepresentativeLegalRepresentative);
    }

    [Fact]
    public async Task Create_ExecutedSide_NaturalPersonWithRepresentative_MapsIt()
    {
        var req = ExecutedSample();
        req.ExecutedNaturalPersons[0] = req.ExecutedNaturalPersons[0] with
        {
            RepresentativeName = "ممثل الشخص",
            RepresentativeCapacity = "ولي",
            RepresentativeAddressType = "عنوان",
            RepresentativeAddress = "حلب",
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var person = Assert.Single(doc.ExecutedNaturalPersons);
        Assert.Equal("ممثل الشخص", person.RepresentativeName);
        Assert.Equal("ولي", person.RepresentativeCapacity);
        Assert.Equal("عنوان", person.RepresentativeAddressType);
        Assert.Equal("حلب", person.RepresentativeAddress);
    }

    [Fact]
    public async Task Create_ApplicantSide_WithBorrowerRepresentative_MapsIt()
    {
        var req = Sample();
        req.BorrowerRepresentativeName = "ممثل المقترض";
        req.BorrowerRepresentativeFather = "أب";
        req.BorrowerRepresentativeFamily = "نسبة";
        req.BorrowerRepresentativeCapacity = "ولي";
        req.BorrowerRepresentativeAddressType = "وكيل قانوني";
        req.BorrowerRepresentativeAddress = "المحامي سامر";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal("ممثل المقترض", doc.BorrowerRepresentativeName);
        Assert.Equal("أب", doc.BorrowerRepresentativeFather);
        Assert.Equal("نسبة", doc.BorrowerRepresentativeFamily);
        Assert.Equal("ولي", doc.BorrowerRepresentativeCapacity);
        Assert.Equal("وكيل قانوني", doc.BorrowerRepresentativeAddressType);
        Assert.Equal("المحامي سامر", doc.BorrowerRepresentativeAddress);
    }

    [Fact]
    public async Task Create_ApplicantSide_WithGuarantorRepresentative_MapsIt()
    {
        var req = Sample();
        req.Guarantors[0] = req.Guarantors[0] with
        {
            RepresentativeName = "ممثل الكفيل",
            RepresentativeFather = "أب2",
            RepresentativeFamily = "نسبة2",
            RepresentativeCapacity = "وصي",
            RepresentativeAddressType = "موطن مختار",
            RepresentativeAddress = "دمشق",
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var guarantor = Assert.Single(doc.Guarantors);
        Assert.Equal("ممثل الكفيل", guarantor.RepresentativeName);
        Assert.Equal("وصي", guarantor.RepresentativeCapacity);
        Assert.Equal("موطن مختار", guarantor.RepresentativeAddressType);
        Assert.Equal("دمشق", guarantor.RepresentativeAddress);
    }

    [Fact]
    public async Task Create_ApplicantSide_HeirCapacityAndDomicile_Normalize()
    {
        var req = Sample();
        req.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "وريث", null, null, "إضافة لتركة", "موطن مختار", "دمشق - المزة"),
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        var heir = Assert.Single(doc.BorrowerHeirs);
        Assert.Equal("إضافة لتركة", heir.Capacity);
        Assert.Equal("موطن مختار", heir.AddressType);
    }

    [Fact]
    public async Task Create_ApplicantSide_Representative_IgnoresEmptyTripleName()
    {
        var req = Sample();
        req.BorrowerRepresentativeName = "  ";
        req.BorrowerRepresentativeCapacity = "ولي";
        req.BorrowerRepresentativeAddressType = "عنوان";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Null(doc.BorrowerRepresentativeName);
        Assert.Null(doc.BorrowerRepresentativeCapacity);
        Assert.Null(doc.BorrowerRepresentativeAddressType);
    }

    [Fact]
    public async Task Create_ApplicantSide_Representative_NormalizesInvalidValues()
    {
        var req = Sample();
        req.BorrowerRepresentativeName = "ممثل";
        req.BorrowerRepresentativeCapacity = "غريب";
        req.BorrowerRepresentativeAddressType = "نوع غير معروف";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal("ممثل", doc.BorrowerRepresentativeName);
        Assert.Equal("", doc.BorrowerRepresentativeCapacity);
        Assert.Equal("عنوان", doc.BorrowerRepresentativeAddressType);
    }

    [Fact]
    public async Task Create_ExecutedSide_RejectsBankingContract()
    {
        var req = ExecutedSample();
        req.ContractTypeSelector = "مصرفي";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_ExecutedSide_RejectsGuarantorsOrEstates()
    {
        var req = ExecutedSample();
        req.Guarantors.Add(new GuarantorDto(null, 1, "كفيل", "ح", "ع", null, null, null, null, null, null, null, null, null, null, null, null, new()));

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_ExecutedSide_RequiresFileNumber()
    {
        var req = ExecutedSample();
        req.FileNumber = "";
        req.FileYear = "";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_ExecutedSide_DoesNotRequireRegistrationDate()
    {
        // ملف «منفذ عليها» لا يحمل تاريخ قيد (يقيده الخصم)، فالتحقق من تاريخ القيد
        // يُستثنى لهذه الصفة حتى مع إدخال رقم وسنة الملف.
        var req = ExecutedSample();
        req.FileNumber = "999";
        req.FileYear = "2025";
        req.FileRegistrationDate = "";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(GeneralEntitySideCatalog.Executed, doc.GeneralEntitySide);
        Assert.False(doc.IsDraft);
    }

    [Fact]
    public async Task Create_ExecutedSide_WhenStruckOff_AppliesSubmittedStruckOffDate()
    {
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "10/6/2024";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(ExecutedStatusCatalog.StruckOff, doc.ExecutedStatus);
        Assert.Equal(new DateTime(2024, 6, 10), doc.StruckOffDate);
    }

    [Fact]
    public async Task Create_ExecutedSide_WhenStruckOff_RejectsInvalidStruckOffDate()
    {
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "ليست تاريخًا";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ الشطب", ex.Message);
    }

    [Fact]
    public async Task Create_ExecutedSide_AcceptsFreeTextFileReceiptDate()
    {
        // «تاريخ ورود الاخطار» نص حر بصيغ مألوفة (1/5/2024، yyyy-MM-dd) ويُخزَّن زمنيًا.
        var req = ExecutedSample();
        req.FileReceiptDate = "2024-01-05";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(new DateTime(2024, 1, 5), doc.FileReceiptDate);
    }

    [Fact]
    public async Task Create_ExecutedSide_RejectsInvalidFileReceiptDate()
    {
        var req = ExecutedSample();
        req.FileReceiptDate = "ليست تاريخًا";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ ورود الاخطار", ex.Message);
    }

    [Fact]
    public async Task Create_ExecutedSide_SavesFileReceiptNumber()
    {
        var req = ExecutedSample();
        req.FileReceiptNumber = "  77  ";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal("77", doc.FileReceiptNumber);
    }

    [Fact]
    public async Task Create_ApplicantSide_DoesNotStoreFileReceiptNumber()
    {
        var req = Sample();
        req.FileReceiptNumber = "77";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Null(doc.FileReceiptNumber);
    }

    [Fact]
    public async Task Create_ApplicantSide_StillRequiresBorrowerName()
    {
        var req = Sample();
        req.BorrowerName = "";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Update_ExecutedSide_DoesNotChangeStoredSide()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var req = Sample();
        req.GeneralEntitySide = GeneralEntitySideCatalog.Applicant;
        var updated = await _service.UpdateAsync(doc.Id, req, "lawyer1", 1);

        // صفة الملف تُثبَّت عند الإنشاء ولا تتغير عند التعديل.
        Assert.Equal(GeneralEntitySideCatalog.Executed, updated!.GeneralEntitySide);
    }

    [Fact]
    public async Task UpdateExecutedStatus_SetsStruckOffWithDate()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.StruckOff, updated!.ExecutedStatus);
        Assert.NotNull(updated.StruckOffDate);
        Assert.Contains("executed-status", _audit.Actions);
    }

    [Fact]
    public async Task RestoreStruckOff_KeepsStruckOffDateButClearsStatus()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        var struckOffDate = (await _service.GetAsync(doc.Id))!.StruckOffDate;

        var ok = await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest
        {
            RenewalFileNumber = "2026/55",
            RenewalFileType = "قضية تنفيذ",
            RenewalFileReceiptNumber = "45",
            RenewalFileReceiptDate = "1/2/2026",
            RenewalDate = "5/2/2026",
        }, "lawyer1");
        Assert.True(ok);

        var restored = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.None, restored!.ExecutedStatus);
        Assert.Equal(struckOffDate, restored.StruckOffDate);
        Assert.Equal("2026/55", restored.RenewalFileNumber);
        Assert.Equal("قضية تنفيذ", restored.RenewalFileType);
        Assert.Equal(new DateTime(2026, 2, 5), restored.RenewalDate);
        Assert.Equal("2026/55", restored.DisplayFileNumber);
        Assert.Contains("restore-struck-off", _audit.Actions);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ReStrikeAfterRestore_RefreshesStruckOffDate()
    {
        // شطب ثم فكّ الشطب (يُبقي تاريخ الشطب السابق للعرض بعد الإعادة) ثم شطب جديد:
        // يجب أن يحمل الشطبُ الجديد تاريخَه الخاص وأن يُسجَّل وقعتا شطب في السجل الزمني.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        var firstStruckOffDate = (await _service.GetAsync(doc.Id))!.StruckOffDate!.Value;

        await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest { RenewalFileNumber = "2026/55" }, "lawyer1");

        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        var reStruck = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.StruckOff, reStruck!.ExecutedStatus);
        Assert.NotNull(reStruck.StruckOffDate);
        Assert.True(reStruck.StruckOffDate!.Value > firstStruckOffDate.AddSeconds(-2),
            "الشطب الجديد يجب أن يحمل تاريخًا أحدث من تاريخ الشطب الأول");
        Assert.Equal(2, reStruck.Occurrences.Count(o => o.OccurrenceType == OccurrenceTypeCatalog.StruckOff));
    }

    [Fact]
    public async Task RestoreStruckOff_WithoutRenewalFileNumber_Throws()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest(), "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_OnApplicantSideFile_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_WithInvalidStatus_Throws()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, "حالة-غير-صالحة", "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToExecuted_PersistsExecutedFields()
    {
        // حفظ حقول الحالة عند الانتقال إلى «منفذ»: المبلغ وكيفية التنفيذ والوصف تُحفظ
        // ولا تُمسّ المحفوظة سابقًا في سواها.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 2000m,
                ExecutedDescription = "منفذ جزئيا",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.Executed, updated!.ExecutedStatus);
        Assert.Equal(2000m, updated.ExecutedPaidAmount);
        Assert.Equal("منفذ جزئيا", updated.ExecutedDescription);
        Assert.Contains("executed-status", _audit.Actions);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToExecuted_PersistsThreePaidAmountsWithCurrencies()
    {
        // المبلغ المدفوع حتى ثلاثة بعملاتها (منهج «كل مبلغ له عملة»): تُحفظ كل خانة بعملتها.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 2000m,
                ExecutedPaidCurrency = "دولار أمريكي",
                ExecutedPaidAmount2 = 300m,
                ExecutedPaidCurrency2 = "يورو",
                ExecutedPaidAmount3 = 60m,
                ExecutedPaidCurrency3 = "ليرة سورية",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(2000m, updated!.ExecutedPaidAmount);
        Assert.Equal("دولار أمريكي", updated.ExecutedPaidCurrency);
        Assert.Equal(300m, updated.ExecutedPaidAmount2);
        Assert.Equal("يورو", updated.ExecutedPaidCurrency2);
        Assert.Equal(60m, updated.ExecutedPaidAmount3);
        Assert.Equal("ليرة سورية", updated.ExecutedPaidCurrency3);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToExecuted_KeepsPriorPaidSlotsWhenNotProvided()
    {
        // الإعادة إلى «منفذ» بحقول فارغة تُبقي ما سبق تسجيله: عند تحديث خانة أولى فقط
        // لا تُمسّ الثانية والثالثة المحفوظتان سابقًا.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1000m,
                ExecutedPaidCurrency = "دولار أمريكي",
                ExecutedPaidAmount2 = 200m,
                ExecutedPaidCurrency2 = "يورو",
            }, "lawyer1");

        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1500m,
            }, "lawyer1");

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(1500m, updated!.ExecutedPaidAmount);
        Assert.Equal("دولار أمريكي", updated.ExecutedPaidCurrency);
        Assert.Equal(200m, updated.ExecutedPaidAmount2);
        Assert.Equal("يورو", updated.ExecutedPaidCurrency2);
        Assert.Null(updated.ExecutedPaidAmount3);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToExecuted_KeepsPriorFieldsWhenNotProvided()
    {
        // الإعادة إلى «منفذ» بوصف وتاريخ تنفيذ فارغين تُبقي ما سبق تسجيله (مبدأ عدم المساس).
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1000m,
                ExecutedDescription = "منفذ جزئيا",
                ExecutedExecutionDate = "1/6/2026",
            }, "lawyer1");

        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1200m,
            }, "lawyer1");

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(1200m, updated!.ExecutedPaidAmount);
        Assert.Equal("منفذ جزئيا", updated.ExecutedDescription);
        Assert.Equal(new DateTime(2026, 6, 1), updated.ExecutedExecutionDate);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToExecuted_OnDeposit_PersistsDepositFields()
    {
        // «عرض وايداع» عند الانتقال إلى «منفذ»: يُحفظ المبلغ المودع وتاريخ الإيداع وتُضبط
        // علامة «سبق تنفيذه»، ويبقى الوصف فارغًا (الصفة لا تحمله).
        var doc = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1250m,
                ExecutedDepositDate = "10/6/2024",
                ExecutedDescription = "لا يُحفظ في العرض",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.Executed, updated!.ExecutedStatus);
        Assert.Equal(1250m, updated.ExecutedPaidAmount);
        Assert.Equal(new DateTime(2024, 6, 10), updated.ExecutedDepositDate);
        Assert.Null(updated.ExecutedDescription);
    }

    [Fact]
    public async Task UpdateExecutedStatus_ToStruckOff_UsesSubmittedDate()
    {
        // عند الدخول إلى «مشطوب» يُحدَّث تاريخ الشطب بتاريخه المُرسَل إن وُجد وإلا للآن.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.StruckOff,
                StruckOffDate = "10/6/2026",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.StruckOff, updated!.ExecutedStatus);
        Assert.Equal(new DateTime(2026, 6, 10), updated.StruckOffDate);
    }

    [Fact]
    public async Task UpdateExecutedStatus_FromStruckOffToCirculating_AppliesRenewal()
    {
        // العودة من «مشطوب» إلى متداول تستلزم تجديد الملف برقم ملف جديد لسنة الإعادة.
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.None,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.None,
                RenewalFileNumber = "2026/55",
                RenewalFileType = "قضية تنفيذ",
                RenewalFileReceiptNumber = "45",
                RenewalFileReceiptDate = "1/2/2026",
                RenewalDate = "5/2/2026",
            }, "lawyer1");
        Assert.True(ok);

        var restored = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.None, restored!.ExecutedStatus);
        Assert.Equal("2026/55", restored.RenewalFileNumber);
        Assert.Equal("قضية تنفيذ", restored.RenewalFileType);
        Assert.Equal(new DateTime(2026, 2, 5), restored.RenewalDate);
        Assert.Equal("2026/55", restored.DisplayFileNumber);
    }

    [Fact]
    public async Task UpdateExecutedStatus_FromStruckOffToCirculating_WithoutRenewalNumber_Throws()
    {
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.None,
                new ExecutedStatusRequest { Status = ExecutedStatusCatalog.None }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_ExecutedSide_FromExecuted_IsTerminal()
    {
        // حالة «منفذ» في صفة «الجهة العامة منفذ عليها» نهائية: لا تُغيَّر إلى متداول ولا إلى مشطوب
        // (ويبقى الدخول مجددًا إلى «منفذ» ذاتها مسموحًا لتحديث حقول الحالة).
        var doc = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1000m,
            }, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.None, "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_Deposit_FromExecutedToStruckOff_Throws()
    {
        // «عرض وايداع» المنفذ لا يُشطب؛ يمكن إرجاعه إلى متداول بكتاب الجهة العامة بالسير بالملف.
        var doc = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1250m,
            }, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1"));
    }

    [Fact]
    public async Task UpdateExecutedStatus_Deposit_FromExecutedToCirculating_RequiresSayerAndKeepsAmount()
    {
        // الإرجاع من «منفذ» إلى «متداول» في «عرض وايداع»: كتاب الجهة العامة بالسير بالملف
        // إلزامي (رقم وتاريخ الكتاب وورودهما)، ويُحفظ مع بقاء المبالغ المودعة.
        var doc = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.Executed,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.Executed,
                ExecutedPaidAmount = 1250m,
                ExecutedDepositDate = "10/6/2024",
            }, "lawyer1");

        // دون كتاب السير: يرفض الإرجاع.
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.None,
                new ExecutedStatusRequest { Status = ExecutedStatusCatalog.None }, "lawyer1"));

        // مع كتاب السير: يُرجع مع بقاء المبلغ المودع وتاريخ الإيداع.
        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.None,
            new ExecutedStatusRequest
            {
                Status = ExecutedStatusCatalog.None,
                SayerNumber = "45/2024",
                SayerDate = "1/2/2026",
                SayerRegNumber = "88",
                SayerRegDate = "2/2/2026",
            }, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.None, updated!.ExecutedStatus);
        Assert.Equal(1250m, updated.ExecutedPaidAmount);
        Assert.Equal(new DateTime(2024, 6, 10), updated.ExecutedDepositDate);
        Assert.Equal("45/2024", updated.SayerNumber);
        Assert.Contains("executed-status", _audit.Actions);
    }

    [Fact]
    public async Task Create_ExecutedSide_PersistsThreePaidAmountsWithCurrencies()
    {
        var req = ExecutedSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        req.ExecutedPaidAmount = 2000m;
        req.ExecutedPaidCurrency = "دولار أمريكي";
        req.ExecutedPaidAmount2 = 300m;
        req.ExecutedPaidCurrency2 = "يورو";
        req.ExecutedPaidAmount3 = 60m;
        req.ExecutedPaidCurrency3 = "ليرة سورية";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(2000m, loaded!.ExecutedPaidAmount);
        Assert.Equal("دولار أمريكي", loaded.ExecutedPaidCurrency);
        Assert.Equal(300m, loaded.ExecutedPaidAmount2);
        Assert.Equal("يورو", loaded.ExecutedPaidCurrency2);
        Assert.Equal(60m, loaded.ExecutedPaidAmount3);
        Assert.Equal("ليرة سورية", loaded.ExecutedPaidCurrency3);
    }

    [Fact]
    public async Task SearchStruckOff_ReturnsOnlyStruckOffExecutedFiles()
    {
        var struck = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(struck.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1); // منفذ عليه متداول
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);          // طالبة تنفيذ

        var result = await _service.SearchStruckOffAsync(null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(struck.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task Create_DepositSide_SetsSideLabelAndDepositStatusFields()
    {
        var req = DepositSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        req.ExecutedPaidAmount = 1250m;
        req.ExecutedDepositDate = "10/6/2024";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(GeneralEntitySideCatalog.Deposit, doc.GeneralEntitySide);
        Assert.Equal("عرض وايداع", doc.GeneralEntitySideLabel);
        Assert.False(doc.IsDraft);
        Assert.Equal(ExecutedStatusCatalog.Executed, doc.ExecutedStatus);
        Assert.Equal(1250m, doc.ExecutedPaidAmount);
        Assert.Equal(new DateTime(2024, 6, 10), doc.ExecutedDepositDate);
        // صفة العرض لا تحمل وصفًا إضافيًا: يسجل الفارغ مهما أُرسل.
        Assert.Null(doc.ExecutedDescription);
        Assert.Equal(new DateTime(2024, 1, 5), doc.FileReceiptDate);
    }

    [Fact]
    public async Task Create_DepositSide_RejectsInvalidExecutedDepositDate()
    {
        var req = DepositSample();
        req.ExecutedStatus = ExecutedStatusCatalog.Executed;
        req.ExecutedDepositDate = "ليست تاريخًا";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("تاريخ ايداعه حساب الجهة العامة", ex.Message);
    }

    [Fact]
    public async Task Create_DepositSide_RejectsBankingContract()
    {
        var req = DepositSample();
        req.ContractTypeSelector = "مصرفي";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_DepositSide_RejectsBorrowerOrGuarantors()
    {
        var req = DepositSample();
        req.BorrowerName = "مقترض لا يليق";

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_DepositSide_RejectsBorrowerHeirs()
    {
        var req = DepositSample();
        req.BorrowerHeirs = new()
        {
            new HeirDto(null, "وريث", "لأب", "متوفى", "أصالة", "عنوان", "عنوان الوريث"),
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task Create_DepositSide_RequiresFileNumber()
    {
        var req = DepositSample();
        req.FileNumber = "";
        req.FileYear = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("عرض وايداع", ex.Message);
    }

    [Fact]
    public async Task Create_DepositSide_DoesNotRequireRegistrationDate()
    {
        var req = DepositSample();
        req.FileNumber = "999";
        req.FileYear = "2025";
        req.FileRegistrationDate = "";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(GeneralEntitySideCatalog.Deposit, doc.GeneralEntitySide);
        Assert.False(doc.IsDraft);
    }

    [Fact]
    public async Task Create_DepositSide_WhenStruckOff_AppliesSubmittedStruckOffDate()
    {
        var req = DepositSample();
        req.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        req.StruckOffDate = "10/6/2024";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.Equal(ExecutedStatusCatalog.StruckOff, doc.ExecutedStatus);
        Assert.Equal(new DateTime(2024, 6, 10), doc.StruckOffDate);
    }

    [Fact]
    public async Task UpdateExecutedStatus_OnDepositSideFile_Works()
    {
        var doc = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        Assert.True(ok);

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(ExecutedStatusCatalog.StruckOff, updated!.ExecutedStatus);
    }

    [Fact]
    public async Task RestoreStruckOff_OnDepositSideFile_Works()
    {
        var doc = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(doc.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        var ok = await _service.RestoreStruckOffAsync(doc.Id, new RenewalRequest
        {
            RenewalFileNumber = "2026/77",
        }, "lawyer1");
        Assert.True(ok);

        Assert.Equal(ExecutedStatusCatalog.None, (await _service.GetAsync(doc.Id))!.ExecutedStatus);
    }

    [Fact]
    public async Task SearchStruckOff_IncludesStruckOffDepositFiles()
    {
        var struck = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(struck.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1); // عرض وايداع متداول

        var result = await _service.SearchStruckOffAsync(null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(struck.Id, result.Items.Single().Id);
    }

    [Fact]
    public async Task SearchAsync_ExcludesStruckOffDepositFiles()
    {
        var struck = await _service.CreateAsync(DepositSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(struck.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, null, null, 1, 20);

        Assert.DoesNotContain(result.Items, d => d.Id == struck.Id);
    }

    [Fact]
    public async Task SearchAsync_ExcludesStruckOffFiles()
    {
        var struck = await _service.CreateAsync(ExecutedSample(), 1, "lawyer1", 1);
        await _service.UpdateExecutedStatusAsync(struck.Id, ExecutedStatusCatalog.StruckOff, "lawyer1");
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, null, null, null, 1, 20);

        Assert.DoesNotContain(result.Items, d => d.Id == struck.Id);
    }
}

public class AuthServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly FakeAuditLogger _audit = new();

    public AuthServiceTests()
    {
        _db = TestDb.Create();
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private AuthService CreateService(int maxFailedAttempts = 5, int lockoutMinutes = 15) => new(
        new UserRepository(_db),
        new UnitOfWork(_db),
        new PasswordHasher(),
        new FakeTokenService(),
        new TransactionRunner(_db),
        _audit,
        Microsoft.Extensions.Options.Options.Create(new LockoutOptions
        {
            MaxFailedAttempts = maxFailedAttempts,
            LockoutMinutes = lockoutMinutes,
        }));

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUser()
    {
        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal("lawyer1", result.Response!.User.Username);
        Assert.Contains("login", _audit.Actions);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "bad"));
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Contains("login_failed", _audit.Actions);
    }

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_LocksAccount()
    {
        var service = CreateService(maxFailedAttempts: 3);

        for (var i = 0; i < 3; i++)
        {
            var failed = await service.LoginAsync(new LoginRequest("lawyer1", "bad"));
            Assert.Equal(LoginStatus.InvalidCredentials, failed.Status);
        }

        Assert.Contains("login_locked", _audit.Actions);

        var locked = await service.LoginAsync(new LoginRequest("lawyer1", "123456"));
        Assert.Equal(LoginStatus.LockedOut, locked.Status);

        var user = _db.Users.First();
        Assert.NotNull(user.LockoutEndUtc);
        Assert.True(user.LockoutEndUtc > DateTime.UtcNow);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public async Task Login_WhileLockedOut_CorrectPasswordStillReturnsLockedOut()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(10);
        user.FailedLoginCount = 2;
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.LockedOut, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(2, _db.Users.First().FailedLoginCount);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_CorrectPasswordSucceeds()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        user.FailedLoginCount = 3;
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(0, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_WrongPassword_StartsFreshCounter()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        user.FailedLoginCount = 3;
        _db.SaveChanges();

        var service = CreateService(maxFailedAttempts: 3);

        var result = await service.LoginAsync(new LoginRequest("lawyer1", "bad"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Equal(1, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task Login_Success_ResetsFailedCount()
    {
        var service = CreateService();
        for (var i = 0; i < 2; i++)
            await service.LoginAsync(new LoginRequest("lawyer1", "bad"));

        Assert.Equal(2, _db.Users.First().FailedLoginCount);

        var result = await service.LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal(0, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectOldPassword_Succeeds()
    {
        var service = CreateService();
        var user = _db.Users.First();
        var ok = await service.ChangePasswordAsync(user.Id, "123456", "newpass");
        Assert.True(ok);

        var login = await service.LoginAsync(new LoginRequest("lawyer1", "newpass"));
        Assert.Equal(LoginStatus.Success, login.Status);
        Assert.NotNull(login.Response);
    }

    [Fact]
    public async Task ChangePassword_BumpsTokenVersion()
    {
        var service = CreateService();
        var user = _db.Users.First();
        var initial = user.TokenVersion;

        var ok = await service.ChangePasswordAsync(user.Id, "123456", "newpass");

        Assert.True(ok);
        Assert.Equal(initial + 1, _db.Users.First().TokenVersion);
    }

    [Fact]
    public async Task ChangePassword_WithShortPassword_Throws()
    {
        var service = CreateService();
        var user = _db.Users.First();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChangePasswordAsync(user.Id, "123456", "123"));
    }

    [Fact]
    public async Task Login_DuplicateNameAcrossBranches_ReturnsBranchSelection()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        var b2 = _db.Branches.Add(new Branch { Name = "فرع ب", Code = "B2" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("فارس أحمد يوسف");
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b2.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("فارس أحمد يوسف", "123456"));

        Assert.Equal(LoginStatus.BranchSelectionRequired, result.Status);
        Assert.Null(result.Response);
        Assert.NotNull(result.Branches);
        Assert.Equal(2, result.Branches!.Count);
        Assert.Contains(result.Branches, c => c.BranchId == b1.Id && c.BranchName == "فرع أ");
        Assert.Contains(result.Branches, c => c.BranchId == b2.Id && c.BranchName == "فرع ب");
        Assert.DoesNotContain("login_failed", _audit.Actions);
    }

    [Fact]
    public async Task Login_DuplicateName_WithBranchId_LogsIntoThatBranch()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        var b2 = _db.Branches.Add(new Branch { Name = "فرع ب", Code = "B2" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("رامي سامر فادي");
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b2.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("رامي سامر فادي", "123456", b2.Id));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal(b2.Id, result.Response!.User.BranchId);
    }

    [Fact]
    public async Task Login_DuplicateName_ZeroBranchId_SelectsNoBranchAccount()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("مازن خالد رشيد");
        var noBranch = new User { Username = name, FullName = name, Role = UserRole.Manager, BranchId = null, PasswordHash = new PasswordHasher().Hash("123456") };
        _db.Users.Add(noBranch);
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("مازن خالد رشيد", "123456", 0));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal(noBranch.Id, result.Response!.User.Id);
    }

    [Fact]
    public async Task Login_DuplicateName_UnknownBranchId_ReturnsInvalidCredentials()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("غسان وائل هاني");
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = null, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("غسان وائل هاني", "123456", 999));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Contains("login_failed", _audit.Actions);
    }

    [Fact]
    public async Task Login_DuplicateName_WrongPassword_DoesNotRevealPasswordValidityAtSelection()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        var b2 = _db.Branches.Add(new Branch { Name = "فرع ب", Code = "B2" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("ياسر محمد سامر");
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b2.Id, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        // المرحلة الأولى تعرض الاختيار دون أي إشارة لصحة كلمة المرور،
        // والتحقق الحقيقي يقع فقط بعد اختيار الفرع.
        var selection = await CreateService().LoginAsync(new LoginRequest("ياسر محمد سامر", "wrong-password"));
        Assert.Equal(LoginStatus.BranchSelectionRequired, selection.Status);
        Assert.DoesNotContain("login_failed", _audit.Actions);

        var failed = await CreateService().LoginAsync(new LoginRequest("ياسر محمد سامر", "wrong-password", b1.Id));
        Assert.Equal(LoginStatus.InvalidCredentials, failed.Status);
        Assert.Contains("login_failed", _audit.Actions);
    }

    [Fact]
    public async Task Login_DuplicateNameWithInactiveAccount_SkipsBranchSelection()
    {
        var b1 = _db.Branches.Add(new Branch { Name = "فرع أ", Code = "B1" }).Entity;
        var b2 = _db.Branches.Add(new Branch { Name = "فرع ب", Code = "B2" }).Entity;
        _db.SaveChanges();
        var name = ArabicNameNormalizer.Normalize("نائل عمر عادل");
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b1.Id, IsActive = true, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.Users.Add(new User { Username = name, FullName = name, Role = UserRole.Lawyer, BranchId = b2.Id, IsActive = false, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("نائل عمر عادل", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal(b1.Id, result.Response!.User.BranchId);
    }
}

public class ConsiderDelegationExecutedTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public ConsiderDelegationExecutedTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User { Username = "lawyer1", FullName = "محامي", Role = UserRole.Lawyer, BranchId = 1, PasswordHash = new PasswordHasher().Hash("123456") });
        _db.SaveChanges();
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(
            new DocumentRepository(_db), new UserRepository(_db), new Repository<Guarantor>(_db),
            new Repository<Asset>(_db), new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db), new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db), new DelegationRepository(_db), uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Document> CreateSourceAsync()
    {
        var req = new DocumentUpsertRequest
        {
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1000,
            Currency = "ليرة سورية",
            ContractNumber = "1/2025",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = "900",
            FileYear = "2025",
            FileRegistrationDate = "2/1/2025",
            Assets = new()
            {
                new AssetDto(null, AssetKindCatalog.RealEstate, new List<string> { "المدعى عليه" }, "تمام العقار",
                    "بيت", "1", "المزة", "الصالحية",
                    null, null, null, null,
                    null, null, null, null, null,
                    null,
                    null, null, null,
                    null),
            },
        };
        var dto = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);
        return await _db.Documents.SingleAsync(d => d.Id == dto.Id);
    }

    private async Task<Document> SourceInPartialForciblyAsync()
    {
        var doc = await CreateSourceAsync();
        doc.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
        doc.ExecSubStatus = ExecutionStatusCatalog.SubPartiallyExecuted;
        _db.Documents.Update(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    private async Task AddExecutedDelegationAsync(int sourceId)
    {
        _db.DocumentDelegations.Add(new DocumentDelegation
        {
            SourceDocumentId = sourceId,
            CreatedById = 1,
            DelegatedCourt = "دائرة تنفيذ حلب",
            Status = DelegationStatusCatalog.Executed,
            Assets = new List<DelegationAsset>
            {
                new() { AssetKind = AssetKindCatalog.RealEstate, AssetLabel = "بيت", SalePrice = 500 },
            },
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ConsiderExecuted_WithExecutedDelegation_MarksFullyAndPersistsTransferFields()
    {
        var source = await SourceInPartialForciblyAsync();
        await AddExecutedDelegationAsync(source.Id);

        var ok = await _service.ConsiderExecutedByDelegationAsync(source.Id, new Dictionary<string, string?>
        {
            ["forcedTransferDate"] = "١٥/٨/٢٠٢٦",
            ["forcedTransferNoticeNumber"] = "77/2026",
        }, "lawyer1");

        Assert.True(ok);
        var loaded = await _db.Documents.SingleAsync(d => d.Id == source.Id);
        Assert.Equal(ExecutionStatusCatalog.SubFullyExecuted, loaded.ExecSubStatus);
        Assert.Equal(new DateTime(2026, 8, 15), loaded.ForcibleTransferDate);
        Assert.Equal("77/2026", loaded.ForcibleTransferNoticeNumber);

        // وقعة «منفذ جبريا» كاملة بحقولها تُسجَّل في «وقوعات الملف».
        var occ = await _db.DocumentOccurrences.SingleAsync(o => o.DocumentId == source.Id);
        Assert.Equal(OccurrenceTypeCatalog.Forcible, occ.OccurrenceType);
        Assert.Contains("forcedTransferDate", occ.Details);
        Assert.Contains("15/8/2026", occ.Details);
        Assert.Contains("77/2026", occ.Details);
        Assert.Contains("status", _audit.Actions);
    }

    [Fact]
    public async Task ConsiderExecuted_TransferNoticeOptional_PersistsNullWhenAbsent()
    {
        var source = await SourceInPartialForciblyAsync();
        await AddExecutedDelegationAsync(source.Id);

        var ok = await _service.ConsiderExecutedByDelegationAsync(source.Id, new Dictionary<string, string?>
        {
            ["forcedTransferDate"] = "15/8/2026",
            ["forcedTransferNoticeNumber"] = "   ",
        }, "lawyer1");

        Assert.True(ok);
        var loaded = await _db.Documents.SingleAsync(d => d.Id == source.Id);
        Assert.Equal(new DateTime(2026, 8, 15), loaded.ForcibleTransferDate);
        Assert.Null(loaded.ForcibleTransferNoticeNumber);
    }

    [Fact]
    public async Task ConsiderExecuted_WithoutExecutedDelegation_Throws()
    {
        var source = await SourceInPartialForciblyAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ConsiderExecutedByDelegationAsync(source.Id, new Dictionary<string, string?>
            {
                ["forcedTransferDate"] = "15/8/2026",
            }, "lawyer1"));

        Assert.Contains("لا توجد إنابة منفذة", ex.Message);
    }

    [Fact]
    public async Task ConsiderExecuted_FromWrongState_Throws()
    {
        var source = await CreateSourceAsync();
        await AddExecutedDelegationAsync(source.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ConsiderExecutedByDelegationAsync(source.Id, new Dictionary<string, string?>
            {
                ["forcedTransferDate"] = "15/8/2026",
            }, "lawyer1"));

        Assert.Contains("لا يمكن اعتبار الملف منفذًا كاملًا بهذا البيع", ex.Message);
    }

    [Fact]
    public async Task ConsiderExecuted_MissingTransferDate_Throws()
    {
        var source = await SourceInPartialForciblyAsync();
        await AddExecutedDelegationAsync(source.Id);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.ConsiderExecutedByDelegationAsync(source.Id, new Dictionary<string, string?>(), "lawyer1"));

        Assert.Contains("تاريخ تحويل بدل المبيع للجهة العامة", ex.Message);
    }

    [Fact]
    public async Task ConsiderExecuted_UnknownDocument_ReturnsFalse()
    {
        var ok = await _service.ConsiderExecutedByDelegationAsync(999_999, new Dictionary<string, string?>(), "lawyer1");
        Assert.False(ok);
    }

    [Fact]
    public async Task UpdateSourceAssets_RefreshesPendingDelegationSnapshot_FlagsAdjusted_LeavesExecutedUntouched()
    {
        var doc = await CreateSourceAsync();
        var asset = await _db.Assets.SingleAsync(a => a.DocumentId == doc.Id);
        var originalLabel = AssetDisplay.Label(asset);
        Assert.Equal("بيت", originalLabel);

        _db.DocumentDelegations.AddRange(
            new DocumentDelegation
            {
                SourceDocumentId = doc.Id,
                CreatedById = 1,
                DelegatedCourt = "دائرة تنفيذ حلب",
                Status = DelegationStatusCatalog.PendingHead,
                Assets = new List<DelegationAsset>
                {
                    new() { AssetKind = AssetKindCatalog.RealEstate, AssetLabel = originalLabel },
                },
            },
            new DocumentDelegation
            {
                SourceDocumentId = doc.Id,
                CreatedById = 1,
                DelegatedCourt = "دائرة تنفيذ حماة",
                Status = DelegationStatusCatalog.Executed,
                Assets = new List<DelegationAsset>
                {
                    new() { AssetKind = AssetKindCatalog.RealEstate, AssetLabel = originalLabel, SalePrice = 500 },
                },
            });
        await _db.SaveChangesAsync();
        var pendingDelegationId = await _db.DocumentDelegations
            .Where(d => d.SourceDocumentId == doc.Id && d.Status == DelegationStatusCatalog.PendingHead)
            .Select(d => d.Id)
            .SingleAsync();

        // تعديل وصف الأصل في الملف المنيب (نفس النوع) يُحدِّث لقطة الإنابة المعلقة ويُعلِّمها؛
        // أما الإنابة المنفذة فسجل نهائي بالبدل فلا تُمسّ لقطتها.
        var req = new DocumentUpsertRequest
        {
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1000,
            Currency = "ليرة سورية",
            ContractNumber = "1/2025",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = "900",
            FileYear = "2025",
            FileRegistrationDate = "2/1/2025",
            Assets = new()
            {
                new AssetDto(asset.Id, AssetKindCatalog.RealEstate, new List<string> { "المدعى عليه" },
                    "تمام العقار", "بيت — المزة 77", asset.PropertyNumber, asset.PropertyDistrict, asset.LandRegistry,
                    null, null, null, null,
                    null, null, null, null, null,
                    null,
                    null, null, null,
                    null),
            },
        };
        await _service.UpdateAsync(doc.Id, req, actorName: "lawyer1", userId: 1);

        var pendingSnapshot = await _db.DelegationAssets
            .SingleAsync(a => a.DelegationId == pendingDelegationId);
        Assert.Equal("بيت — المزة 77", pendingSnapshot.AssetLabel);
        Assert.True(pendingSnapshot.SnapshotAdjusted);

        var executedSnapshot = await _db.DelegationAssets
            .SingleAsync(a => a.Delegation.SourceDocumentId == doc.Id
                && a.Delegation.Status == DelegationStatusCatalog.Executed);
        Assert.Equal(originalLabel, executedSnapshot.AssetLabel);
        Assert.False(executedSnapshot.SnapshotAdjusted);
    }
}

public class FakeTokenService : ITokenService
{
    public string CreateToken(User user) => "fake-token";
}
