using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سقف أطراف الملف (قرار المالك): إجمالي المنفذ عليهم 50 شاملًا المقترض
/// (مصرفي/عادي بنفس السقف)، أي حتى 49 صف كفلاء — `PartyLimitsCatalog` هو المصدر الوحيد.
/// </summary>
public class PartyLimitsTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public PartyLimitsTests()
    {
        _db = TestDb.Create();
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
        _service = DocumentServiceFactory.Create(_db, _audit).Documents;
    }

    public void Dispose() => _db.Dispose();

    private static GuarantorDto NewGuarantor(int number) => new(
        Id: null,
        GuarantorNumber: number,
        Name: $"كفيل {number}",
        Father: "أب",
        Family: "عائلة",
        Mother: null,
        Birth: null,
        Register: null,
        NationalId: null,
        Address: "حلب",
        AddressType: "موطن مختار",
        RepresentativeName: null,
        RepresentativeFather: null,
        RepresentativeFamily: null,
        RepresentativeCapacity: null,
        RepresentativeAddressType: null,
        RepresentativeAddress: null);

    private static DocumentUpsertRequest Sample(int guarantorCount) => new()
    {
        BorrowerName = "أحمد",
        BorrowerFather = "خالد",
        BorrowerFamily = "الخطيب",
        AmountNumeric = 1000,
        Currency = "ليرة سورية",
        FileNumber = "520",
        FileYear = "2024",
        FileRegistrationDate = "1/1/2024",
        Guarantors = Enumerable.Range(1, guarantorCount).Select(NewGuarantor).ToList(),
    };

    [Fact]
    public async Task Create_WithMaxGuarantors_SucceedsAndRoundTrips()
    {
        var doc = await _service.CreateAsync(Sample(PartyLimitsCatalog.MaxGuarantors), 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(PartyLimitsCatalog.MaxGuarantors, loaded!.Guarantors.Count);
        Assert.Equal("كفيل 49", loaded.Guarantors.OrderBy(g => g.GuarantorNumber).Last().Name);
    }

    [Fact]
    public async Task Create_AboveMaxGuarantors_ThrowsWithLimitMessage()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(Sample(PartyLimitsCatalog.MaxGuarantors + 1), 1, "lawyer1", 1));
        Assert.Contains(PartyLimitsCatalog.MaxTotalDebtors.ToString(), ex.Message);
    }

    [Fact]
    public async Task Update_AboveMaxGuarantors_ThrowsWithLimitMessage()
    {
        var doc = await _service.CreateAsync(Sample(1), 1, "lawyer1", 1);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(doc.Id, Sample(PartyLimitsCatalog.MaxGuarantors + 1), "lawyer1", 1));
        Assert.Contains(PartyLimitsCatalog.MaxTotalDebtors.ToString(), ex.Message);
    }

    [Fact]
    public async Task Create_DuplicateGuarantorNumber_Throws()
    {
        var req = Sample(2);
        req.Guarantors[1] = req.Guarantors[0] with { Name = "مكرر" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(req, 1, "lawyer1", 1));
        Assert.Contains("مكرر", ex.Message);
    }

    [Fact]
    public async Task Create_FullPartiesWithHeirsAndRepresentatives_RoundTripsExactly()
    {
        // أمانة الحفظ على نطاق 50: المقترض + الكفيل الأخير (49) بورثة وممثل شرعي
        // وعناوين — تُحفظ وتُسترجع حرفيًا بلا إسقاط صامت ولا خلط ربط (GuarantorNumber).
        var req = Sample(PartyLimitsCatalog.MaxGuarantors);
        req.BorrowerAddress = "المزة";
        req.BorrowerRepresentativeName = "ولي المقترض";
        req.BorrowerRepresentativeFather = "أب الولي";
        req.BorrowerRepresentativeFamily = "عائلة الولي";
        req.BorrowerRepresentativeCapacity = "ولي";
        req.BorrowerRepresentativeAddressType = "عنوان";
        req.BorrowerRepresentativeAddress = "دمشق";
        req.BorrowerHeirs = new List<HeirDto>
        {
            new(null, "وريث المقترض", "أب الوريث", "عائلة الوريث", "أصالة", "عنوان", "حلب"),
        };
        req.Guarantors[^1] = req.Guarantors[^1] with
        {
            Address = "حمص",
            RepresentativeName = "وصي الكفيل",
            RepresentativeFather = "أب الوصي",
            RepresentativeFamily = "عائلة الوصي",
            RepresentativeCapacity = "وصي",
            RepresentativeAddressType = "عنوان",
            RepresentativeAddress = "اللاذقية",
            Heirs = new List<HeirDto>
            {
                new(null, "وريث الكفيل", "أب وريث الكفيل", "عائلة وريث الكفيل", "إضافة لتركة", "عنوان", "طرطوس"),
            },
        };

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var loaded = await _service.GetAsync(doc.Id);

        Assert.Equal(PartyLimitsCatalog.MaxGuarantors, loaded!.Guarantors.Count);
        Assert.Equal("ولي المقترض", loaded.BorrowerRepresentativeName);
        Assert.Equal("ولي", loaded.BorrowerRepresentativeCapacity);
        Assert.Equal("دمشق", loaded.BorrowerRepresentativeAddress);
        var borrowerHeir = Assert.Single(loaded.BorrowerHeirs);
        Assert.Equal("وريث المقترض", borrowerHeir.Name);
        Assert.Equal("حلب", borrowerHeir.Address);

        var last = loaded.Guarantors.OrderBy(g => g.GuarantorNumber).Last();
        Assert.Equal(PartyLimitsCatalog.MaxGuarantors, last.GuarantorNumber);
        Assert.Equal("حمص", last.Address);
        Assert.Equal("وصي الكفيل", last.RepresentativeName);
        Assert.Equal("وصي", last.RepresentativeCapacity);
        Assert.Equal("اللاذقية", last.RepresentativeAddress);
        var guarantorHeir = Assert.Single(last.Heirs ?? []);
        Assert.Equal("وريث الكفيل", guarantorHeir.Name);
        Assert.Equal("إضافة لتركة", guarantorHeir.Capacity);
        Assert.Equal("طرطوس", guarantorHeir.Address);

        // ربط الورثة بالأرقام لا يختلط: وريث المقترض (null) منفصل عن وريث الكفيل 49.
        Assert.All(loaded.Guarantors.Take(PartyLimitsCatalog.MaxGuarantors - 1),
            g => Assert.Empty(g.Heirs ?? []));
    }
}
