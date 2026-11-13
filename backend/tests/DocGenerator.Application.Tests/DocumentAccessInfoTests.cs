using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// انحدار F-01: إسقاط الوصول الخفيف يجب أن يعيد الحقول الخمسة نفسها التي تعتمد
/// عليها قاعدة الصلاحيات، ويحترم فلتر الحذف المنطقي، دون تحميل شجرة المستند.
/// </summary>
public sealed class DocumentAccessInfoTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly DocumentRepository _repository;

    public DocumentAccessInfoTests()
    {
        _db = TestDb.Create();
        _repository = new DocumentRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetAccessInfo_ReturnsBranchCircuitAndSection_ForLiveDocument()
    {
        var (document, circuit, branch, section) = await SeedAsync(withCircuit: true);

        var info = await _repository.GetAccessInfoAsync(document.Id);

        Assert.NotNull(info);
        Assert.Equal(document.Id, info!.Id);
        Assert.Equal(document.CreatedById, info.CreatedById);
        Assert.Equal(branch.Id, info.BranchId);
        Assert.Equal(circuit!.Id, info.ExecutionCircuitId);
        Assert.Equal(section!.Id, info.SectionId);
    }

    [Fact]
    public async Task GetAccessInfo_ReturnsNullSection_WhenDocumentHasNoCircuit()
    {
        var (document, _, _, _) = await SeedAsync(withCircuit: false);

        var info = await _repository.GetAccessInfoAsync(document.Id);

        Assert.NotNull(info);
        Assert.Null(info!.ExecutionCircuitId);
        Assert.Null(info.SectionId);
    }

    [Fact]
    public async Task GetAccessInfo_ReturnsNull_ForMissingAndSoftDeletedDocuments()
    {
        var (document, _, _, _) = await SeedAsync(withCircuit: true);
        document.IsDeleted = true;
        await _db.SaveChangesAsync();

        Assert.Null(await _repository.GetAccessInfoAsync(document.Id));
        Assert.Null(await _repository.GetAccessInfoAsync(document.Id + 999));
    }

    private async Task<(Document Document, ExecutionCircuit? Circuit, Branch Branch, Section? Section)> SeedAsync(
        bool withCircuit)
    {
        var branch = new Branch { Name = $"فرع-{Guid.NewGuid():N}"[..20], Code = $"AC{Guid.NewGuid():N}"[..10] };
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var section = new Section { BranchId = branch.Id, Name = "شعبة الوصول" };
        var user = new User
        {
            Username = $"access_{Guid.NewGuid():N}"[..20],
            FullName = "فاحص الوصول",
            Role = UserRole.Head,
            BranchId = branch.Id,
            PasswordHash = "x",
        };
        _db.Sections.Add(section);
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        ExecutionCircuit? circuit = null;
        if (withCircuit)
        {
            circuit = new ExecutionCircuit
            {
                BranchId = branch.Id,
                SectionId = section.Id,
                Name = "دائرة الوصول",
                CreatedById = user.Id,
            };
            _db.ExecutionCircuits.Add(circuit);
            await _db.SaveChangesAsync();
        }

        var document = new Document
        {
            BranchId = branch.Id,
            CreatedById = user.Id,
            ExecutionCircuitId = circuit?.Id,
            BorrowerName = "مدين الوصول",
            GeneralEntitySide = GeneralEntitySideCatalog.Applicant,
        };
        _db.Documents.Add(document);
        await _db.SaveChangesAsync();

        return (document, circuit, branch, circuit is null ? null : section);
    }
}
