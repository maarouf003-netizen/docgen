using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace DocGenerator.Application.Tests;

/// <summary>
/// الإسقاط العمودي للتصدير: القوائم الصغيرة (طالبو التنفيذ/المنفذ عليهم/الإجراءات/
/// أرقام الأساس) والرقم الفعّال والحالة تُسقط بقيم مطابقة لمسار الكيان الكامل —
/// حماية عقد الورقة بعد إسقاط `Include` الشجرة.
/// </summary>
public class ExportProjectionTests : IDisposable
{
    private readonly DocGeneratorDbContext _db = TestDb.Create();
    private readonly FakeAuditLogger _audit = new();
    private int _userId;

    public void Dispose() => _db.Dispose();

    private IDocumentService Build()
    {
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer_proj",
            FullName = "محامي الإسقاط",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        _userId = _db.Users.First(u => u.Username == "lawyer_proj").Id;

        return new DocumentService(
            new DocumentRepository(_db),
            new UserRepository(_db),
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db),
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new DelegationRepository(_db),
            new AppealRepository(_db),
            new HeadAlertService(new HeadAlertRepository(_db), new DocumentRepository(_db), new UserRepository(_db), new Repository<Branch>(_db), new UnitOfWork(_db), new TransactionRunner(_db), _audit),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            Options.Create(new ExportOptions { MaxRows = 10 }),
            TimeProvider.System,
            TestClock.TimeZone,
            new DbExceptionClassifier());
    }

    [Fact]
    public async Task ExportAsync_ProjectsRelatedListsAndEffectiveIdentity()
    {
        var svc = Build();
        var created = await svc.CreateAsync(new DocumentUpsertRequest
        {
            BorrowerName = "مقترض الإسقاط",
            Applicant = "المصرف",
            Court = "دمشق",
            Guarantors = new List<GuarantorDto>(),
            Assets = new List<AssetDto>(),
            BorrowerHeirs = new List<HeirDto>(),
            ExecutionApplicants = new List<ExecutionApplicantDto>(),
            ExecutedPublicEntities = new List<ExecutedPublicEntityDto>(),
            ExecutedNaturalPersons = new List<ExecutedNaturalPersonDto>(),
        }, _userId, "tester", branchId: 1);

        _db.ExecutionApplicants.Add(new ExecutionApplicant { DocumentId = created.Id, Name = "طالب", Father = "أب", Family = "عائلة" });
        _db.ExecutedPublicEntities.Add(new ExecutedPublicEntity { DocumentId = created.Id, EntityName = "الجهة المنفذ عليها" });
        _db.ExecutedNaturalPersons.Add(new ExecutedNaturalPerson { DocumentId = created.Id, Name = "طبيعي", Father = "أ", Family = "ع" });
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = created.Id, Text = "إجراء أول", CreatedById = _userId });
        _db.ExecutionActions.Add(new ExecutionAction { DocumentId = created.Id, Text = "إجراء ثانٍ", CreatedById = _userId });
        _db.BaseNumbers.Add(new DocumentBaseNumber { DocumentId = created.Id, Year = 2024, BaseNumber = "777", CreatedById = _userId, CreatedAt = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc) });
        await _db.SaveChangesAsync();

        var rows = await svc.ExportAsync(null, null, null, null, null, null, null, null, null);
        var row = Assert.Single(rows);

        Assert.Equal("مقترض الإسقاط", row.BorrowerName);
        Assert.Equal("المصرف", row.Applicant);
        var applicant = Assert.Single(row.ExecutionApplicants);
        Assert.Equal("طالب", applicant.Name);
        Assert.Equal("أب", applicant.Father);
        Assert.Equal("عائلة", applicant.Family);
        Assert.Equal(new[] { "الجهة المنفذ عليها" }, row.ExecutedPublicEntities.Select(e => e.EntityName).ToList());
        Assert.Equal("طبيعي", Assert.Single(row.ExecutedNaturalPersons).Name);
        // الأول حصريًا (Take(1)) — الثاني لا يُسقط.
        Assert.Equal("إجراء أول", Assert.Single(row.ExecutionActions).Text);
        // الرقم الفعّال من البذور (2024 ≤ السنة الحالية).
        Assert.Equal("777", row.DisplayFileNumber);
        Assert.Equal("2024", row.DisplayFileYear);
        // الحالة على الصف تطابق الكيان الكامل عبر المحلّل نفسه.
        var full = await svc.GetAsync(created.Id);
        Assert.NotNull(full);
        Assert.Equal(full!.DisplayStatus, DocumentStatusResolver.Resolve(row));
    }
}
