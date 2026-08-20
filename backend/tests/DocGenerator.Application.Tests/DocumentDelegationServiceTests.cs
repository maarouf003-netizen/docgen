using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class DocumentDelegationServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentDelegationService _service;
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

        _service = new DocumentDelegationService(
            new DelegationRepository(_db),
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Branch>(_db),
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            new HeadAlertService(
                new HeadAlertRepository(_db),
                new DocumentRepository(_db),
                new UserRepository(_db),
                new Repository<Branch>(_db),
                new UnitOfWork(_db),
                new TransactionRunner(_db),
                _audit));
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
        Assert.Equal(source.Court, target.Court);
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
        source.FileReceiptNumber = "قيد-9";
        source.FileReceiptDate = new DateTime(2026, 8, 7);
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

        // كتب الملف المنيب تنتقل كلها.
        Assert.Equal("ورود-7", target.FileArrivalNumber);
        Assert.Equal("5/8/2026", target.FileArrivalDate);
        Assert.Equal("كتاب-الجهة-44", target.FileIncoming);
        Assert.Equal("6/8/2026", target.FileIncomingDate);
        Assert.Equal("تحت-3", target.UnderFilingNumber);
        Assert.Equal("قيد-9", target.FileReceiptNumber);
        Assert.Equal(new DateTime(2026, 8, 7), target.FileReceiptDate);
    }

    [Fact]
    public async Task Assign_TargetIsFrozenSnapshot_IndependentFromSourceEdits()
    {
        var source = await CreateSourceAsync();
        var assetId = await _db.Assets.Where(a => a.DocumentId == source.Id).Select(a => a.Id).SingleAsync();
        var created = await _service.CreateAsync(source.Id, SampleRequest(assetId), _lawyer1.Id, "lawyer1");
        var dto = await _service.AssignAsync(created.Id, new AssignDelegationRequest(_lawyer2.Id),
            _head1.Id, _branch.Id, "head1");
        var targetId = dto!.TargetDocumentId!.Value;

        // تعديل الملف المنيب بعد الاعتماد لا يمس الملف المناب (لقطة مجمدة منفصلة).
        source.BorrowerName = "غيّر-حالياً";
        source.AmountNumeric = 2_000_000;
        await _db.SaveChangesAsync();

        var target = await _db.Documents.FindAsync(targetId);
        Assert.Equal("أحمد", target!.BorrowerName);
        Assert.Equal(1_000_000, target.AmountNumeric);
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
            }, "12/8/2026"), _lawyer2.Id, "lawyer2");

        Assert.NotNull(dto);
        Assert.Equal(DelegationStatusCatalog.Executed, dto!.Status);
        Assert.Equal("2026-08-10", dto.ReturnDate);
        Assert.Equal(750_000m, dto.Assets.Single().SalePrice);

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
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026"),
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
        Assert.Contains("نفذت إنابتك في ملف 890/2026", alert.Message);
        Assert.Contains("للتنفيذ على عقار رقم 77", alert.Message);
        Assert.Contains("يرجى المراجعة والمتابعة أصولًا", alert.Message);
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
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026"),
            _externalLawyer.Id, "externalLawyer");

        // على الرغم من أن التنفيذ جرى في فرع اللاذقية، يُنشأ تنبيه الإتمام في فرع الملف المنيب
        // (دمشق) ليكون مرئيًا لمحاميه المختص ورئيس قسمه.
        var alert = await _db.HeadAlerts
            .Include(a => a.Recipients)
            .SingleAsync(a => a.DocumentId == source.Id);
        Assert.Equal(_branch.Id, alert.BranchId);
        Assert.Contains(_lawyer1.Id, alert.Recipients.Select(r => r.UserId));
        Assert.Contains("نفذت إنابتك في ملف 890/2026", alert.Message);
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
        Assert.Contains("بانتظار الإتمام", alert.Message);
        Assert.Contains("890", alert.Message);
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
            new CompleteDelegationRequest("10/8/2026", new List<DelegationSaleDto> { new(assetDto.Id, 750_000m) }, "12/8/2026"),
            _lawyer2.Id, "lawyer2");

        // بعد الإتمام: لا تنبيهات مرحلية للإنابة، ويبقى إشعار «نفذت إنابتك» لمحامي المنيب.
        Assert.Empty(_db.HeadAlerts.Where(a => a.DelegationId == created.Id));
        var done = await _db.HeadAlerts.SingleAsync(a => a.DocumentId == source.Id);
        Assert.Contains("نفذت إنابتك", done.Message);
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
}
