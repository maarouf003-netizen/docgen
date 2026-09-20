using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

public class DocumentDelegationServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentDelegationService _service;
    private readonly IDocumentService _documentService;
    private readonly FakeAuditLogger _audit = new();

    private readonly Branch _branch;
    private readonly Branch _otherBranch;
    private readonly User _lawyer1;
    private readonly User _lawyer2;
    private readonly User _head1;
    private readonly User _head2;
    private readonly User _externalLawyer;

    public DocumentDelegationServiceTests()
    {
        _db = TestDb.Create();

        _branch = new Branch { Name = "دمشق", Code = "DAM" };
        _otherBranch = new Branch { Name = "اللاذقية", Code = "LAT" };
        _db.Branches.AddRange(_branch, _otherBranch);
        _db.SaveChanges();

        _lawyer1 = User(_branch.Id, "lawyer1", "محامي دمشق");
        _lawyer2 = User(_branch.Id, "lawyer2", "محامي دمشق ثانٍ");
        _head1 = User(_branch.Id, "head1", "رئيس قسم دمشق", UserRole.Head);
        _head2 = User(_otherBranch.Id, "head2", "رئيس قسم اللاذقية", UserRole.Head);
        _externalLawyer = User(_otherBranch.Id, "lawyer_lat", "محامي اللاذقية");
        _db.Users.AddRange(_lawyer1, _lawyer2, _head1, _head2, _externalLawyer);
        _db.SaveChanges();

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var branches = new Repository<Branch>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var occurrences = new Repository<DocumentOccurrence>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        var headAlerts = new HeadAlertService(
            new HeadAlertRepository(_db),
            documents,
            users,
            branches,
            uow,
            tx,
            _audit);

        _service = new DocumentDelegationService(
            new DelegationRepository(_db),
            documents,
            users,
            branches,
            registrationDates,
            occurrences,
            uow,
            tx,
            _audit,
            headAlerts,
            TimeProvider.System,
            TestClock.TimeZone);

        _documentService = new DocumentService(
            documents,
            users,
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db),
            registrationDates,
            occurrences,
            new DelegationRepository(_db),
            new AppealRepository(_db),
            headAlerts,
            uow,
            tx,
            _audit,
            Options.Create(new ExportOptions()),
            TimeProvider.System,
            TestClock.TimeZone);
    }

    public void Dispose() => _db.Dispose();

    private static User User(int? branchId, string username, string fullName, UserRole role = UserRole.Lawyer) => new()
    {
        Username = username,
        FullName = fullName,
        Role = role,
        BranchId = branchId,
        PasswordHash = new Services.PasswordHasher().Hash("123456"),
    };

    /// <summary>ملف «طالبة تنفيذ» مقيد بأصل عقار واحد، في ملكية lawyer1.</summary>
    private async Task<Document> CreateSourceAsync()
    {
        var doc = new Document
        {
            CreatedById = _lawyer1.Id,
            BranchId = _branch.Id,
            BranchName = _branch.Name,
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            AmountNumeric = 1_000_000,
            Currency = "ليرة سورية",
            ContractType = "عقد قرض",
            ContractNumber = "12/2024",
            Court = "دمشق",
            Applicant = "المدعي",
            FileNumber = "520",
            FileYear = "2024",
            DocumentType = "متداول - أحمد خالد الخطيب",
            SearchText = "أحمد الخطيب المدعي 520",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _db.Assets.Add(new Asset
        {
            DocumentId = doc.Id,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "77",
            PropertyDistrict = "المزة",
        });
        await _db.SaveChangesAsync();
        return doc;
    }

    private static UpsertDelegationRequest SampleRequest(params int[] assetIds) => new(
        DelegatedCourt: "دائرة تنفيذ حلب",
        IsExternal: false,
        ExternalBranchId: null,
        DelegationDate: "1/8/2026",
        DelegationText: "الإنابة على العقار المذكور",
        DepositBookNumber: "كتاب-1",
        DepositBookDate: "2/8/2026",
        AssetIds: assetIds.ToList());

    /// <summary>طلب تعديل يعيد بناء جميع حقول المستند بدقة من الكيان — للمرآة/الحارس بلا تشويه.</summary>
    private static DocumentUpsertRequest MirrorRequest(Document doc) => new()
    {
        GeneralEntitySide = doc.GeneralEntitySide,
        DocumentType = doc.DocumentType,
        BorrowerName = doc.BorrowerName,
        BorrowerFather = doc.BorrowerFather,
        BorrowerFamily = doc.BorrowerFamily,
        BorrowerMother = doc.BorrowerMother,
        BorrowerBirth = doc.BorrowerBirth,
        BorrowerRegister = doc.BorrowerRegister,
        BorrowerNationalId = doc.BorrowerNationalId,
        BorrowerAddress = doc.BorrowerAddress,
        BorrowerAddressType = doc.BorrowerAddressType,
        BorrowerNature = doc.BorrowerNature,
        BorrowerRegistrationNumber = doc.BorrowerRegistrationNumber,
        BorrowerRepresentedBy = doc.BorrowerRepresentedBy,
        BorrowerRepresentativeName = doc.BorrowerRepresentativeName,
        BorrowerRepresentativeFather = doc.BorrowerRepresentativeFather,
        BorrowerRepresentativeFamily = doc.BorrowerRepresentativeFamily,
        BorrowerRepresentativeCapacity = doc.BorrowerRepresentativeCapacity,
        BorrowerRepresentativeAddressType = doc.BorrowerRepresentativeAddressType,
        BorrowerRepresentativeAddress = doc.BorrowerRepresentativeAddress,
        ContractType = doc.ContractType,
        ContractTypeSelector = doc.ContractTypeSelector,
        ContractNumber = doc.ContractNumber,
        ContractDate = doc.ContractDate,
        AnnexType = doc.AnnexType,
        AnnexNumber = doc.AnnexNumber,
        AnnexDate = doc.AnnexDate,
        InclusionText = doc.InclusionText,
        AmountNumeric = doc.AmountNumeric,
        AmountWords = doc.AmountWords,
        Currency = doc.Currency,
        Amount2Numeric = doc.Amount2Numeric,
        Amount2Words = doc.Amount2Words,
        Currency2 = doc.Currency2,
        Amount3Numeric = doc.Amount3Numeric,
        Amount3Words = doc.Amount3Words,
        Currency3 = doc.Currency3,
        InclusionAmountNumeric = doc.InclusionAmountNumeric,
        InclusionAmountWords = doc.InclusionAmountWords,
        InclusionCurrency = doc.InclusionCurrency,
        InclusionAmount2Numeric = doc.InclusionAmount2Numeric,
        InclusionAmount2Words = doc.InclusionAmount2Words,
        InclusionCurrency2 = doc.InclusionCurrency2,
        InclusionAmount3Numeric = doc.InclusionAmount3Numeric,
        InclusionAmount3Words = doc.InclusionAmount3Words,
        InclusionCurrency3 = doc.InclusionCurrency3,
        Court = doc.Court,
        Applicant = doc.Applicant,
        FileNumber = doc.FileNumber,
        FileType = doc.FileType,
        FileYear = doc.FileYear,
        FileRegistrationDate = doc.RegistrationDate?.Date ?? "1/1/2024",
        FileIncoming = doc.FileIncoming,
        FileIncomingDate = doc.FileIncomingDate,
        UnderFilingNumber = doc.UnderFilingNumber,
        BranchName = doc.BranchName,
        SeizureDate = doc.SeizureDate,
        ImmediateActions = doc.ImmediateActions,
        Notes = doc.Notes,
        FileArrivalNumber = doc.FileArrivalNumber,
        FileArrivalDate = doc.FileArrivalDate,
        FileReceiptNumber = doc.FileReceiptNumber,
        FileReceiptDate = doc.FileReceiptDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        ApplicantPublicEntities = new List<ApplicantPublicEntityDto>(),
        ExecutionApplicants = new List<ExecutionApplicantDto>(),
        ExecutedPublicEntities = new List<ExecutedPublicEntityDto>(),
        ExecutedNaturalPersons = new List<ExecutedNaturalPersonDto>(),
        Guarantors = new List<GuarantorDto>(),
        BorrowerHeirs = new List<HeirDto>(),
        Assets = new List<AssetDto>(),
    };

    /// <summary>صفّ كفلاء المستند كامِلًا (مع ورثتهم) كـ DTO — للطلبات التي تلامس الكفلاء.</summary>
    private static List<GuarantorDto> MirrorGuarantors(Document doc) =>
        doc.Guarantors.OrderBy(g => g.GuarantorNumber)
            .Select(g => new GuarantorDto(
                g.Id, g.GuarantorNumber, g.GuarantorName, g.GuarantorFather, g.GuarantorFamily,
                g.GuarantorMother, g.GuarantorBirth, g.GuarantorRegister, g.GuarantorNationalId,
                g.GuarantorAddress, g.AddressType,
                g.RepresentativeName, g.RepresentativeFather, g.RepresentativeFamily,
                g.RepresentativeCapacity, g.RepresentativeAddressType, g.RepresentativeAddress,
                doc.Heirs.Where(h => h.GuarantorNumber == g.GuarantorNumber)
                    .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily,
                        h.HeirCapacity, h.AddressType, h.HeirAddress)).ToList(),
                g.GuarantorNature, g.GuarantorRegistrationNumber, g.GuarantorRepresentedBy))
            .ToList();

    /// <summary>ورثة المقترض (بلا رقم كفيل) كـ DTO.</summary>
    private static List<HeirDto> MirrorBorrowerHeirs(Document doc) =>
        doc.Heirs.Where(h => h.GuarantorNumber is null)
            .Select(h => new HeirDto(h.Id, h.HeirName, h.HeirFather, h.HeirFamily,
                h.HeirCapacity, h.AddressType, h.HeirAddress)).ToList();

    /// <summary>طلب تعديل يعيد بناء كل حقول المستند بما فيها الأطراف (الكفلاء وورثتهم وورثة المقترض).</summary>
    private static DocumentUpsertRequest MirrorRequestWithParties(Document doc)
    {
        var request = MirrorRequest(doc);
        request.Guarantors = MirrorGuarantors(doc);
        request.BorrowerHeirs = MirrorBorrowerHeirs(doc);
        return request;
    }

    [Fact]
    public async Task Create_SourceFile_ReturnsPendingDelegationWithAssetSnapshot()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var dto = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.True(dto.Id > 0);
        Assert.Equal(DelegationStatusCatalog.PendingHead, dto.Status);
        Assert.Equal("دائرة تنفيذ حلب", dto.DelegatedCourt);
        Assert.Equal("2026-08-01", dto.DelegationDate);
        Assert.Equal("2026-08-02", dto.DepositBookDate);
        var asset = Assert.Single(dto.Assets);
        Assert.Equal(AssetKindCatalog.RealEstate, asset.AssetKind);
        Assert.Equal("عقار رقم 77", asset.AssetLabel);
        Assert.Equal("أحمد خالد الخطيب", dto.SourceDocumentLabel);
        Assert.Equal("520", dto.SourceFileNumber);
        Assert.Equal("2024", dto.SourceFileYear);
    }

    [Fact]
    public async Task Create_SourceWithCurrentYearBaseNumber_ReturnsCurrentBaseAsSourceFileNumber()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = source.Id,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();

        var dto = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.Equal("1500", dto.SourceFileNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), dto.SourceFileYear);
    }

    [Fact]
    public async Task Create_NotOwner_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer2.Id, "lawyer2"));
        Assert.Contains("لا تملكه", ex.Message);
    }

    [Fact]
    public async Task Create_ExecutedSideFile_Throws()
    {
        var source = await CreateSourceAsync();
        source.GeneralEntitySide = GeneralEntitySideCatalog.Executed;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1"));
        Assert.Contains("طالبة التنفيذ", ex.Message);
    }

    [Fact]
    public async Task Create_ExecutedFile_Throws()
    {
        var source = await CreateSourceAsync();
        source.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
        source.ExecSubStatus = ExecutionStatusCatalog.SubFullyExecuted;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1"));
        Assert.Contains("منفَّذ", ex.Message);
    }

    [Fact]
    public async Task Create_MissingCourtOrDate_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var noCourt = SampleRequest(assetId) with { DelegatedCourt = null };
        var ex1 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, noCourt, _lawyer1.Id, "lawyer1"));
        Assert.Contains("الدائرة المنابة", ex1.Message);

        var noDate = SampleRequest(assetId) with { DelegationDate = null };
        var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, noDate, _lawyer1.Id, "lawyer1"));
        Assert.Contains("تاريخ الإنابة", ex2.Message);
    }

    [Fact]
    public async Task Create_NoAssetsOrForeignAsset_Throws()
    {
        var source = await CreateSourceAsync();

        var ex1 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(), _lawyer1.Id, "lawyer1"));
        Assert.Contains("الأموال موضوع الإنابة", ex1.Message);

        var foreign = new Asset { DocumentId = source.Id, AssetKind = AssetKindCatalog.Vehicle, PlateNumber = "1" };
        _db.Assets.Add(foreign);
        await _db.SaveChangesAsync();

        var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(foreign.Id + 999), _lawyer1.Id, "lawyer1"));
        Assert.Contains("لا يتبع", ex2.Message);
    }

    [Fact]
    public async Task Update_Pending_ByOwner_ChangesFields()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var updated = await _service.UpdateAsync(created.Id,
            SampleRequest(assetId) with { DelegatedCourt = "دائرة تنفيذ حماة", DelegationText = "عدّلت المنطوق" },
            _lawyer1.Id, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("دائرة تنفيذ حماة", updated!.DelegatedCourt);
        Assert.Equal("عدّلت المنطوق", updated.DelegationText);
    }

    [Fact]
    public async Task Update_Pending_ByOwner_SwitchAsset_Succeeds()
    {
        // سيناريو المستخدم حرفيًا: تسطير إنابة على عقار ثم تصحيح فوري بتبديله
        // بعقار آخر من الملف المنيب نفسه.
        var source = await CreateSourceAsync();
        _db.Assets.Add(new Asset
        {
            DocumentId = source.Id,
            AssetKind = AssetKindCatalog.RealEstate,
            PropertyNumber = "78",
            PropertyDistrict = "المزة",
        });
        await _db.SaveChangesAsync();
        var firstId = await _db.Assets
            .Where(a => a.DocumentId == source.Id && a.PropertyNumber == "77")
            .Select(a => a.Id).SingleAsync();
        var secondId = await _db.Assets
            .Where(a => a.DocumentId == source.Id && a.PropertyNumber == "78")
            .Select(a => a.Id).SingleAsync();

        var created = await _service.CreateAsync(source.Id, SampleRequest(firstId), _lawyer1.Id, "lawyer1");

        // محاكاة طلب HTTP جديد: تفريغ متعقب الكيانات فيُعاد التحميل طازجًا من القاعدة
        // (في الإنتاج سياق جديد لكل طلب — السياق المشترك وحده يخفي غياب الـ Include
        // عبر خريطة الهوية، فيبقى التعديل أخضر في الاختبار ومكسورًا في الإنتاج).
        _db.ChangeTracker.Clear();

        var updated = await _service.UpdateAsync(created.Id,
            SampleRequest(secondId) with { DelegationText = "تصحيح العقار" },
            _lawyer1.Id, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("تصحيح العقار", updated!.DelegationText);
        var asset = Assert.Single(updated.Assets);
        Assert.Equal(AssetKindCatalog.RealEstate, asset.AssetKind);
        Assert.Equal("عقار رقم 78", asset.AssetLabel);
    }

    [Fact]
    public async Task Update_NotOwner_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(created.Id, SampleRequest(assetId), _lawyer2.Id, "lawyer2"));
        Assert.Contains("لا تملكه", ex.Message);
    }

    [Fact]
    public async Task Delete_Pending_ByOwner_Removes()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.True(await _service.DeleteAsync(created.Id, _lawyer1.Id, "lawyer1"));
        Assert.Null(await _db.DocumentDelegations.FindAsync(created.Id));
    }

    [Fact]
    public async Task Assign_BySourceHead_CreatesTargetFile()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.Assigned, dto!.Status);
        Assert.Equal(_lawyer2.Id, dto.AssignedLawyerId);
        Assert.True(dto.TargetDocumentId.HasValue);

        var target = await _db.Documents.Include(d => d.SourceDelegation).FirstAsync(d => d.Id == dto.TargetDocumentId!.Value);
        Assert.Equal(source.Id, target.SourceDelegation!.SourceDocumentId);
        Assert.Equal(_lawyer2.Id, target.CreatedById);
        Assert.Equal(_branch.Id, target.BranchId);
        Assert.Equal("انابة", target.FileType);
        Assert.True(target.IsDraft);
        // لقطة مجمدة من الأطراف والسند
        Assert.Equal(source.BorrowerName, target.BorrowerName);
        Assert.Equal(source.BorrowerFamily, target.BorrowerFamily);
        Assert.Equal(source.AmountNumeric, target.AmountNumeric);
        // هوية مستقلة: دائرة المناب هي المنابة المسجَّل فيها لا دائرة المنيب.
        Assert.Equal("دائرة تنفيذ حلب", target.Court);
        Assert.NotEqual(source.Court, target.Court);
        Assert.Equal("دائرة تنفيذ حلب", dto!.DelegatedCourt);
    }

    [Fact]
    public async Task TargetCourt_IsIndependent_EditableAndNotMirrored()
    {
        // الدائرة حقيقة خاصة بالمناب: قابلة للتعديل كحقل أصيل، وتعديل المنيب لا يسري عليها،
        // وبطاقة الإنابة تعرض الحيّة منها.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        // 1) تعديل دائرة المناب مقبول (لا حارس «الدائرة» بعد اليوم).
        var edit = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        edit.Court = "دائرة تنفيذ حماة";
        await _documentService.UpdateAsync(targetId, edit, _lawyer2.FullName, _lawyer2.Id);
        Assert.Equal("دائرة تنفيذ حماة",
            (await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId)).Court);

        // 2) تعديل دائرة المنيب لا يُزامَن إلى المناب.
        var sourceEdit = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == source.Id));
        sourceEdit.Court = "دائرة تنفيذ دمشق الجديدة";
        await _documentService.UpdateAsync(source.Id, sourceEdit, _lawyer1.FullName, _lawyer1.Id);
        Assert.Equal("دائرة تنفيذ حماة",
            (await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId)).Court);

        // 3) بطاقة الإنابة (جهة المناب) تعرض الدائرة الحيّة.
        var card = await _service.ListForDocumentAsync(targetId);
        Assert.Equal("دائرة تنفيذ حماة", card.Single(d => d.Id == created.Id).DelegatedCourt);
    }

    [Fact]
    public async Task Assign_SetsTargetLawyerName_ForListColumnAndFilter()
    {
        // انحدار: اسم المحامي الموكول بالملف المناب هو مصدر عمود «المحامي المختص»
        // في «الملفات التنفيذية» وفلتر المحامي وقائمته — كان يُترك فارغًا فيُعرض «—».
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var targetId = dto!.TargetDocumentId!.Value;
        _db.ChangeTracker.Clear();
        var target = await _db.Documents.FirstAsync(d => d.Id == targetId);
        Assert.Equal(_lawyer2.FullName, target.Lawyer);

        // مستوى العقد: القائمة تعرض الاسم كما سيراه رئيس القسم/المدير/المشرف.
        var page = await _documentService.SearchAsync(null, null, null, null, null, null, null, null, null,
            1, 20, _branch.Id);
        Assert.Equal(_lawyer2.FullName, Assert.Single(page.Items, d => d.Id == targetId).Lawyer);

        // فلتر المحامي يلتقط الملف المناب باسمه.
        var filtered = await _documentService.SearchAsync(null, null, null, null, _lawyer2.FullName, null, null, null, null,
            1, 20, _branch.Id);
        Assert.Contains(filtered.Items, d => d.Id == targetId);
    }

    [Fact]
    public async Task Assign_WithBlankLawyerFullName_FallsBackToUsername()
    {
        // حدّية: محامٍ بلا اسم كامل يُسجَّل المناب باسم دخوله — بنفس صيغة الإنشاء العادي.
        var nameless = User(_branch.Id, "lawyer_noname", "   ");
        _db.Users.Add(nameless);
        await _db.SaveChangesAsync();

        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(nameless.Id),
            _head1.Id, _branch.Id, "head1");

        _db.ChangeTracker.Clear();
        var target = await _db.Documents.FirstAsync(d => d.Id == dto!.TargetDocumentId!.Value);
        Assert.Equal("lawyer_noname", target.Lawyer);
    }

    [Fact]
    public async Task ListForSource_AfterTargetRotation_ReturnsEffectiveTargetFileNumber()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var targetId = dto!.TargetDocumentId!.Value;
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = targetId,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = _lawyer2.Id,
        });
        await _db.SaveChangesAsync();

        var listed = await _service.ListForDocumentAsync(source.Id);

        var assigned = Assert.Single(listed);
        Assert.Equal("1500", assigned.TargetFileNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), assigned.TargetFileYear);
    }

    [Fact]
    public async Task ListForSource_WithoutTargetRotation_FallsBackToTargetOriginalFileNumber()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var target = await _db.Documents.FirstAsync(d => d.Id == dto!.TargetDocumentId!.Value);
        target.FileNumber = "300";
        target.FileYear = "2025";
        await _db.SaveChangesAsync();

        var listed = await _service.ListForDocumentAsync(source.Id);

        var assigned = Assert.Single(listed);
        Assert.Equal("300", assigned.TargetFileNumber);
        Assert.Equal("2025", assigned.TargetFileYear);
    }

    [Fact]
    public async Task ListForTarget_AfterTargetRotation_ReturnsEffectiveTargetFileNumber()
    {
        // مسار المناب (FindByTargetAsync): رقم المناب الفعّال يُعرض في بطاقته —
        // يتطلب جلب TargetDocument.BaseNumbers وإلا سقط العرض إلى الرقم الأصلي.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var targetId = dto!.TargetDocumentId!.Value;
        var rotatedYear = DateTime.Today.Year - 1;
        var target = await _db.Documents.FirstAsync(d => d.Id == targetId);
        target.FileNumber = "300";
        target.FileYear = "2025";
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = targetId,
            Year = rotatedYear,
            BaseNumber = "1500",
            CreatedById = _lawyer2.Id,
        });
        await _db.SaveChangesAsync();

        var listed = await _service.ListForDocumentAsync(targetId);

        var assigned = Assert.Single(listed);
        Assert.Equal("1500", assigned.TargetFileNumber);
        Assert.Equal(rotatedYear.ToString(), assigned.TargetFileYear);
    }

    [Fact]
    public async Task Assign_CopiesAllSourcePartiesAndBooksToTarget()
    {
        var source = await CreateSourceAsync();
        source.FileArrivalNumber = "ورود-7";
        source.FileArrivalDate = "5/8/2026";
        source.FileIncoming = "كتاب-الجهة-44";
        source.FileIncomingDate = "6/8/2026";
        source.UnderFilingNumber = "تحت-3";
        _db.ApplicantPublicEntities.Add(new ApplicantPublicEntity
        {
            DocumentId = source.Id,
            Name = "المصرف العقاري",
            Branch = "فرع دمشق",
            Governorate = "دمشق",
        });
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id,
            GuarantorNumber = 2,
            GuarantorName = "محمود",
            GuarantorFather = "سامي",
            GuarantorFamily = "الحلبي",
            GuarantorNature = PartyNatureCatalog.Natural,
        });
        _db.Heirs.Add(new Heir
        {
            DocumentId = source.Id,
            GuarantorNumber = 2,
            HeirName = "حسن",
            HeirFather = "محمود",
            HeirFamily = "الحلبي",
        });
        await _db.SaveChangesAsync();

        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var target = await _db.Documents
            .Include(d => d.ApplicantPublicEntities)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .FirstAsync(d => d.Id == dto!.TargetDocumentId!.Value);

        // أطراف الملف المنيب كلها تنتقل، فلا يظهر صف «الجهات العامة» أو «الكفلاء» فارغًا على الملف المناب.
        var entity = Assert.Single(target.ApplicantPublicEntities);
        Assert.Equal("المصرف العقاري", entity.Name);
        Assert.Equal("فرع دمشق", entity.Branch);
        var guarantor = Assert.Single(target.Guarantors);
        Assert.Equal("محمود", guarantor.GuarantorName);
        Assert.Equal("الحلبي", guarantor.GuarantorFamily);
        var heir = Assert.Single(target.Heirs);
        Assert.Equal("حسن", heir.HeirName);

        // كتب الملف المنيب الخمسة تنتقل (ورود الملف/كتاب الجهة/تحت رفع).
        // (تاريخ إلقاء الحجز — السادس في عقد المرآة — مغطّى باختبار التكافؤ.)
        Assert.Equal("ورود-7", target.FileArrivalNumber);
        Assert.Equal("5/8/2026", target.FileArrivalDate);
        Assert.Equal("كتاب-الجهة-44", target.FileIncoming);
        Assert.Equal("6/8/2026", target.FileIncomingDate);
        Assert.Equal("تحت-3", target.UnderFilingNumber);
    }

    [Fact]
    public async Task Assign_DoesNotCopyFileReceiptFieldsToTarget()
    {
        // B6: حقلا «ورود الإخطار التنفيذي» (FileReceiptNumber/FileReceiptDate) خارج عقد
        // المرآة. الزرع أدناه حالة متسخة مستحيلة إنتاجيًا (ApplyRequest يصفّرهما على
        // طالبة تنفيذ) والغرض تثبيت العقد على مستوى الخدمة — CopyBooks هو الكاتب
        // الوحيد المحتمل على الهدف (يُبنى بـ new Document بقيم null افتراضيًا)، فأي
        // فشل في هذا الاختبار يشير إليه بدقة.
        var source = await CreateSourceAsync();
        source.FileReceiptNumber = "قيد-قديم";
        source.FileReceiptDate = new DateTime(2026, 8, 7);
        await _db.SaveChangesAsync();

        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var target = await _db.Documents.AsNoTracking()
            .FirstAsync(d => d.Id == dto!.TargetDocumentId!.Value);
        Assert.Null(target.FileReceiptNumber);
        Assert.Null(target.FileReceiptDate);
    }

    [Fact]
    public async Task Update_SourceAfterAssignment_MirrorsChangesToPendingTarget()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = dto!.TargetDocumentId!.Value;

        // الملف المناب «مرآة» لا لقطة مجمدة: تعديل المنيب عبر مسار UpdateAsync الفعلي
        // يُحدَّث المناب المعلّق أصولًا (الاختبار القديم عدّل الكيان مباشرةً فتجاوز المرآة).
        var sourceRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == source.Id));
        sourceRequest.BorrowerName = "غيّر-بعد-الاعتماد";
        sourceRequest.AmountNumeric = 2_000_000;

        await _documentService.UpdateAsync(source.Id, sourceRequest, _lawyer1.FullName, _lawyer1.Id);

        var target = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal("غيّر-بعد-الاعتماد", target.BorrowerName);
        Assert.Equal(2_000_000m, target.AmountNumeric);
    }

    [Fact]
    public async Task Update_SourceAfterAssignment_MirrorsLockedFieldsToPendingTargetAndAlertsTargetLawyer()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        var sourceRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == source.Id));
        sourceRequest.BorrowerName = "أحمد مُحدَّث من المنيب";

        await _documentService.UpdateAsync(source.Id, sourceRequest, _lawyer1.FullName, _lawyer1.Id);

        var target = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal("أحمد مُحدَّث من المنيب", target.BorrowerName);

        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer2.Id);
        Assert.Equal(HeadAlertTargetType.Lawyer, alert.TargetType);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Contains(_lawyer2.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("تم تحديث نسخة الملف المناب", alert.Message);
        Assert.Contains("بيانات المقترض", alert.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsLockedFieldChange()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var targetRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        targetRequest.BorrowerName = "تغيير مقفول";

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, targetRequest, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل الحقول المقفولة على الملف المناب", ex.Message);
        Assert.Contains("اسم المقترض", ex.Message);

        var target = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal("أحمد", target.BorrowerName);
    }

    [Fact]
    public async Task Update_TargetMirror_AllowsLocalHeirAddition_AndNotifiesSourceLawyer()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var targetRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        targetRequest.BorrowerHeirs.Add(new HeirDto(null, "مرسى", "أحمد", "الخطيب", null, null, null));

        var updated = await _documentService.UpdateAsync(targetId, targetRequest, _lawyer2.FullName, _lawyer2.Id);
        Assert.NotNull(updated);

        var target = await _db.Documents.Include(d => d.Heirs).AsNoTracking().SingleAsync(d => d.Id == targetId);
        var heir = Assert.Single(target.Heirs);
        Assert.Equal("مرسى", heir.HeirName);
        Assert.Equal("أحمد", heir.HeirFather);

        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer1.Id);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Contains(_lawyer1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("أُضيف الوريث", alert.Message);
        Assert.Contains("مرسى", alert.Message);
    }

    [Fact]
    public async Task Register_TargetCarriesSourceFileType_AndDocumentResponseLinksSourceDelegation()
    {
        var source = await CreateSourceAsync();
        source.FileType = "متداول";
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var delegationDto = (await _service.ListForDocumentAsync(source.Id)).Single();
        Assert.Equal("متداول", delegationDto.SourceFileType);
        Assert.Equal("دمشق", delegationDto.SourceCourt);

        // مسار الملف المناب (FindByTargetAsync): الدائرة المنيبة حاضرة أيضًا في بطاقته.
        var targetDelegationDto = (await _service.ListForDocumentAsync(targetId)).Single();
        Assert.Equal("دمشق", targetDelegationDto.SourceCourt);

        var response = await _documentService.GetAsync(targetId);
        Assert.NotNull(response);
        Assert.Equal(created.Id, response!.SourceDelegationId);
    }

    [Fact]
    public async Task Assign_NotifiesAssignedLawyerViaHeadAlert()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        // تنبيه بموجب نظام تنبيهات رئيس القسم: يُخبر المحامي المختص بإحالة ملف الإنابة عليه
        // (يصل محاميه)، مرتبط بالملف المناب المولّد (ينقله من لوحة التنبيهات إلى صفحة الملف).
        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DocumentId == dto!.TargetDocumentId);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Equal(_head1.Id, alert.CreatedById);
        Assert.Null(alert.TargetLawyerId);
        Assert.Contains(_lawyer2.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("أحال إليك رئيس القسم ملف إنابة لقيده أصولًا", alert.Message);
        Assert.Contains("دائرة تنفيذ حلب", alert.Message);
        Assert.EndsWith("ملف أحمد خالد الخطيب)", alert.Message);
    }

    [Fact]
    public async Task Assign_External_NotifiesAssignedLawyerInExternalBranch()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");

        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_externalLawyer.Id),
            _head2.Id, _otherBranch.Id, "head2");

        // التنبيه في فرع اللاذقية (الفرع المناب) لرؤية محامي الفرع المناب فقط.
        var alert = await _db.HeadAlerts.SingleAsync(a => a.DocumentId == dto!.TargetDocumentId);
        Assert.Equal(_otherBranch.Id, alert.BranchId);
        Assert.Contains(_externalLawyer.Id, alert.Recipients.Select(r => r.UserId));
    }

    [Fact]
    public async Task Assign_WrongHeadBranch_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        // رئيس قسم فرع آخر لا يملك اعتماد إنابة فرع دمشق.
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
                _head2.Id, _otherBranch.Id, "head2"));
        Assert.Contains("ضمن فرعك", ex.Message);
    }

    [Fact]
    public async Task Assign_ToNonLawyer_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        // رئيس القسم نفسه ليس محاميًا مختصًا (لا يمكن أن يُكلَّف بمتابعة الإنابة).
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_head1.Id), _head1.Id, _branch.Id, "head1"));
        Assert.Contains("المحامي المختص", ex.Message);
    }

    [Fact]
    public async Task Register_ByTargetLawyer_MarksRegisteredAndFillsFileData()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        var dto = await _service.RegisterAsync(created.Id,
            new RegisterDelegationRequest("890", "2026", "5/8/2026"), _lawyer2.Id, "lawyer2");

        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.Registered, dto!.Status);
        var target = await _db.Documents.Include(d => d.RegistrationDate).FirstAsync(d => d.Id == targetId);
        Assert.Equal("890", target.FileNumber);
        Assert.Equal("2026", target.FileYear);
        Assert.False(target.IsDraft);
        Assert.Equal("2026-08-05", target.RegistrationDate!.Date);
        Assert.Equal(new DateTime(2026, 8, 5), target.RegistrationDate.DateParsed);
    }

    [Fact]
    public async Task Register_ByWrongLawyer_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
                _lawyer1.Id, "lawyer1"));
        Assert.Contains("لا يمكنك تسجيل", ex.Message);
    }

    [Fact]
    public async Task Register_RaisesDelegation_AroundRegisteredTarget_AndEditPageShowsSameIdentity()
    {
        // عقد «نافذة تسجيل الإنابة أصولًا»: رقم أساس الإنابة وسنة قيدها وتاريخ قيدها تُكتب
        // على اللوحة نفسها لحقول الملف المناب (FileNumber/FileYear/DocumentRegistrationDate)
        // التي تعرضها «صفحة تعديل الملف» — هي عينُها لا نسخٌ متوازية.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        // محاكاة طلب جديد تمامًا: تفريغ متعقب الكيانات — خريطة هوية السياق الواحد تُخفي
        // انفصال النافذتين بجعل القراءة اللاحقة تلتقط المثيل المتتبع نفسه دون الرجوع للقاعدة،
        // فتبقى الاختبارات خضراء بينما يتكسر الربط في الإنتاج (سياق جديد لكل طلب).
        _db.ChangeTracker.Clear();

        await _service.RegisterAsync(created.Id,
            new RegisterDelegationRequest("890", "2026", "5/8/2026"), _lawyer2.Id, "lawyer2");

        // إعادة تحميل الملف المناب من قاعدة البيانات فعليًا (بلا تتبع) مع قناة تاريخ القيد (1:1).
        var target = await _db.Documents
            .AsNoTracking()
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId);

        Assert.Equal("890", target.FileNumber);
        Assert.Equal("2026", target.FileYear);
        Assert.False(target.IsDraft);
        Assert.Equal("2026-08-05", target.RegistrationDate!.Date);
        Assert.Equal(new DateTime(2026, 8, 5), target.RegistrationDate.DateParsed);
    }

    [Fact]
    public async Task Complete_ByTargetLawyer_MarksExecutedAndPrices()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();
        var dto = await _service.CompleteAsync(created.Id,
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto>
            {
                new(assetDto.Id, 750_000m),
            }, "12/8/2026", true), _lawyer2.Id, "lawyer2");

        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.Executed, dto!.Status);
        Assert.Equal("2026-08-10", dto.ReturnDate);
        Assert.Equal(750_000m, dto.Assets.Single().SalePrice);

        // دائرة كاملة لتغطية البدل: يُرسَل → يُخزّن → يُعاد قراءةً ويظهر في الاستجابة.
        Assert.Equal(true, dto.SaleCoversFullDebt);
        var stored = await _db.DocumentDelegations.SingleAsync(d => d.Id == created.Id);
        Assert.Equal(true, stored.SaleCoversFullDebt);

        var target = await _db.Documents.FirstAsync(d => d.Id == targetId);
        Assert.Equal(ExecutionStatusCatalog.DelegationExecuted, target.ExecStatus);
    }

    [Fact]
    public async Task Complete_NotifiesSourceLawyerViaHeadAlert()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");
        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();

        await _service.CompleteAsync(created.Id,
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026", true),
            _lawyer2.Id, "lawyer2");

        // إشعار محامي المنيب بإتمام الإنابة: تنبيه مرتبط بالملف المنيب في فرعه،
        // يصل صاحبه (lawyer1) ويراه رئيس قسمه.
        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DocumentId == source.Id);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Equal(_lawyer2.Id, alert.CreatedById);
        Assert.Null(alert.TargetLawyerId);
        Assert.Contains(_lawyer1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("أُعيدت الإنابة المسطرة", alert.Message);
        Assert.Contains("البدل غطى كامل المديونية", alert.Message);
        Assert.Contains("يرجى تغيير حالة الملف", alert.Message);
        Assert.Contains("المناب ملف 890/2026 للتنفيذ على عقار رقم 77", alert.Message);
    }

    [Fact]
    public async Task Complete_External_NotifiesSourceLawyerInSourceBranch()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_externalLawyer.Id),
            _head2.Id, _otherBranch.Id, "head2");
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _externalLawyer.Id, "externalLawyer");
        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();

        await _service.CompleteAsync(created.Id,
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026", false),
            _externalLawyer.Id, "externalLawyer");

        // على الرغم من أن التنفيذ جرى في فرع اللاذقية، يُنشأ تنبيه الإتمام في فرع الملف المنيب
        // (دمشق) ليكون مرئيًا لمحاميه المختص ورئيس قسمه.
        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DocumentId == source.Id);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Contains(_lawyer1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("أُعيدت الإنابة المسطرة", alert.Message);
        Assert.Contains("البدل لم يغطِ كامل المديونية", alert.Message);
        Assert.Contains("للتنفيذ على عقار رقم 77", alert.Message);
    }

    [Fact]
    public async Task Complete_MissingSalePriceOrReturnDate_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");
        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();

        var noDate = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(created.Id,
                new CompleteDelegationRequest(null, new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026"),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("تاريخ إعادة", noDate.Message);

        var noPrice = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(created.Id,
                new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto>(), "12/8/2026"),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("بدل المبيع", noPrice.Message);

        var noForcedDate = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(created.Id,
                new CompleteDelegationRequest("10/8/2026",
                    new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("تاريخ قرار الإحالة القطعية", noForcedDate.Message);

        var noCoverage = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(created.Id,
                new CompleteDelegationRequest("10/8/2026",
                    new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026"),
                _lawyer2.Id, "lawyer2"));
        Assert.Contains("غطى كامل المديونية", noCoverage.Message);
    }

    /// <summary>إنابة مسجلة أصولًا على منيب متداول (الملف المناب في ملكية lawyer2).</summary>
    private async Task<(int Id, Document Source, Document Target)> CreateRegisteredDelegationAsync()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var target = await _db.Documents.SingleAsync(d => d.Id == assigned!.TargetDocumentId!.Value);
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");
        return (created.Id, source, target);
    }

    [Theory]
    [InlineData(ExecutionStatusCatalog.Deferred)]
    [InlineData(ExecutionStatusCatalog.DelegationExecuted)]
    [InlineData(ExecutionStatusCatalog.StateStruckOff)]
    [InlineData(ExecutionStatusCatalog.Recovered)]
    [InlineData(ExecutionStatusCatalog.ExecutedBySettlement)]
    public async Task Complete_OnNonTradingTarget_RejectsWithE3(string targetStatus)
    {
        // C2 (E3): إتمام الإنابة «لمتداول فقط» — المناب الموروث (تريث)/المنفذ-إنابة/المشطوب/
        // المسترد/المنفذ (تسوية/جبريا) لا يُتمَّم، والرفض قبل أي تحقق آخر من بيانات الطلب.
        var (id, _, target) = await CreateRegisteredDelegationAsync();
        target.ExecStatus = targetStatus;
        if (targetStatus == ExecutionStatusCatalog.ExecutedBySettlement)
            target.ExecSubStatus = null;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(id, new CompleteDelegationRequest(null, null), _lawyer2.Id, "lawyer2"));

        Assert.Contains("لا يمكن تنفيذ انابة في ملف غير متداول", ex.Message);
    }

    [Theory]
    [InlineData(ExecutionStatusCatalog.Deferred, null)]
    [InlineData(ExecutionStatusCatalog.ExecutedBySettlement, null)]
    [InlineData(ExecutionStatusCatalog.ExecutedForcibly, ExecutionStatusCatalog.SubFullyExecuted)]
    [InlineData(ExecutionStatusCatalog.Recovered, null)]
    [InlineData(ExecutionStatusCatalog.StateStruckOff, null)]
    [InlineData(ExecutionStatusCatalog.DelegationExecuted, null)]
    public async Task Complete_OnNonTradingSource_RejectsWithE3(string sourceStatus, string? sourceSubStatus)
    {
        // توسيع B (F10): إنابة متداخلة على مناب منتهٍ لا تُتمَّم — المصدر يجب أن يكون
        // «متداولًا أو منفذًا جزئيًا» وإلا رُفض الإتمام بإعادة E3 ذاتها.
        var (id, source, _) = await CreateRegisteredDelegationAsync();
        source.ExecStatus = sourceStatus;
        source.ExecSubStatus = sourceSubStatus;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CompleteAsync(id, new CompleteDelegationRequest(null, null), _lawyer2.Id, "lawyer2"));

        Assert.Contains("لا يمكن تنفيذ انابة في ملف غير متداول", ex.Message);
    }

    [Fact]
    public async Task Complete_OnTradingTarget_WithPartiallyExecutedSource_Succeeds()
    {
        // المنيب «منفذ جبريا / منفذ جزئيا» (N1) والمناب ما زال متداولًا — الإتمام جائز.
        var (id, source, target) = await CreateRegisteredDelegationAsync();
        source.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
        source.ExecSubStatus = ExecutionStatusCatalog.SubPartiallyExecuted;
        await _db.SaveChangesAsync();

        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();
        var dto = await _service.CompleteAsync(id,
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026", true),
            _lawyer2.Id, "lawyer2");

        Assert.Equal(DelegationStatusCatalog.Executed, dto!.Status);
        Assert.Equal(ExecutionStatusCatalog.DelegationExecuted, target.ExecStatus);
    }

    [Fact]
    public async Task Create_OnDeferredSource_RejectsWithE4()
    {
        // C3 (E4): لا تُسطَّر إنابة على ملف تريث.
        var source = await CreateSourceAsync();
        source.ExecStatus = ExecutionStatusCatalog.Deferred;
        source.TarithNumber = "كتاب-تريث";
        source.TarithDate = "1/9/2026";
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1"));

        Assert.Contains("لا يمكن تسطير انابة في ملف تريث", ex.Message);
    }

    [Fact]
    public async Task Register_OnDeferredSource_RejectsWithE5()
    {
        // N5 (E5): لا تُسجَّل إنابة بعد وصول كتاب تريث في الملف المنيب — الحالة تُفحص لحظة التسجيل.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        source.ExecStatus = ExecutionStatusCatalog.Deferred;
        source.TarithNumber = "كتاب-تريث";
        source.TarithDate = "1/9/2026";
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
                _lawyer2.Id, "lawyer2"));

        Assert.Contains("لا يمكن تسجيل الانابة لورود كتاب تريث في الملف المنيب", ex.Message);
    }

    [Fact]
    public async Task Register_OnSettledSource_RejectsWithValidateSourceMessage()
    {
        // N5: لو أصبح المنيب منفَّذًا (تسوية) بعد الاعتماد تُرفض التسجيل برسالة ValidateSource القائمة.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        source.ExecStatus = ExecutionStatusCatalog.ExecutedBySettlement;
        source.CollectedAmount = 500;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
                _lawyer2.Id, "lawyer2"));

        Assert.Contains("لا يمكن تسطير إنابة على ملف منفَّذ أو مشطوب", ex.Message);
        // التسجيل لم يقع: إنابة لم تُنقل إلى «مسجلة أصولًا».
        Assert.Equal(DelegationStatusCatalog.Assigned,
            (await _db.DocumentDelegations.SingleAsync(d => d.Id == created.Id)).Status);
    }

    [Fact]
    public async Task Create_External_PersistsBranchAndDepositBook()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var dto = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");

        Assert.True(dto.IsExternal);
        Assert.Equal(_otherBranch.Id, dto.ExternalBranchId);
        Assert.Equal("اللاذقية", dto.ExternalBranchName);
        Assert.Equal("كتاب-1", dto.DepositBookNumber);
        Assert.Equal("2026-08-02", dto.DepositBookDate);
    }

    [Fact]
    public async Task Create_External_WithoutOrUnknownBranch_Throws()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var noBranch = SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = null };
        var ex1 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, noBranch, _lawyer1.Id, "lawyer1"));
        Assert.Contains("تتطلب تحديد الفرع المناب", ex1.Message);

        var unknownBranch = SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = 999_999 };
        var ex2 = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, unknownBranch, _lawyer1.Id, "lawyer1"));
        Assert.Contains("الفرع المناب غير موجود", ex2.Message);
    }

    [Fact]
    public async Task Update_Pending_ToExternal_PersistsBranchAndDepositBook()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var updated = await _service.UpdateAsync(created.Id,
            SampleRequest(assetId) with
            {
                IsExternal = true,
                ExternalBranchId = _otherBranch.Id,
                DepositBookNumber = "كتاب-2",
                DepositBookDate = "4/8/2026",
            }, _lawyer1.Id, "lawyer1");

        Assert.NotNull(updated);
        Assert.True(updated!.IsExternal);
        Assert.Equal(_otherBranch.Id, updated.ExternalBranchId);
        Assert.Equal("كتاب-2", updated.DepositBookNumber);
        Assert.Equal("2026-08-04", updated.DepositBookDate);
    }

    [Fact]
    public async Task Assign_External_ByDelegatedBranchHead_CreatesTargetInExternalBranch()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");

        // العكس: رئيس قسم الفرع المنيب لا يملك اعتماد إنابة خارجية.
        var wrongHead = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_externalLawyer.Id),
                _head1.Id, _branch.Id, "head1"));
        Assert.Contains("ضمن فرعك", wrongHead.Message);

        // رئيس قسم الفرع المناب (اللاذقية) يعتمدها ويكلف محاميًا ضمن فرعه.
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_externalLawyer.Id),
            _head2.Id, _otherBranch.Id, "head2");

        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.Assigned, dto!.Status);
        Assert.Equal(_externalLawyer.Id, dto.AssignedLawyerId);
        var target = await _db.Documents.Include(d => d.SourceDelegation).FirstAsync(d => d.Id == dto.TargetDocumentId!.Value);
        Assert.Equal(_otherBranch.Id, target.BranchId);
        Assert.Equal("اللاذقية", target.BranchName);
        Assert.Equal(source.Id, target.SourceDelegation!.SourceDocumentId);
    }

    [Fact]
    public async Task ListPendingForHead_ShowsInternalAndExternalOfHisBranchOnly()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var internalDelegation = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var externalDelegation = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");

        var damPending = await _service.ListPendingForHeadAsync(_branch.Id);
        Assert.Contains(damPending, d => d.Id == internalDelegation.Id);
        Assert.DoesNotContain(damPending, d => d.Id == externalDelegation.Id);

        var latPending = await _service.ListPendingForHeadAsync(_otherBranch.Id);
        Assert.Contains(latPending, d => d.Id == externalDelegation.Id);
        Assert.DoesNotContain(latPending, d => d.Id == internalDelegation.Id);
    }

    // ── دورة حياة تنبيهات الإنابة للنظام (نطاق «head») ─────────────────────────

    [Fact]
    public async Task Create_Pending_NotifiesApprovalBranchHead()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        // تنبيه نظامي لرئيس القسم: «بانتظار اعتماد الإنابة» في فرع الاعتماد (فرع المنيب
        // للداخلية)، مرتبط بالإنابة نفسها (DelegationId) لتصفيته/تحديثه آليًا.
        var alert = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(HeadAlertTargetType.Head, alert.TargetType);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Equal(source.Id, alert.DocumentId);
        Assert.Null(alert.TargetLawyerId);
        Assert.Contains(_head1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("بانتظار اعتماد الإنابة", alert.Message);
        Assert.Contains("دائرة تنفيذ حلب", alert.Message);
    }

    [Fact]
    public async Task Create_External_Pending_NotifiesReceivingBranchHead()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var created = await _service.CreateAsync(source.Id,
            SampleRequest(assetId) with { IsExternal = true, ExternalBranchId = _otherBranch.Id },
            _lawyer1.Id, "lawyer1");

        // الإنابة الخارجية: تنبيه الاعتماد في فرع الجهة المعنية بالاعتماد (الفرع المناب).
        var alert = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(_otherBranch.Id, alert.BranchId);
        Assert.Contains(_head2.Id, alert.Recipients.Select(r => r.UserId));
    }

    [Fact]
    public async Task Update_Pending_RefreshesPendingApprovalMessage()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        await _service.UpdateAsync(created.Id,
            SampleRequest(assetId) with { DelegatedCourt = "دائرة تنفيذ حماة" },
            _lawyer1.Id, "lawyer1");

        // رسالة التنبيه تتحدث بلا تكرار (تبقى واحدة وللمستلم نفسه).
        var alert = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.DelegationId == created.Id);
        Assert.Contains("دائرة تنفيذ حماة", alert.Message);
        Assert.DoesNotContain("دائرة تنفيذ حلب", alert.Message);
        Assert.Contains(_head1.Id, alert.Recipients.Select(r => r.UserId));
    }

    [Fact]
    public async Task Delete_Pending_RemovesLinkedHeadAlerts()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.True(await _service.DeleteAsync(created.Id, _lawyer1.Id, "lawyer1"));

        Assert.Empty(_db.HeadAlerts.Where(a => a.DelegationId == created.Id));
    }

    [Fact]
    public async Task Assign_RemovesPendingApprovalAlert()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        // بعد الاعتماد: لا تنبيه راتق لرئيس القسم، ويبقى تنبيه الملف المناب للمحامي المختص.
        Assert.Empty(_db.HeadAlerts.Where(a => a.DelegationId == created.Id));
        var targetAlert = await _db.HeadAlerts.Include(a => a.Recipients)
            .SingleAsync(a => a.DocumentId == dto!.TargetDocumentId);
        Assert.Contains(_lawyer2.Id, targetAlert.Recipients.Select(r => r.UserId));
    }

    [Fact]
    public async Task Register_CreatesPendingCompletionAlertForTargetBranchHead()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        await _service.RegisterAsync(created.Id,
            new RegisterDelegationRequest("890", "2026", "5/8/2026"), _lawyer2.Id, "lawyer2");

        // «بانتظار الإتمام» في فرع الملف المناب (فرع متابعة الإتمام)، مرتبط بالملف المناب.
        var alert = await _db.HeadAlerts.Include(a => a.Recipients).SingleAsync(a => a.DelegationId == created.Id);
        Assert.Equal(HeadAlertTargetType.Head, alert.TargetType);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Equal(targetId, alert.DocumentId);
        Assert.Contains(_head1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Equal("بانتظار الإتمام", alert.Message);
    }

    [Fact]
    public async Task Complete_RemovesPendingCompletionAlert()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");
        var assetDto = (await _service.ListForDocumentAsync(source.Id)).Single().Assets.Single();

        await _service.CompleteAsync(created.Id,
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026", true),
            _lawyer2.Id, "lawyer2");

        // بعد الإتمام: لا تنبيهات مرحلية للإنابة، ويبقى إشعار «أُعيدت الإنابة… منفذة» لمحامي المنيب.
        Assert.Empty(_db.HeadAlerts.Where(a => a.DelegationId == created.Id));
        var done = await _db.HeadAlerts.SingleAsync(a => a.DocumentId == source.Id);
        Assert.Contains("أُعيدت الإنابة المسطرة", done.Message);
    }

    [Fact]
    public async Task SoftDeletedSource_HidesDelegationFromAllOperations()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        source.IsDeleted = true;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();

        // الفلتر العام (Configurations) يخفي الإنابة مع حذف مصدرها منطقيًا — لا عمليات ولا NRE.
        var exAssign = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id), _head1.Id, _branch.Id, "head1"));
        Assert.Contains("غير موجودة", exAssign.Message);

        var exUpdate = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateAsync(created.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1"));
        Assert.Contains("غير موجودة", exUpdate.Message);

        Assert.DoesNotContain(await _service.ListPendingForHeadAsync(_branch.Id), d => d.Id == created.Id);
    }

    [Fact]
    public async Task SoftDeletedSource_TargetView_ReturnsEmptyWithoutCrash()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        source.IsDeleted = true;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();

        // الملف المناب النشط لا يرى إنابة مصدرها محذوف (بطاقة «تشعبات الملف» فارغة دون انهيار).
        var delegations = await _service.ListForDocumentAsync(targetId);
        Assert.Empty(delegations);
    }

    [Fact]
    public async Task Create_SourceWithOlderBaseNumber_SourceFileNumberShowsEffectiveNotOriginal()
    {
        var source = await CreateSourceAsync();
        source.FileNumber = "520";
        _db.Documents.Update(source);
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = source.Id,
            Year = 2024,
            BaseNumber = "520",
            CreatedById = _lawyer1.Id,
        });
        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = source.Id,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var dto = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.Equal("1500", dto.SourceFileNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), dto.SourceFileYear);
    }

    [Fact]
    public async Task ListForSource_SourceRotatedAfterDelegation_SourceFileNumberUpdates()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        var listed1 = await _service.ListForDocumentAsync(source.Id);
        Assert.Equal("520", Assert.Single(listed1).SourceFileNumber);

        _db.BaseNumbers.Add(new DocumentBaseNumber
        {
            DocumentId = source.Id,
            Year = DateTime.Today.Year,
            BaseNumber = "1500",
            CreatedById = _lawyer1.Id,
        });
        await _db.SaveChangesAsync();

        var listed2 = await _service.ListForDocumentAsync(source.Id);
        Assert.Equal("1500", Assert.Single(listed2).SourceFileNumber);
        Assert.Equal(DateTime.Today.Year.ToString(), Assert.Single(listed2).SourceFileYear);
    }

    // ── حزمة §10: حارس المرآة (B1-B7) + قواعد النظافة (T1-T4) ────────────────

    [Fact]
    public async Task Create_AfterSeizure_TargetCarriesSeizureDate()
    {
        // B5: تاريخ إلقاء الحجز المستندي ينتقل مع الكتب عند إنشاء الإنابة (سطر CopyBooks).
        var source = await CreateSourceAsync();
        source.SeizureDate = "10/8/2026";
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        var target = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == assigned!.TargetDocumentId);
        Assert.Equal("10/8/2026", target.SeizureDate);
    }

    [Fact]
    public async Task Create_DraftSource_RejectsDelegation()
    {
        // T2: رفض التسطير على ملف تحت رفع (بلا رقم قيد) — الرقم ضروري لرابطة المصدر/الهدف.
        var source = await CreateSourceAsync();
        source.IsDraft = true;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1"));
        Assert.Contains("تحت رفع", ex.Message);
    }

    [Fact]
    public async Task Create_PartiallyExecutedSource_AllowsDelegation()
    {
        // T2: «منفذ جبريا/منفذ جزئيا» ما زال متداولًا — الإنابة ترخّص لها (IsExecuted خاطئة).
        var source = await CreateSourceAsync();
        source.ExecStatus = ExecutionStatusCatalog.ExecutedForcibly;
        source.ExecSubStatus = ExecutionStatusCatalog.SubPartiallyExecuted;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();

        var dto = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");

        Assert.True(dto.Id > 0);
        Assert.Equal(DelegationStatusCatalog.PendingHead, dto.Status);
    }

    [Fact]
    public async Task Update_SourceDeletesMiddleGuarantor_MirrorRemovesItAndNamesDeletedHeirs()
    {
        // T3 + B7 + قرار 8: حذف كفيل أوسط من المنيب يمرر إلى المناب مع ورثته المرتبطين برقمه،
        // ويُسمَّى الورثة المحذوفون مع الكفيل المحذوف في التنبيه الموحّد.
        var source = await CreateSourceAsync();
        _db.Guarantors.AddRange(
            new Guarantor
            {
                DocumentId = source.Id, GuarantorNumber = 2,
                GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
                GuarantorNature = PartyNatureCatalog.Natural,
            },
            new Guarantor
            {
                DocumentId = source.Id, GuarantorNumber = 3,
                GuarantorName = "علي", GuarantorFather = "حسن", GuarantorFamily = "الأمين",
                GuarantorNature = PartyNatureCatalog.Natural,
            });
        _db.Heirs.Add(new Heir
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            HeirName = "حسن", HeirFather = "محمود", HeirFamily = "الحلبي",
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;

        // مصدر يحذف الكفيل رقم 2 (مارت بين 2 و3) مع ورثته — يبقى رقم 3 فقط.
        var sourceRequest = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == source.Id));
        sourceRequest.Guarantors = sourceRequest.Guarantors.Where(g => g.GuarantorNumber != 2).ToList();

        await _documentService.UpdateAsync(source.Id, sourceRequest, _lawyer1.FullName, _lawyer1.Id);

        var target = await _db.Documents
            .Include(d => d.Guarantors).Include(d => d.Heirs)
            .AsNoTracking().SingleAsync(d => d.Id == targetId);
        var remaining = Assert.Single(target.Guarantors);
        Assert.Equal(3, remaining.GuarantorNumber);
        Assert.Empty(target.Heirs);

        var alert = await _db.HeadAlerts
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer2.Id);
        Assert.Contains("حُذف من المنيب", alert.Message);
        Assert.Contains("محمود سامي الحلبي", alert.Message);
        Assert.Contains("وورثته (حسن محمود الحلبي)", alert.Message);
    }

    [Fact]
    public async Task Update_Source_WithLocalRepresentativeOnTarget_MirrorSkipsAddressWithoutNoise()
    {
        // B7/قرار 17: عند وجود ممثل محلي على المناب تترك المرآة عنوانه/نوعه كما هو (المتفرغ محليًا)
        // ولا تطلق تنبيه «بيانات المقترض» ضجيجًا.
        var source = await CreateSourceAsync();
        source.BorrowerAddress = "عنوان-المصدر";
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        // ممثل محلي على المناب مع عنوان مخزَّن قبله (الممثل إضافة محلية مسموحة).
        var addRep = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        addRep.BorrowerRepresentativeName = "ممثل";
        addRep.BorrowerRepresentativeFather = "المقترض";
        addRep.BorrowerRepresentativeFamily = "المحلي";
        addRep.BorrowerRepresentativeCapacity = "ولي";
        addRep.BorrowerRepresentativeAddressType = "عنوان";
        addRep.BorrowerRepresentativeAddress = "عنوان-الممثل";
        await _documentService.UpdateAsync(targetId, addRep, _lawyer2.FullName, _lawyer2.Id);

        var before = await _db.HeadAlerts.CountAsync(a => a.DelegationId == created.Id);

        // تغيير عنوان المنيب: المرآة تتجاوز الكتابة فوق عنوان المناب (بلا تنبيه ضجيج).
        var sourceRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == source.Id));
        sourceRequest.BorrowerAddress = "عنوان-المصدر-الجديد";
        await _documentService.UpdateAsync(source.Id, sourceRequest, _lawyer1.FullName, _lawyer1.Id);

        var target = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal("عنوان-المصدر", target.BorrowerAddress);
        Assert.Equal(before, await _db.HeadAlerts.CountAsync(a => a.DelegationId == created.Id));
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsGuarantorHeirRemoveOrEdit()
    {
        // B2/قرار 12: ورثة الكفيل إضافة-فقط بالمفتاح الهوياتي — حذف أو تعديل قائم مرفوض.
        var source = await CreateSourceAsync();
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
            GuarantorNature = PartyNatureCatalog.Natural,
        });
        _db.Heirs.Add(new Heir
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            HeirName = "حسن", HeirFather = "محمود", HeirFamily = "الحلبي",
            AddressType = "عنوان", HeirAddress = "عنوان-كفيل",
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var baseRequest = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var guarantor = baseRequest.Guarantors.Single();

        // حذف الوريث القائم: مرفوض.
        var removeHeir = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        removeHeir.Guarantors = new List<GuarantorDto> { guarantor with { Heirs = new List<HeirDto>() } };
        var exRemove = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, removeHeir, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن حذف ورثة الكفيل", exRemove.Message);
        Assert.Contains("محمود سامي الحلبي", exRemove.Message);

        // تعديل عنوان وريث قائم (نفس الهوية بقيمة مختلفة): مرفوض.
        var editHeir = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        editHeir.Guarantors = new List<GuarantorDto>
        {
            guarantor with
            {
                Heirs = new List<HeirDto> { new(null, "حسن", "محمود", "الحلبي", "أصالة", "عنوان", "عنوان-معدل") },
            },
        };
        var exEdit = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, editHeir, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل ورثة الكفيل", exEdit.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsDuplicateGuarantorNumbersWithValidationError()
    {
        // متانة: رقم الكفيل مفتاح هوية للدمج والمقارنة (لا قيد فريد على (DocumentId, GuarantorNumber)
        // في القاعدة). تكراره يجب أن يُرفض برسالة تحقق صريحة، لا بانهيار ToDictionary الداخلي (500).
        var source = await CreateSourceAsync();
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
            GuarantorNature = PartyNatureCatalog.Natural,
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var duplicated = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var guarantor = duplicated.Guarantors.Single();
        duplicated.Guarantors = new List<GuarantorDto> { guarantor, guarantor with { Id = null } };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, duplicated, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("أرقام الكفلاء مكررة", ex.Message);

        // الحالة المخزَّنة لم تُمسّ (الرفض قبل أي كتابة).
        var stored = await _db.Documents.AsNoTracking().Include(d => d.Guarantors).SingleAsync(d => d.Id == targetId);
        Assert.Equal(2, stored.Guarantors.Single().GuarantorNumber);
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsHeirsOrRepresentativeOnLegalEntity()
    {
        // B3/قرار 12 و16: لا ورثة ولا ممثل على طرف اعتباري (ApplyRequest يُسقطها بصمت فتُرفض صراحةً).
        var source = await CreateSourceAsync();
        source.BorrowerNature = PartyNatureCatalog.Legal;
        source.BorrowerRegistrationNumber = "1234";
        source.BorrowerRepresentedBy = "مديرها";
        // كفيل اعتباري قائم على المنيب: تُنسَخ منه نسخة اعتبارية إلى المناب، ولا يصح عليها
        // لا ورثة ولا ممثل (الحارس يفحص الكفلاء القائمين لا الجدد — يُمنع إضافة كفيل أصلًا).
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "شركة الكفل",
            GuarantorNature = PartyNatureCatalog.Legal,
            GuarantorRegistrationNumber = "99",
            GuarantorRepresentedBy = "مديرها",
        });
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var withBorrowerHeir = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        withBorrowerHeir.BorrowerHeirs.Add(new HeirDto(null, "غريب", "عن", "المقترض", null, null, null));
        var exHeir = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, withBorrowerHeir, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن إضافة ورثة أو ممثل شرعي على المقترض الاعتباري", exHeir.Message);

        var withBorrowerRep = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        withBorrowerRep.BorrowerRepresentativeName = "ممثل";
        withBorrowerRep.BorrowerRepresentativeFather = "على";
        withBorrowerRep.BorrowerRepresentativeFamily = "المقترض";
        var exRep = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, withBorrowerRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن إضافة ورثة أو ممثل شرعي على المقترض الاعتباري", exRep.Message);

        // كفيل اعتباري قائم بممثل مرسَل معه: مرفوض صراحةً (رقمه القائم + حقول تمثيل).
        var withGuarantorRep = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var legal = withGuarantorRep.Guarantors.Single(g => g.GuarantorNumber == 2);
        withGuarantorRep.Guarantors = new List<GuarantorDto>
        {
            legal with
            {
                RepresentativeName = "ممثل",
                RepresentativeFather = "الشركة",
                RepresentativeFamily = "المحلي",
                RepresentativeCapacity = "ولي",
                RepresentativeAddressType = "عنوان",
                RepresentativeAddress = "عنوان-الممثل",
            },
        };
        var exGuarantor = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, withGuarantorRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("على الكفيل الاعتباري", exGuarantor.Message);
        Assert.Contains("شركة الكفل", exGuarantor.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsExistingRepresentativeEditOrRemoval()
    {
        // B3/قرار 16: تعديل ممثل قائم كإزالته مرفوض — لا صمت.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var addRep = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        addRep.BorrowerRepresentativeName = "ممثل";
        addRep.BorrowerRepresentativeFather = "المقترض";
        addRep.BorrowerRepresentativeFamily = "المحلي";
        addRep.BorrowerRepresentativeCapacity = "ولي";
        addRep.BorrowerRepresentativeAddressType = "عنوان";
        addRep.BorrowerRepresentativeAddress = "عنوان-الممثل";
        await _documentService.UpdateAsync(targetId, addRep, _lawyer2.FullName, _lawyer2.Id);

        // إزالة الممثل القائم (الطلب بلا ممثل): مرفوض.
        var removeRep = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        removeRep.BorrowerRepresentativeName = null;
        removeRep.BorrowerRepresentativeFather = null;
        removeRep.BorrowerRepresentativeFamily = null;
        removeRep.BorrowerRepresentativeCapacity = null;
        removeRep.BorrowerRepresentativeAddressType = null;
        removeRep.BorrowerRepresentativeAddress = null;
        var exRemove = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, removeRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن إزالة الممثل الشرعي للمقترض", exRemove.Message);

        // تعديل اسم الممثل القائم: مرفوض.
        var editRep = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        editRep.BorrowerRepresentativeName = "اسم-معدل";
        var exEdit = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, editRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل الممثل الشرعي للمقترض", exEdit.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_RejectsExistingGuarantorRepresentativeEditOrRemoval()
    {
        // B3/قرار 16: ممثل الكفيل القائم كممثل المقترض — تعديله أو إزالته مرفوض.
        var source = await CreateSourceAsync();
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
            GuarantorNature = PartyNatureCatalog.Natural,
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var addRep = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var existing = addRep.Guarantors.Single();
        addRep.Guarantors = new List<GuarantorDto>
        {
            existing with
            {
                RepresentativeName = "ولي",
                RepresentativeFather = "الكفيل",
                RepresentativeFamily = "المحلي",
                RepresentativeCapacity = "ولي",
                RepresentativeAddressType = "عنوان",
                RepresentativeAddress = "عنوان-الممثل",
            },
        };
        await _documentService.UpdateAsync(targetId, addRep, _lawyer2.FullName, _lawyer2.Id);

        var removeRep = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var stored = removeRep.Guarantors.Single();
        removeRep.Guarantors = new List<GuarantorDto>
        {
            stored with
            {
                RepresentativeName = null, RepresentativeFather = null, RepresentativeFamily = null,
                RepresentativeCapacity = null, RepresentativeAddressType = null, RepresentativeAddress = null,
            },
        };
        var exRemove = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, removeRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن إزالة الممثل الشرعي للكفيل", exRemove.Message);

        var editRep = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var withRep = editRep.Guarantors.Single();
        editRep.Guarantors = new List<GuarantorDto> { withRep with { RepresentativeName = "اسم-معدل" } };
        var exEdit = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, editRep, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل الممثل الشرعي للكفيل", exEdit.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_ReverseAlertNamesTheGuarantorOfAddedHeir()
    {
        // B2/قرار 12: التنبيه العكسي للمنيب يُسمّي الكفيل عند إضافة وريث له («لكفيل-N»).
        var source = await CreateSourceAsync();
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
            GuarantorNature = PartyNatureCatalog.Natural,
        });
        _db.Heirs.Add(new Heir
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            HeirName = "حسن", HeirFather = "محمود", HeirFamily = "الحلبي",
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var request = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var existing = request.Guarantors.Single();
        request.Guarantors = new List<GuarantorDto>
        {
            existing with
            {
                Heirs = new List<HeirDto>
                {
                    new(null, "حسن", "محمود", "الحلبي", "أصالة", "عنوان", null),
                    new(null, "قاسم", "محمود", "الحلبي", "أصالة", "عنوان", null),
                },
            },
        };

        await _documentService.UpdateAsync(targetId, request, _lawyer2.FullName, _lawyer2.Id);

        var target = await _db.Documents.Include(d => d.Heirs).AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal(2, target.Heirs.Count);

        var alert = await _db.HeadAlerts
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer1.Id);
        Assert.Contains("أُضيف الوريث قاسم محمود الحلبي", alert.Message);
        Assert.Contains("لكفيل-2", alert.Message);
    }

    [Fact]
    public async Task RestoreStruckOff_NotifiesPendingTargetsWithStatusChangeAlert()
    {
        // B4: التراجع عن الشطب يُنبه المنابات المعلقة بالنص الموحد كالشطب ذاته.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");

        source.ExecStatus = ExecutionStatusCatalog.StateStruckOff;
        _db.Documents.Update(source);
        await _db.SaveChangesAsync();

        var restored = await _documentService.RestoreStruckOffAsync(source.Id,
            new RenewalRequest { RenewalFileNumber = "899", RenewalYear = 2026 }, "lawyer1");
        Assert.True(restored);

        var alert = await _db.HeadAlerts
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer2.Id);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Contains("تغيّرت حالة الملف المنيب", alert.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_RepeatSaveWithHeirs_AcceptedWithEmptyAddress()
    {
        // T4/قرار 13: حفظ متكرر بلا إضافة (مع ورثة) لا يُرفض ويُخزَّن العنوان فارغًا باطراد.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var addHeir = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        addHeir.BorrowerHeirs.Add(new HeirDto(null, "مرسى", "أحمد", "الخطيب", "أصالة", "عنوان", null));
        await _documentService.UpdateAsync(targetId, addHeir, _lawyer2.FullName, _lawyer2.Id);

        var repeated = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));

        var updated = await _documentService.UpdateAsync(targetId, repeated, _lawyer2.FullName, _lawyer2.Id);
        Assert.NotNull(updated);

        var stored = await _db.Documents.Include(d => d.Heirs).AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Single(stored.Heirs);
        // العنوان فارغ (ثابت «ورثة/ممثل ⟺ عنوان فارغ» — قرار 13-ب)؛ نوع العنوان يبقى قيمةً
        // تزيينية منسوخة لا تُعرض بلا عنوان (الواجهة تحتفظ به أيضًا — لا يُفحص هنا).
        Assert.Null(stored.BorrowerAddress);
    }

    [Fact]
    public async Task Update_TargetMirror_AddRepresentativeOverHeirs_AllEmptiedAddressesAccepted()
    {
        // T4/قرار 14: ممثل مع ورثة (ورثة قائمة بعناوين مخزَّنة من قبل) — يحميه إعفاء حضور
        // فيُقبل التفريغ الجماعي للعناوين بلا رفض كاذب.
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var addHeirs = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        addHeirs.BorrowerHeirs.AddRange(new List<HeirDto>
        {
            new(null, "مرسى", "أحمد", "الخطيب", "أصالة", "عنوان", "عنوان-وريث-1"),
            new(null, "سلمى", "أحمد", "الخطيب", "أصالة", "عنوان", "عنوان-وريث-2"),
        });
        await _documentService.UpdateAsync(targetId, addHeirs, _lawyer2.FullName, _lawyer2.Id);

        // إضافة ممثل: النموذج يفرّغ عناوين كل الورثة — يُقبل (إعفاء حضور للجميع).
        var addRep = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        addRep.BorrowerRepresentativeName = "ممثل";
        addRep.BorrowerRepresentativeFather = "لها";
        addRep.BorrowerRepresentativeFamily = "المحلي";
        addRep.BorrowerRepresentativeCapacity = "ولي";
        addRep.BorrowerRepresentativeAddressType = "عنوان";
        addRep.BorrowerRepresentativeAddress = "عنوان-الممثل";
        addRep.BorrowerHeirs = addRep.BorrowerHeirs.Select(h => h with { Address = null }).ToList();

        var updated = await _documentService.UpdateAsync(targetId, addRep, _lawyer2.FullName, _lawyer2.Id);
        Assert.NotNull(updated);

        var stored = await _db.Documents.Include(d => d.Heirs).AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal(2, stored.Heirs.Count);
        Assert.All(stored.Heirs, h => Assert.Equal(string.Empty, h.HeirAddress));
        Assert.Equal("ممثل", stored.BorrowerRepresentativeName);

        var alert = await _db.HeadAlerts
            .SingleAsync(a => a.DelegationId == created.Id && a.TargetLawyerId == _lawyer1.Id);
        Assert.Contains("الممثل الشرعي للمقترض", alert.Message);
    }

    [Fact]
    public async Task Update_TargetMirror_GuarantorRepresentativeOverStoredAddressAndHeirs_AcceptedAndEmptied()
    {
        // T4/قرار 15: ممثل الكفيل مع عنوانه وورثته المخزَّنين — مقبول ويُصفَّر (إعفاء حضور).
        var source = await CreateSourceAsync();
        _db.Guarantors.Add(new Guarantor
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            GuarantorName = "محمود", GuarantorFather = "سامي", GuarantorFamily = "الحلبي",
            GuarantorAddress = "عنوان-كفيل", AddressType = "عنوان", GuarantorNature = PartyNatureCatalog.Natural,
        });
        _db.Heirs.Add(new Heir
        {
            DocumentId = source.Id, GuarantorNumber = 2,
            HeirName = "حسن", HeirFather = "محمود", HeirFamily = "الحلبي",
            AddressType = "عنوان", HeirAddress = "عنوان-وريث",
        });
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var request = MirrorRequestWithParties(await _db.Documents
            .Include(d => d.RegistrationDate)
            .Include(d => d.Guarantors)
            .Include(d => d.Heirs)
            .SingleAsync(d => d.Id == targetId));
        var existing = request.Guarantors.Single();
        request.Guarantors = new List<GuarantorDto>
        {
            existing with
            {
                Address = null, AddressType = "عنوان",
                RepresentativeName = "ولي", RepresentativeFather = "الكفيل", RepresentativeFamily = "المحلي",
                RepresentativeCapacity = "ولي", RepresentativeAddressType = "عنوان", RepresentativeAddress = "عنوان-الممثل",
                Heirs = new List<HeirDto> { existing.Heirs!.Single() with { Address = null } },
            },
        };

        var updated = await _documentService.UpdateAsync(targetId, request, _lawyer2.FullName, _lawyer2.Id);
        Assert.NotNull(updated);

        var stored = await _db.Documents
            .Include(d => d.Guarantors).Include(d => d.Heirs)
            .AsNoTracking().SingleAsync(d => d.Id == targetId);
        var guarantor = Assert.Single(stored.Guarantors);
        Assert.Null(guarantor.GuarantorAddress);
        Assert.Equal("ولي", guarantor.RepresentativeName);
        var heir = Assert.Single(stored.Heirs);
        Assert.Equal(string.Empty, heir.HeirAddress);
    }

    [Fact]
    public async Task Update_TargetMirror_EmptyingAddressWithoutFamily_StillRejected()
    {
        // T4/قرار 13: تفريغ العنوان بلا ورثة/ممثل في الطلب يبقى فرقًا مقفولًا (لا إعفاء حضور).
        var source = await CreateSourceAsync();
        source.BorrowerAddress = "عنوان-محفوظ";
        source.BorrowerAddressType = "عنوان";
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        var request = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        request.BorrowerAddress = null;
        request.BorrowerAddressType = null;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, request, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل الحقول المقفولة على الملف المناب", ex.Message);
        Assert.Contains("عنوان المقترض", ex.Message);

        var stored = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal("عنوان-محفوظ", stored.BorrowerAddress);
    }

    /// <summary>حالة اختبار تكافؤ قوائم الحقول: نسخ الإنشاء ↔ دمج المرآة ↔ فحوص الحارس.</summary>
    private sealed record FieldEquivalenceCase(
        string Label,
        Func<Document, object?> EntityGet,
        Action<Document, object?> EntitySet,
        Action<DocumentUpsertRequest, object?> RequestSet,
        object? V1,
        object? V2,
        object? Flip);

    private async Task AssertFieldEquivalenceAsync(FieldEquivalenceCase c)
    {
        // 1) نسخ الإنشاء: الهدف يحمل قيمة المصدر عند الاعتماد.
        var source = await CreateSourceAsync();
        c.EntitySet(source, c.V1);
        await _db.SaveChangesAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var assigned = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = assigned!.TargetDocumentId!.Value;
        var copied = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal(c.V1, c.EntityGet(copied));

        await _service.RegisterAsync(created.Id, new RegisterDelegationRequest("890", "2026", "5/8/2026"),
            _lawyer2.Id, "lawyer2");

        // 2) دمج المرآة: تعديل المنيب يُحدث المناب بنفس الحقل.
        var sourceRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == source.Id));
        c.RequestSet(sourceRequest, c.V2);
        await _documentService.UpdateAsync(source.Id, sourceRequest, _lawyer1.FullName, _lawyer1.Id);
        var mirrored = await _db.Documents.AsNoTracking().SingleAsync(d => d.Id == targetId);
        Assert.Equal(c.V2, c.EntityGet(mirrored));

        // 3) فحص الحارس: إعادة إرسال ما يُخزَّن بعد المرآة مقبولة (ائتلاف بدل انحراف القوائم).
        var roundTrip = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        await _documentService.UpdateAsync(targetId, roundTrip, _lawyer2.FullName, _lawyer2.Id);

        // 4) فحص الحارس: قلب نفس الحقل يبقى مرفوضًا (مقفولًا ومُزامنًا).
        var flipRequest = MirrorRequest(await _db.Documents
            .Include(d => d.RegistrationDate)
            .SingleAsync(d => d.Id == targetId));
        c.RequestSet(flipRequest, c.Flip);
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UpdateAsync(targetId, flipRequest, _lawyer2.FullName, _lawyer2.Id));
        Assert.Contains("لا يمكن تعديل الحقول المقفولة على الملف المناب", ex.Message);
    }

    [Fact]
    public async Task MirrorAndGuard_FieldLists_AreEquivalentAcrossCreateMirrorGuard()
    {
        // B6: قائمة الحقول المشتركة بين (نسخ الإنشاء ↔ دمج المرآة ↔ فحوص الحارس)
        // للسند/الكتب/المقترض — أي انحرافٍ بين المسارات يوقف الاختبار على الحقل المشتبه.
        var textCase = (string label, Func<Document, string?> get, Action<Document, string?> set,
            Action<DocumentUpsertRequest, string?> requestSet) => new FieldEquivalenceCase(
            label, d => get(d), (d, v) => set(d, (string?)v), (r, v) => requestSet(r, (string?)v),
            $"{label}-ق1", $"{label}-ق2", $"{label}-مخالف");

        var cases = new List<FieldEquivalenceCase>();

        // السند التنفيذي (النصوص).
        cases.AddRange(new[]
        {
            textCase("نوع العقد", d => d.ContractType, (d, v) => d.ContractType = v, (r, v) => r.ContractType = v),
            textCase("نوع العقد (المفصل)", d => d.ContractTypeSelector, (d, v) => d.ContractTypeSelector = v, (r, v) => r.ContractTypeSelector = v),
            textCase("رقم العقد", d => d.ContractNumber, (d, v) => d.ContractNumber = v, (r, v) => r.ContractNumber = v),
            textCase("تاريخ العقد", d => d.ContractDate, (d, v) => d.ContractDate = v, (r, v) => r.ContractDate = v),
            textCase("نوع الإلحاق", d => d.AnnexType, (d, v) => d.AnnexType = v, (r, v) => r.AnnexType = v),
            textCase("رقم الإلحاق", d => d.AnnexNumber, (d, v) => d.AnnexNumber = v, (r, v) => r.AnnexNumber = v),
            textCase("تاريخ الإلحاق", d => d.AnnexDate, (d, v) => d.AnnexDate = v, (r, v) => r.AnnexDate = v),
            textCase("نص الإدراج", d => d.InclusionText, (d, v) => d.InclusionText = v, (r, v) => r.InclusionText = v),
            textCase("مبلغ العقد كتابة", d => d.AmountWords, (d, v) => d.AmountWords = v, (r, v) => r.AmountWords = v),
            textCase("عملة المبلغ الأول", d => d.Currency, (d, v) => d.Currency = v, (r, v) => r.Currency = v),
            textCase("المبلغ الثاني كتابة", d => d.Amount2Words, (d, v) => d.Amount2Words = v, (r, v) => r.Amount2Words = v),
            textCase("عملة المبلغ الثاني", d => d.Currency2, (d, v) => d.Currency2 = v, (r, v) => r.Currency2 = v),
            textCase("المبلغ الثالث كتابة", d => d.Amount3Words, (d, v) => d.Amount3Words = v, (r, v) => r.Amount3Words = v),
            textCase("عملة المبلغ الثالث", d => d.Currency3, (d, v) => d.Currency3 = v, (r, v) => r.Currency3 = v),
            textCase("المبلغ المدرج كتابة", d => d.InclusionAmountWords, (d, v) => d.InclusionAmountWords = v, (r, v) => r.InclusionAmountWords = v),
            textCase("عملة المبلغ المدرج", d => d.InclusionCurrency, (d, v) => d.InclusionCurrency = v, (r, v) => r.InclusionCurrency = v),
            textCase("المبلغ المدرج الثاني كتابة", d => d.InclusionAmount2Words, (d, v) => d.InclusionAmount2Words = v, (r, v) => r.InclusionAmount2Words = v),
            textCase("عملة المبلغ المدرج الثاني", d => d.InclusionCurrency2, (d, v) => d.InclusionCurrency2 = v, (r, v) => r.InclusionCurrency2 = v),
            textCase("المبلغ المدرج الثالث كتابة", d => d.InclusionAmount3Words, (d, v) => d.InclusionAmount3Words = v, (r, v) => r.InclusionAmount3Words = v),
            textCase("عملة المبلغ المدرج الثالث", d => d.InclusionCurrency3, (d, v) => d.InclusionCurrency3 = v, (r, v) => r.InclusionCurrency3 = v),
            // الدائرة خارج عقد المرآة: حقيقة مستقلة للمناب (المنابة المسجَّل فيها) —
            // لا تُنسخ عند الاعتماد ولا تُزامَن ولا يُفحص تكافؤها (يغطيها TargetCourt_IsIndependent).
        });

        // السند التنفيذي (المبالغ).
        var numericCase = (string label, Func<Document, decimal> get, Action<Document, decimal> set,
            Action<DocumentUpsertRequest, decimal> requestSet) => new FieldEquivalenceCase(
            label, d => get(d), (d, v) => set(d, (decimal)v!), (r, v) => requestSet(r, (decimal)v!),
            1000.25m, 2000.25m, 3333m);
        cases.AddRange(new[]
        {
            numericCase("المبلغ (الأول)", d => d.AmountNumeric, (d, v) => d.AmountNumeric = v, (r, v) => r.AmountNumeric = v),
            numericCase("المبلغ (الثاني)", d => d.Amount2Numeric, (d, v) => d.Amount2Numeric = v, (r, v) => r.Amount2Numeric = v),
            numericCase("المبلغ (الثالث)", d => d.Amount3Numeric, (d, v) => d.Amount3Numeric = v, (r, v) => r.Amount3Numeric = v),
            numericCase("المبلغ المدرج (الأول)", d => d.InclusionAmountNumeric, (d, v) => d.InclusionAmountNumeric = v, (r, v) => r.InclusionAmountNumeric = v),
            numericCase("المبلغ المدرج (الثاني)", d => d.InclusionAmount2Numeric, (d, v) => d.InclusionAmount2Numeric = v, (r, v) => r.InclusionAmount2Numeric = v),
            numericCase("المبلغ المدرج (الثالث)", d => d.InclusionAmount3Numeric, (d, v) => d.InclusionAmount3Numeric = v, (r, v) => r.InclusionAmount3Numeric = v),
        });

        // الكتب (النصوص) + تاريخ القاء الحجز. حقلّا «ورود الإخطار التنفيذي» (FileReceiptNumber/
        // FileReceiptDate) خاصان بوضع «منفذ عليه» ويُصفَّران على طالبة تنفيذ — خارج عقد المرآة.
        cases.AddRange(new[]
        {
            textCase("رقم ورود الملف", d => d.FileArrivalNumber, (d, v) => d.FileArrivalNumber = v, (r, v) => r.FileArrivalNumber = v),
            textCase("تاريخ ورود الملف", d => d.FileArrivalDate, (d, v) => d.FileArrivalDate = v, (r, v) => r.FileArrivalDate = v),
            textCase("رقم كتاب الجهة العامة", d => d.FileIncoming, (d, v) => d.FileIncoming = v, (r, v) => r.FileIncoming = v),
            textCase("تاريخ كتاب الجهة العامة", d => d.FileIncomingDate, (d, v) => d.FileIncomingDate = v, (r, v) => r.FileIncomingDate = v),
            textCase("رقم تحت رفع", d => d.UnderFilingNumber, (d, v) => d.UnderFilingNumber = v, (r, v) => r.UnderFilingNumber = v),
            textCase("تاريخ القاء الحجز", d => d.SeizureDate, (d, v) => d.SeizureDate = v, (r, v) => r.SeizureDate = v),
        });

        // نواة المقترض (عدا حقول الممثل المحلية وقيم طبيعة/رقم تسجيل الاعتباري التي تُصهر في الحفظ).
        cases.AddRange(new[]
        {
            textCase("اسم المقترض", d => d.BorrowerName, (d, v) => d.BorrowerName = v, (r, v) => r.BorrowerName = v),
            textCase("اسم والد المقترض", d => d.BorrowerFather, (d, v) => d.BorrowerFather = v, (r, v) => r.BorrowerFather = v),
            textCase("اسم عائلة المقترض", d => d.BorrowerFamily, (d, v) => d.BorrowerFamily = v, (r, v) => r.BorrowerFamily = v),
            textCase("اسم أم المقترض", d => d.BorrowerMother, (d, v) => d.BorrowerMother = v, (r, v) => r.BorrowerMother = v),
            textCase("تاريخ ولادة المقترض", d => d.BorrowerBirth, (d, v) => d.BorrowerBirth = v, (r, v) => r.BorrowerBirth = v),
            textCase("سجل المقترض", d => d.BorrowerRegister, (d, v) => d.BorrowerRegister = v, (r, v) => r.BorrowerRegister = v),
            textCase("الرقم الوطني للمقترض", d => d.BorrowerNationalId, (d, v) => d.BorrowerNationalId = v, (r, v) => r.BorrowerNationalId = v),
            textCase("عنوان المقترض", d => d.BorrowerAddress, (d, v) => d.BorrowerAddress = v, (r, v) => r.BorrowerAddress = v),
            textCase("نوع عنوان المقترض", d => d.BorrowerAddressType, (d, v) => d.BorrowerAddressType = v, (r, v) => r.BorrowerAddressType = v),
            new FieldEquivalenceCase("طبيعة المقترض",
                d => d.BorrowerNature, (d, v) => d.BorrowerNature = (string)v!, (r, v) => r.BorrowerNature = (string)v!,
                PartyNatureCatalog.Natural, PartyNatureCatalog.Legal, PartyNatureCatalog.Natural),
        });

        foreach (var c in cases)
            await AssertFieldEquivalenceAsync(c);
    }
}
