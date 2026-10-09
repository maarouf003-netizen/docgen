using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// نطاق الشعبة للقوائم الأربع (§5 — قرار §2.21): رئيس الشعبة يرى ملفات دوائر شعبته
/// فقط في المحذوفة/المشطوبة/المنفذة/المحالة للبداية (بلا ملفات شعبة أخرى وبلا بلا-دائرة)،
/// ورئيس القسم يرى نطاق القسم كاملًا، والإدارة ترى الكل.
/// نقطة النطاق الوحيدة `ApplyOwnerScope` — والمتحكم يمرر `ownerSectionId` (يغطيه حارس
/// `subhead-guards.mjs`)، فالتغطية هنا على السلوك عبر المستودع مباشرة.
/// </summary>
public class SubHeadDocumentScopeTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly DocumentRepository _documents;
    private readonly Branch _branch;
    private readonly User _lawyer;
    private static int s_seq;

    public SubHeadDocumentScopeTests()
    {
        _db = TestDb.Create();
        _branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(_branch);
        _db.SaveChanges();
        _lawyer = new User
        {
            Username = "lawyer1",
            FullName = "محامي دمشق",
            Role = UserRole.Lawyer,
            BranchId = _branch.Id,
            IsActive = true,
            PasswordHash = new Services.PasswordHasher().Hash("123456"),
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();
        _documents = new DocumentRepository(_db);
    }

    public void Dispose() => _db.Dispose();

    private async Task<int> AddSectionAsync(string name)
    {
        var section = new Section
        {
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            BranchId = _branch.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Sections.Add(section);
        await _db.SaveChangesAsync();
        return section.Id;
    }

    private async Task<int> AddCircuitAsync(string name, int? sectionId)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = _branch.Id,
            SectionId = sectionId,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
            CreatedById = _lawyer.Id,
        };
        _db.ExecutionCircuits.Add(circuit);
        await _db.SaveChangesAsync();
        return circuit.Id;
    }

    private async Task<Document> AddDocumentAsync(int? circuitId, Action<Document>? shape = null)
    {
        var number = $"770{System.Threading.Interlocked.Increment(ref s_seq):D4}";
        var doc = new Document
        {
            BranchId = _branch.Id,
            CreatedById = _lawyer.Id,
            IsDraft = false,
            BorrowerName = "أحمد",
            BorrowerFather = "خالد",
            BorrowerFamily = "الخطيب",
            FileNumber = $"{number}/2026",
            FileType = "تنفيذي",
            FileYear = "2026",
            Court = "دائرة تنفيذ دمشق",
            ExecutionCircuitId = circuitId,
            AmountNumeric = 0,
            ExecStatus = string.Empty,
        };
        shape?.Invoke(doc);
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        return doc;
    }

    [Fact]
    public async Task SearchDeleted_SubHeadSeesOwnSectionOnly()
    {
        var sectionA = await AddSectionAsync("شعبة أ");
        var sectionB = await AddSectionAsync("شعبة ب");
        var circuitA = await AddCircuitAsync("دائرة أ", sectionA);
        var circuitB = await AddCircuitAsync("دائرة ب", sectionB);
        var circuitDiv = await AddCircuitAsync("دائرة القسم", null);
        static void Deleted(Document d) { d.IsDeleted = true; d.DeletedAt = DateTime.UtcNow; }
        var docA = await AddDocumentAsync(circuitA, Deleted);
        var docB = await AddDocumentAsync(circuitB, Deleted);
        var docNone = await AddDocumentAsync(null, Deleted);
        var docDiv = await AddDocumentAsync(circuitDiv, Deleted);

        var sub = await _documents.SearchDeletedAsync(null, _branch.Id, null, 1, 20, default, sectionA);
        Assert.Equal(docA.Id, Assert.Single(sub.Items).Id);

        // القسم: ملفاته فقط (بلا دائرة + دوائر بلا شعبة) — ملفات الشعب لمالكها (§5).
        var head = await _documents.SearchDeletedAsync(null, _branch.Id, null, 1, 20, default, null);
        Assert.Equal(2, head.TotalCount);
        Assert.Contains(head.Items, d => d.Id == docNone.Id);
        Assert.Contains(head.Items, d => d.Id == docDiv.Id);
        Assert.DoesNotContain(head.Items, d => d.Id == docA.Id);
        Assert.DoesNotContain(head.Items, d => d.Id == docB.Id);

        var full = await _documents.SearchDeletedAsync(null, null, null, 1, 20);
        Assert.Equal(4, full.TotalCount);
    }

    [Fact]
    public async Task SearchStruckOff_SubHeadSeesOwnSectionOnly()
    {
        var sectionA = await AddSectionAsync("شعبة أ");
        var sectionB = await AddSectionAsync("شعبة ب");
        var circuitA = await AddCircuitAsync("دائرة أ", sectionA);
        var circuitB = await AddCircuitAsync("دائرة ب", sectionB);
        static void StruckOff(Document d)
        {
            d.GeneralEntitySide = GeneralEntitySideCatalog.Executed;
            d.ExecutedStatus = ExecutedStatusCatalog.StruckOff;
        }
        var docA = await AddDocumentAsync(circuitA, StruckOff);
        var docB = await AddDocumentAsync(circuitB, StruckOff);
        var docNone = await AddDocumentAsync(null, StruckOff);
        var circuitDiv = await AddCircuitAsync("دائرة القسم", null);
        var docDiv = await AddDocumentAsync(circuitDiv, StruckOff);

        var sub = await _documents.SearchStruckOffAsync(
            null, null, null, null, null, null, _branch.Id, null, 1, 20, default, sectionA);
        Assert.Equal(docA.Id, Assert.Single(sub.Items).Id);

        var head = await _documents.SearchStruckOffAsync(
            null, null, null, null, null, null, _branch.Id, null, 1, 20, default, null);
        Assert.Equal(2, head.TotalCount);
        Assert.Contains(head.Items, d => d.Id == docDiv.Id);
        Assert.Contains(head.Items, d => d.Id == docNone.Id);
        Assert.DoesNotContain(head.Items, d => d.Id == docB.Id);
    }

    [Fact]
    public async Task SearchExecuted_SubHeadSeesOwnSectionOnly()
    {
        var sectionA = await AddSectionAsync("شعبة أ");
        var sectionB = await AddSectionAsync("شعبة ب");
        var circuitA = await AddCircuitAsync("دائرة أ", sectionA);
        var circuitB = await AddCircuitAsync("دائرة ب", sectionB);
        static void Executed(Document d)
        {
            d.GeneralEntitySide = GeneralEntitySideCatalog.Executed;
            d.ExecutedStatus = ExecutedStatusCatalog.Executed;
        }
        var docA = await AddDocumentAsync(circuitA, Executed);
        var docB = await AddDocumentAsync(circuitB, Executed);
        var docNone = await AddDocumentAsync(null, Executed);
        var circuitDiv = await AddCircuitAsync("دائرة القسم", null);
        var docDiv = await AddDocumentAsync(circuitDiv, Executed);

        var sub = await _documents.SearchExecutedAsync(null, _branch.Id, null, 1, 20, default, sectionA);
        Assert.Equal(docA.Id, Assert.Single(sub.Items).Id);

        var head = await _documents.SearchExecutedAsync(null, _branch.Id, null, 1, 20, default, null);
        Assert.Equal(2, head.TotalCount);
        Assert.Contains(head.Items, d => d.Id == docDiv.Id);
        Assert.Contains(head.Items, d => d.Id == docNone.Id);
        Assert.DoesNotContain(head.Items, d => d.Id == docB.Id);
    }

    [Fact]
    public async Task SearchReferredToStart_SubHeadSeesOwnSectionOnly()
    {
        var sectionA = await AddSectionAsync("شعبة أ");
        var sectionB = await AddSectionAsync("شعبة ب");
        var circuitA = await AddCircuitAsync("دائرة أ", sectionA);
        var circuitB = await AddCircuitAsync("دائرة ب", sectionB);
        static void Referred(Document d)
        {
            d.GeneralEntitySide = GeneralEntitySideCatalog.Applicant;
            d.ExecStatus = ExecutionStatusCatalog.ReferredToStart;
        }
        var docA = await AddDocumentAsync(circuitA, Referred);
        var docB = await AddDocumentAsync(circuitB, Referred);
        var docNone = await AddDocumentAsync(null, Referred);
        var circuitDiv = await AddCircuitAsync("دائرة القسم", null);
        var docDiv = await AddDocumentAsync(circuitDiv, Referred);

        var sub = await _documents.SearchReferredToStartAsync(null, _branch.Id, null, 1, 20, default, sectionA);
        Assert.Equal(docA.Id, Assert.Single(sub.Items).Id);

        var head = await _documents.SearchReferredToStartAsync(null, _branch.Id, null, 1, 20, default, null);
        Assert.Equal(2, head.TotalCount);
        Assert.Contains(head.Items, d => d.Id == docDiv.Id);
        Assert.Contains(head.Items, d => d.Id == docNone.Id);
        Assert.DoesNotContain(head.Items, d => d.Id == docB.Id);
    }
}
