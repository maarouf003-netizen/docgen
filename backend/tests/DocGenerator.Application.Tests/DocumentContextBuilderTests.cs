using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class DocumentContextBuilderTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly DocumentContextBuilder _builder;

    public DocumentContextBuilderTests()
    {
        _db = TestDb.Create();
        _builder = new DocumentContextBuilder(new DocumentRepository(_db));
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> AddAsync(Document doc)
    {
        if (doc.CreatedById == 0)
        {
            if (!_db.Users.Any(u => u.Id == 1))
            {
                // علاقة المحامي المختص إلزامية: نضمن وجود مالك صالح قبل إدراج الملف.
                _db.Users.Add(new User { Username = "seeded", FullName = "مالك افتراضي", PasswordHash = "x" });
                await _db.SaveChangesAsync();
            }
            doc.CreatedById = 1;
        }
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Fact]
    public async Task BuildContext_BankingContract_SetsBaseAndContainFields()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            BorrowerAddress = "المزة",
            BorrowerAddressType = "موطن مختار",
            AmountWords = "خمسمئة ألف ليرة سورية",
            ContractType = "تعهد",
            ContractTypeSelector = "مصرفي",
            Applicant = "المدير العام",
            Guarantors = new List<Guarantor>
            {
                new()
                {
                    GuarantorNumber = 1,
                    GuarantorName = "سمير",
                    GuarantorFather = "حسن",
                    GuarantorFamily = "علي",
                    GuarantorAddress = "حلب",
                    AddressType = "موطن مختار",
                },
            },
        });

        var ctx = await _builder.BuildContextAsync(id, "001");

        Assert.Equal("دمشق", ctx["court"]);
        Assert.Equal("دائرة تنفيذ دمشق", ctx["court_with_prefix"]);
        Assert.Equal("تعهد", ctx["contract_type"]);
        Assert.Equal("المدير العام إضافة لوظيفته، تمثله إدارة قضايا الدولة", ctx["applicant"]);
        Assert.Contains("التزام الجهة المنفذ عليها بدفع مبلغ خمسمئة ألف ليرة سورية مع توابعه القانونية", (string)ctx["contain"]);
        Assert.Contains("الزامك بدفع مبلغ", (string)ctx["contain_notice"]);
        Assert.Equal("خمسمئة ألف ليرة سورية", ctx["amount_words_record"]);

        var singular = (string)ctx["execution_debtor_and_its_adress"];
        Assert.Contains("أحمد خالد الخطيب", singular);
        Assert.Contains("متخذا موطنا مختارا: المزة", singular);

        var rich = (string)ctx["execution_debtors_and_its_adresses"];
        Assert.Contains("<w:b/>", rich);
        Assert.Contains("أحمد خالد الخطيب", rich);
        Assert.Contains("سمير حسن علي", rich);
        Assert.Contains("متخذا موطنا مختارا حلب", rich);

        Assert.Equal("سمير", ctx["guarantor_1_name"]);
        Assert.Equal("موطناً مختاراً حلب", ctx["guarantor_1_address"]);

        Assert.Equal(string.Empty, ctx["guarantor_2_name"]);

        // قالب 001/002: اسم المقترض يُفصَل ضمن borrower_address الغني
        Assert.Equal(string.Empty, ctx["borrower_name"]);
        Assert.Contains("<w:b/>", (string)ctx["borrower_address"]);
    }

    [Fact]
    public async Task BuildContext_OrdinaryContract_UsesInclusionForContain()
    {
        var id = await AddAsync(new Document
        {
            Court = "حلب",
            BorrowerName = "زيد",
            ContractTypeSelector = "عادي",
            ContractType = "تنفيذ محرر",
            InclusionText = "بتنفيذ السند",
            InclusionAmountWords = "مئة ألف ليرة",
        });

        var ctx = await _builder.BuildContextAsync(id, "002");

        Assert.Equal("قرار تنفيذ محرر", ctx["contract_type"]);
        Assert.Equal("بتنفيذ السند و دفع مبلغ مئة ألف ليرة مع فوائده القانونية", ctx["contain"]);
        Assert.Equal(string.Empty, ctx["amount_words"]);
    }

    [Fact]
    public async Task BuildContext_NoticeTemplate_UsesContainNotice()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
        });

        var ctx = await _builder.BuildContextAsync(id, "003");

        Assert.Contains("الزامك بدفع مبلغ مليون ليرة", (string)ctx["contain"]);
    }

    [Fact]
    public async Task BuildContext_Seizure_SetsPerDebtorVars()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            BorrowerAddress = "المزة",
            BorrowerAddressType = "موطن مختار",
            FileNumber = "520",
            FileType = "أساس",
            SeizureDate = "15/03/2025",
            Guarantors = new List<Guarantor>
            {
                new()
                {
                    GuarantorNumber = 1,
                    GuarantorName = "سمير",
                    GuarantorFather = "حسن",
                    GuarantorFamily = "علي",
                    GuarantorAddress = "حلب",
                    AddressType = "موطن مختار",
                },
            },
        });

        var ctx = await _builder.BuildContextAsync(id, "004");

        Assert.Equal("520 أساس\nتاريخ القرار: 15/03/2025", ctx["seizure_date1"]);
        Assert.Equal("520 أساس\nتاريخ القرار: 15/03/2025", ctx["seizure_date2"]);
        Assert.Equal("دائرة تنفيذ دمشق", ctx["court_with_prefix1"]);
        Assert.Equal("دائرة تنفيذ دمشق", ctx["court_with_prefix2"]);
        Assert.Equal(string.Empty, ctx["seizure_date3"]);
        Assert.Equal(string.Empty, ctx["court_with_prefix3"]);
    }

    [Fact]
    public async Task BuildContext_NotFound_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _builder.BuildContextAsync(999, "001"));
    }

    [Fact]
    public async Task BuildContext_GuarantorNotice_WithRecipient_UsesGuarantorData()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            Guarantors = new List<Guarantor>
            {
                new()
                {
                    GuarantorNumber = 1,
                    GuarantorName = "سمير",
                    GuarantorFather = "حسن",
                    GuarantorFamily = "علي",
                    GuarantorMother = "نور",
                    GuarantorBirth = "1980-01-01",
                    GuarantorRegister = "ر1",
                    GuarantorNationalId = "و1",
                    GuarantorAddress = "حلب",
                    AddressType = "موطن مختار",
                },
            },
        });

        var ctx = await _builder.BuildContextAsync(id, "003", recipient: 1);

        Assert.Equal("سمير", ctx["borrower_name"]);
        Assert.Equal("حسن", ctx["borrower_father"]);
        Assert.Equal("علي", ctx["borrower_family"]);
        Assert.Equal("موطناً مختاراً حلب", ctx["borrower_address"]);
        Assert.Contains("سمير حسن علي", (string)ctx["execution_debtor_and_its_adress"]);
        Assert.Contains("موطناً مختاراً حلب", (string)ctx["execution_debtor_and_its_adress"]);
        Assert.Contains("الزامك بدفع مبلغ مليون ليرة", (string)ctx["contain"]);
    }

    [Fact]
    public async Task BuildContext_GuarantorNotice_InvalidRecipient_Throws()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
        });

        await Assert.ThrowsAsync<ArgumentException>(() => _builder.BuildContextAsync(id, "003", recipient: 9));
    }

    [Fact]
    public async Task BuildContext_PropertySale_WithoutEstate_Throws()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            RealEstates = new List<RealEstate>
            {
                new()
                {
                    Owner = "أحمد محمد خالد",
                    Property = "منزل",
                    PropertyNumber = "12",
                    PropertyDistrict = "المزة",
                    LandRegistry = "سجل 3",
                    ShareType = "كامل",
                },
            },
        });

        await Assert.ThrowsAsync<ArgumentException>(() => _builder.BuildContextAsync(id, "005"));
    }

    [Fact]
    public async Task BuildContext_PropertySale_WithEstates_SetsPropertyAndOwner()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            RealEstates = new List<RealEstate>
            {
                new()
                {
                    Owner = "أحمد محمد خالد",
                    Property = "منزل",
                    PropertyNumber = "12",
                    PropertyDistrict = "المزة",
                    LandRegistry = "سجل 3",
                    ShareType = "كامل",
                },
            },
        });

        var estateId = _db.RealEstates.Single().Id;
        var ctx = await _builder.BuildContextAsync(id, "005", estateIds: new[] { estateId });

        Assert.Equal("تمام عقارك رقم 12 من المنطقة العقارية المزة", ctx["property"]);
        Assert.Equal("أحمد محمد خالد", ctx["property_owner"]);
        Assert.Equal("أحمد", ctx["borrower_name"]);
        Assert.Equal("محمد", ctx["borrower_father"]);
        Assert.Equal("خالد", ctx["borrower_family"]);
        Assert.Equal("مليون ليرة مع توابعه القانونية", ctx["amount_words"]);
    }

    [Fact]
    public async Task BuildContext_PropertySalePaper_WithEstates_SetsDebtor()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "خالد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            RealEstates = new List<RealEstate>
            {
                new()
                {
                    Owner = "أحمد محمد خالد",
                    Property = "منزل",
                    PropertyNumber = "12",
                    PropertyDistrict = "المزة",
                    LandRegistry = "سجل 3",
                    ShareType = "كامل",
                },
            },
        });

        var estateId = _db.RealEstates.Single().Id;
        var ctx = await _builder.BuildContextAsync(id, "006", estateIds: new[] { estateId });

        Assert.Equal("أحمد محمد خالد", ctx["execution_debtor"]);
        Assert.Equal("أحمد محمد خالد", ctx["property_owner"]);
        Assert.Equal("تمام عقارك رقم 12 من المنطقة العقارية المزة", ctx["property"]);
        Assert.Equal("مليون ليرة مع توابعه القانونية", ctx["amount_words"]);
    }

    [Fact]
    public async Task BuildContext_NoticePaper_RecipientZero_UsesBorrower()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "خالد",
            BorrowerMother = "فاطمة",
            BorrowerAddress = "المزة",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
        });

        var ctx = await _builder.BuildContextAsync(id, "007");

        Assert.Equal("أحمد محمد خالد", ctx["recipient_name"]);
        Assert.Equal("المقترض", ctx["recipient_role"]);
        Assert.Equal("أحمد محمد خالد", ctx["execution_debtor"]);
        Assert.Equal("أحمد", ctx["borrower_name"]);
        Assert.Contains("الزامك بدفع مبلغ مليون ليرة", (string)ctx["contain"]);
    }

    [Fact]
    public async Task BuildContext_NoticePaper_GuarantorRecipient_UsesGuarantor()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            Guarantors = new List<Guarantor>
            {
                new()
                {
                    GuarantorNumber = 1,
                    GuarantorName = "سمير",
                    GuarantorFather = "حسن",
                    GuarantorFamily = "علي",
                    GuarantorAddress = "حلب",
                    AddressType = "موطن مختار",
                },
            },
        });

        var ctx = await _builder.BuildContextAsync(id, "007", recipient: 1);

        Assert.Equal("سمير حسن علي", ctx["recipient_name"]);
        Assert.Equal("الكفيل 1", ctx["recipient_role"]);
        Assert.Equal("سمير حسن علي", ctx["execution_debtor"]);
    }

    [Fact]
    public async Task BuildContext_PropertySeizure_SetsFieldsAndStripsAmountPrefix()
    {
        var id = await AddAsync(new Document
        {
            Court = "دمشق",
            BorrowerName = "أحمد",
            BorrowerFather = "محمد",
            BorrowerFamily = "خالد",
            AmountWords = "مليون ليرة",
            ContractTypeSelector = "مصرفي",
            ContractType = "تعهد",
            RealEstates = new List<RealEstate>
            {
                new()
                {
                    Owner = "أحمد محمد خالد",
                    Property = "منزل",
                    PropertyNumber = "12",
                    PropertyDistrict = "المزة",
                    LandRegistry = "سجل 3",
                    ShareType = "كامل",
                },
            },
        });

        var estateId = _db.RealEstates.Single().Id;
        var ctx = await _builder.BuildContextAsync(id, "PS", estateIds: new[] { estateId });

        Assert.Equal("سجل 3", ctx["Land_Registry"]);
        Assert.Equal("أحمد محمد خالد", ctx["execution_debtor"]);
        Assert.Equal("12", ctx["property_number"]);
        Assert.Equal("المزة", ctx["property_district"]);
        Assert.Equal("مليون ليرة", ctx["amount_words"]);
    }
}
