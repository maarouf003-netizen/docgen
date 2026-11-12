using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.Tests.TestHelpers;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// خدمة «المتدخلين»: عقد الإضافة/التعديل/الحذف، تطبيع الحقول حسب الصفة،
/// اعتماد بيانات الجهة العامة من السجل المرجعي، ومزامنة وقعة «طلب تدخل»
/// (إنشاءً وتعديلًا وحذفًا) وإعادة بناء نص البحث — كل ذلك في القاعدة.
/// </summary>
public class DocumentInterventionServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly DocumentInterventionService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer;
    private readonly Document _document;

    public DocumentInterventionServiceTests()
    {
        _db = TestDb.Create();
        var branch = new Branch { Name = "دمشق", Code = "DAM" };
        _db.Branches.Add(branch);
        _db.SaveChanges();

        _lawyer = new User
        {
            Username = "law1",
            FullName = "المحامي الأول",
            Role = UserRole.Lawyer,
            BranchId = branch.Id,
            PasswordHash = new PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();

        _document = new Document
        {
            BranchId = branch.Id,
            CreatedById = _lawyer.Id,
            Lawyer = "المحامي الأول",
            BorrowerName = "أحمد",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
        };
        _db.Documents.Add(_document);
        _db.SaveChanges();

        _service = new DocumentInterventionService(
            new DocumentRepository(_db),
            new Repository<PublicEntity>(_db),
            new Repository<PublicEntityGroup>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private static DocumentInterventionUpsertRequest NaturalRequest() => new()
    {
        Nature = PartyNatureCatalog.Natural,
        ReceiptNumber = "123",
        ReceiptDate = "1/8/2026",
        AcceptanceDate = "28/7/2026",
        Subject = "طلب تدخل في الملف",
        Notes = "ملاحظة أولى\nملاحظة ثانية",
        Name = "سامر",
        Father = "محمود",
        Family = "الحسن",
        Mother = "ليلى",
        Birth = "دمشق 1980",
        Register = "دمشق 123",
        NationalId = "01020304",
        AddressType = "موطن مختار",
        Address = "المزة",
    };

    private static DocumentInterventionUpsertRequest LegalRequest() => new()
    {
        Nature = PartyNatureCatalog.Legal,
        ReceiptDate = "2/8/2026",
        EntityName = "شركة النور",
        RegistrationNumber = "987",
        RepresentedBy = "مديرها",
        Governorate = "دمشق",
        AddressType = "عنوان",
        Address = "المالكي",
    };

    private async Task<(PublicEntityGroup Group, PublicEntity Entry)> SeedPublicEntityAsync(
        string name = "وزارة العدل", string branch = "مديرية التنفيذ", bool active = true)
    {
        var group = new PublicEntityGroup
        {
            CanonicalName = name,
            EntityType = PublicEntityTypeCatalog.Ministry,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
        };
        _db.PublicEntityGroups.Add(group);
        await _db.SaveChangesAsync();
        var entry = new PublicEntity
        {
            GroupId = group.Id,
            Governorate = "دمشق",
            BranchName = branch,
            Status = EntityStatusCatalog.Final,
            IsActive = active,
            CreatedById = _lawyer.Id,
            CreatedAt = DateTime.UtcNow,
        };
        _db.PublicEntities.Add(entry);
        await _db.SaveChangesAsync();
        return (group, entry);
    }

    [Fact]
    public async Task Add_NaturalWithHeirs_PersistsAndCreatesInterventionOccurrence()
    {
        var request = NaturalRequest();
        request.Heirs.Add(new HeirDto(null, "وليد", "سامر", "الحسن", "أصالة", "عنوان", "المزة"));

        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.True(dto.Id > 0);
        Assert.Equal("سامر محمود الحسن", dto.DisplayName);
        Assert.Equal("2026-08-01", dto.ReceiptDate);
        Assert.Equal("2026-07-28", dto.AcceptanceDate);
        Assert.Single(dto.Heirs);
        Assert.Null(dto.AddressType);
        Assert.Null(dto.Address);

        var stored = await _db.DocumentInterventions
            .Include(i => i.Heirs)
            .SingleAsync(i => i.Id == dto.Id);
        Assert.Equal(PartyNatureCatalog.Natural, stored.Nature);
        Assert.Equal("سامر", stored.Name);
        Assert.Equal("01020304", stored.NationalId);
        Assert.Single(stored.Heirs);

        var occurrence = await _db.DocumentOccurrences.SingleAsync();
        Assert.Equal(OccurrenceTypeCatalog.Intervention, occurrence.OccurrenceType);
        Assert.Equal(OccurrenceSourceCatalog.System, occurrence.Source);
        Assert.Equal(new DateTime(2026, 8, 1), occurrence.EventDate!.Value.Date);
        Assert.Equal(dto.Id, occurrence.DocumentInterventionId);
        var (details, text) = OccurrenceDetails.Split(occurrence.Details);
        Assert.Null(text);
        Assert.Equal("سامر محمود الحسن", details!["intervenorName"]);
        Assert.Equal("2026-07-28", details["acceptanceDate"]);
        Assert.Equal(InterventionOccurrenceLabel, OccurrenceTypeCatalog.ToLabel(occurrence.OccurrenceType));

        var refreshed = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _document.Id);
        Assert.Contains("سامر", refreshed.SearchText);
        Assert.Contains("وليد", refreshed.SearchText);

        Assert.Contains("intervention", _audit.Actions);
        Assert.Contains(_audit.Details, d => d is not null && d.Contains("سامر محمود الحسن"));
    }

    private const string InterventionOccurrenceLabel = "طلب تدخل";

    [Fact]
    public async Task Add_NormalizesReceiptNumberDigits()
    {
        var request = NaturalRequest();
        request.ReceiptNumber = "ورود ١٢٣";

        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.Equal("ورود 123", dto.ReceiptNumber);
    }

    [Fact]
    public async Task Search_FindsDocumentByInterventionName_EvenWithStaleSearchText()
    {
        var request = NaturalRequest();
        request.Name = "اسم فريد للبحث";
        await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        // محاكاة تقادم/قصّ SearchText: البحث الاسمي المباشر على جدول المتدخلين
        // يجب أن يبقى صالحًا (كبقية فروع الأطراف في الكتلة الموحدة).
        _document.SearchText = "نص قديم لا يضم الاسم";
        await _db.SaveChangesAsync();

        var search = DocumentServiceFactory.Create(_db, _audit).Search;
        var page = await search.SearchAsync(
            "اسم فريد للبحث", null, null, null, null, null, null, null, null,
            page: 1, perPage: 20, visibleBranchId: null, visibleUserId: _lawyer.Id);

        Assert.Contains(page.Items, item => item.Id == _document.Id);
    }

    [Fact]
    public async Task Update_PublicEntity_KeepsSameRegistryAfterDeactivation()
    {
        var (group, entry) = await SeedPublicEntityAsync();
        var addRequest = new DocumentInterventionUpsertRequest
        {
            Nature = PartyNatureCatalog.PublicEntity,
            ReceiptDate = "3/8/2026",
            EntityName = "جهة",
            RegistryId = entry.Id,
        };
        var created = await _service.AddAsync(_document.Id, addRequest, _lawyer.Id, "المحامي الأول");

        // تعطيل القيد والجهة الأم بعد الإضافة (بيانات تاريخية): يبقى التعديل ممكنًا
        // ما دام المتدخل محافظًا على القيد نفسه؛ القيد الجديد يُشترط نشطًا.
        entry.IsActive = false;
        group.IsActive = false;
        await _db.SaveChangesAsync();

        var updated = await _service.UpdateAsync(
            _document.Id,
            created.Id,
            new DocumentInterventionUpsertRequest
            {
                Nature = PartyNatureCatalog.PublicEntity,
                ReceiptDate = "3/8/2026",
                EntityName = "جهة",
                RegistryId = entry.Id,
                Notes = "تعديل لاحق",
            },
            _lawyer.Id,
            "المحامي الأول");

        Assert.NotNull(updated);
        Assert.Equal("وزارة العدل", updated!.EntityName);
        Assert.Equal("تعديل لاحق", updated.Notes);
    }

    [Fact]
    public async Task Add_PublicEntity_WithoutClientName_AdoptsRegistryName()
    {
        var (_, entry) = await SeedPublicEntityAsync();
        var request = new DocumentInterventionUpsertRequest
        {
            Nature = PartyNatureCatalog.PublicEntity,
            ReceiptDate = "3/8/2026",
            RegistryId = entry.Id,
        };

        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.Equal("وزارة العدل", dto.EntityName);
        Assert.Equal("وزارة العدل مديرية التنفيذ", dto.DisplayName);
    }

    [Fact]
    public async Task Add_PublicEntity_AdoptsCanonicalRegistryValues()    {
        var (_, entry) = await SeedPublicEntityAsync();
        var request = new DocumentInterventionUpsertRequest
        {
            Nature = PartyNatureCatalog.PublicEntity,
            ReceiptDate = "3/8/2026",
            // نص عميل مغلوط عمدًا: الخدمة تعتمده من السجل المرجعي.
            EntityName = "اسم مزوّر",
            EntityBranch = "فرع مزوّر",
            Governorate = "محافظة مزوّرة",
            RegistryId = entry.Id,
        };

        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.Equal("وزارة العدل", dto.EntityName);
        Assert.Equal("مديرية التنفيذ", dto.EntityBranch);
        Assert.Equal("دمشق", dto.Governorate);
        Assert.Equal("وزارة العدل مديرية التنفيذ", dto.DisplayName);
        Assert.Equal(entry.Id, dto.RegistryId);
    }

    [Fact]
    public async Task Add_PublicEntity_WithoutRegistry_Throws()
    {
        var request = new DocumentInterventionUpsertRequest
        {
            Nature = PartyNatureCatalog.PublicEntity,
            ReceiptDate = "3/8/2026",
            EntityName = "جهة",
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
        Assert.Contains("السجل المرجعي", ex.Message);
    }

    [Fact]
    public async Task Add_PublicEntity_InactiveRegistry_Throws()
    {
        var (_, entry) = await SeedPublicEntityAsync(active: false);
        var request = new DocumentInterventionUpsertRequest
        {
            Nature = PartyNatureCatalog.PublicEntity,
            ReceiptDate = "3/8/2026",
            EntityName = "جهة",
            RegistryId = entry.Id,
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
    }

    [Fact]
    public async Task Add_WithoutReceiptDate_Throws()
    {
        var request = NaturalRequest();
        request.ReceiptDate = " ";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
        Assert.Contains("تاريخ ورود طلب التدخل مطلوب", ex.Message);
    }

    [Theory]
    [InlineData("10/8/2026")] // بعد الورود
    [InlineData("1/8/2026")]  // في يوم الورود نفسه
    public async Task Add_AcceptanceNotBeforeReceipt_Throws(string acceptanceDate)
    {
        var request = NaturalRequest();
        request.AcceptanceDate = acceptanceDate;

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
        Assert.Contains("يجب أن يسبق تاريخ الورود", ex.Message);
    }

    [Fact]
    public async Task Add_RepresentativeWithHeirs_Throws()
    {
        var request = NaturalRequest();
        request.RepresentativeName = "خالد";
        request.RepresentativeCapacity = "ولي";
        request.Heirs.Add(new HeirDto(null, "وليد", "سامر", "الحسن", "أصالة", "عنوان", "المزة"));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
        Assert.Contains("لا يجتمع", ex.Message);
    }

    [Fact]
    public async Task Add_Representative_ClearsPartyAddress()
    {
        var request = NaturalRequest();
        request.AddressType = "موطن مختار";
        request.Address = "المزة";
        request.RepresentativeName = "خالد";
        request.RepresentativeFather = "أحمد";
        request.RepresentativeFamily = "الحسن";
        request.RepresentativeCapacity = "ولي";
        request.RepresentativeAddressType = "عنوان";
        request.RepresentativeAddress = "دمشق";

        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.Null(dto.AddressType);
        Assert.Null(dto.Address);
        Assert.Equal("دمشق", dto.RepresentativeAddress);
        Assert.Equal("ولي", dto.RepresentativeCapacity);
    }

    [Fact]
    public async Task Add_LegalWithoutEntityName_Throws()
    {
        var request = LegalRequest();
        request.EntityName = "";

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول"));
        Assert.Contains("اسم الشخص الاعتباري مطلوب", ex.Message);
    }

    [Fact]
    public async Task Add_DepositSide_Throws()
    {
        _document.GeneralEntitySide = GeneralEntitySideCatalog.Deposit;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, NaturalRequest(), _lawyer.Id, "المحامي الأول"));
        Assert.Contains("عرض وايداع", ex.Message);
    }

    [Fact]
    public async Task Update_SyncsOccurrenceAndSearchText()
    {
        var dto = await _service.AddAsync(_document.Id, NaturalRequest(), _lawyer.Id, "المحامي الأول");

        var request = NaturalRequest();
        request.Name = "سامر سعيد";
        request.AcceptanceDate = "30/7/2026";
        request.Subject = "موضوع معدّل";
        var updated = await _service.UpdateAsync(
            _document.Id, dto.Id, request, _lawyer.Id, "المحامي الأول");

        Assert.NotNull(updated);
        Assert.Equal("سامر سعيد محمود الحسن", updated!.DisplayName);
        Assert.Equal("2026-07-30", updated.AcceptanceDate);

        var occurrence = await _db.DocumentOccurrences.SingleAsync();
        Assert.Equal(new DateTime(2026, 8, 1), occurrence.EventDate!.Value.Date);
        var (details, _) = OccurrenceDetails.Split(occurrence.Details);
        Assert.Equal("سامر سعيد محمود الحسن", details!["intervenorName"]);
        Assert.Equal("2026-07-30", details["acceptanceDate"]);

        var refreshed = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _document.Id);
        Assert.Contains("سامر سعيد", refreshed.SearchText);
        Assert.DoesNotContain("سامر محمود", refreshed.SearchText);
    }

    [Fact]
    public async Task Delete_RemovesInterventionHeirsAndOccurrence()
    {
        var request = NaturalRequest();
        request.Heirs.Add(new HeirDto(null, "وليد", "سامر", "الحسن", "أصالة", "عنوان", "المزة"));
        var dto = await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");

        var deleted = await _service.DeleteAsync(_document.Id, dto.Id, "المحامي الأول");

        Assert.True(deleted);
        Assert.Empty(await _db.DocumentInterventions.ToListAsync());
        Assert.Empty(await _db.DocumentInterventionHeirs.ToListAsync());
        Assert.Empty(await _db.DocumentOccurrences.ToListAsync());

        var refreshed = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == _document.Id);
        Assert.DoesNotContain("سامر", refreshed.SearchText);
    }

    [Fact]
    public async Task UpdateOrDelete_InterventionOfAnotherDocument_ReturnsNotFound()
    {
        var dto = await _service.AddAsync(_document.Id, NaturalRequest(), _lawyer.Id, "المحامي الأول");
        var other = new Document
        {
            CreatedById = _lawyer.Id,
            Lawyer = "المحامي الأول",
            BorrowerName = "آخر",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
        };
        _db.Documents.Add(other);
        await _db.SaveChangesAsync();

        Assert.Null(await _service.UpdateAsync(
            other.Id, dto.Id, NaturalRequest(), _lawyer.Id, "المحامي الأول"));
        Assert.False(await _service.DeleteAsync(other.Id, dto.Id, "المحامي الأول"));
    }

    [Fact]
    public async Task Add_AboveDocumentLimit_Throws()
    {
        for (var i = 0; i < InterventionLimitsCatalog.MaxPerDocument; i++)
        {
            var request = NaturalRequest();
            request.Name = $"متدخل {i}";
            await _service.AddAsync(_document.Id, request, _lawyer.Id, "المحامي الأول");
        }

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.AddAsync(_document.Id, NaturalRequest(), _lawyer.Id, "المحامي الأول"));
        Assert.Contains(InterventionLimitsCatalog.MaxPerDocument.ToString(), ex.Message);
    }
}
