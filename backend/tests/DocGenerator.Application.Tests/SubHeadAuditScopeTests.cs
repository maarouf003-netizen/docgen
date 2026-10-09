using DocGenerator.Application.Common;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

/// <summary>
/// النطاق الدائري لسجل التدقيق (F6 — قرار 23): رئيس الشعبة يرى أحداث مستندات دوائر
/// شعبته وأحداث فاعلي شعبته فقط — أحداث شعبة أخرى/بلا دائرة/بلا نسب مخفية.
/// مرآة نطاق الفرع القائم (RF-007) على `ownerSectionId`.
/// </summary>
public class SubHeadAuditScopeTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly AuditLogRepository _logs;
    private readonly AuditLogService _service;
    private readonly Branch _branch;
    private readonly int _sectionA;
    private readonly int _sectionB;
    private readonly int _circuitA;
    private readonly int _circuitB;
    private readonly Document _docA;
    private readonly Document _docB;
    private readonly Document _docNone;

    public SubHeadAuditScopeTests()
    {
        _db = TestDb.Create();
        _branch = new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" };
        _db.Branches.Add(_branch);
        _db.SaveChanges();

        AddUser("lawyer1", "محامي دمشق", UserRole.Lawyer, null);
        var lawyer = _db.Users.Single(u => u.Username == "lawyer1");

        _sectionA = AddSection("شعبة أ");
        _sectionB = AddSection("شعبة ب");
        _circuitA = AddCircuit("دائرة أ", _sectionA, lawyer.Id);
        _circuitB = AddCircuit("دائرة ب", _sectionB, lawyer.Id);

        AddUser("sub_a", "رئيس شعبة أ", UserRole.SubHead, _sectionA);
        AddUser("sub_b", "رئيس شعبة ب", UserRole.SubHead, _sectionB);

        _docA = AddDocument(lawyer.Id, _circuitA);
        _docB = AddDocument(lawyer.Id, _circuitB);
        _docNone = AddDocument(lawyer.Id, null);

        _logs = new AuditLogRepository(_db);
        _service = new AuditLogService(_logs);
    }

    public void Dispose() => _db.Dispose();

    private int AddSection(string name)
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
        _db.SaveChanges();
        return section.Id;
    }

    private int AddCircuit(string name, int? sectionId, int createdById)
    {
        var circuit = new ExecutionCircuit
        {
            BranchId = _branch.Id,
            SectionId = sectionId,
            Name = name,
            NameNorm = ArabicNameNormalizer.Normalize(name),
            IsActive = true,
            CreatedById = createdById,
        };
        _db.ExecutionCircuits.Add(circuit);
        _db.SaveChanges();
        return circuit.Id;
    }

    private void AddUser(string username, string fullName, UserRole role, int? sectionId)
    {
        _db.Users.Add(new User
        {
            Username = username,
            FullName = fullName,
            Role = role,
            BranchId = _branch.Id,
            SectionId = sectionId,
            IsActive = true,
            PasswordHash = "x",
        });
        _db.SaveChanges();
    }

    private static int s_seq;

    private Document AddDocument(int ownerId, int? circuitId)
    {
        var number = $"780{System.Threading.Interlocked.Increment(ref s_seq):D4}";
        var doc = new Document
        {
            BranchId = _branch.Id,
            CreatedById = ownerId,
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
        _db.Documents.Add(doc);
        _db.SaveChanges();
        return doc;
    }

    private void AddEvent(string? userName, int? documentId, string action = "view_document")
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserName = userName,
            ActionType = action,
            DocumentId = documentId,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Search_SubHeadSeesOwnSectionEventsOnly()
    {
        AddEvent("lawyer1", _docA.Id); // حدث على مستند دائرة شعبته — مرئي.
        AddEvent("sub_a", null); // حدث فاعل شعبته بلا مستند — مرئي.
        AddEvent("lawyer1", _docB.Id); // مستند شعبة أخرى — مخفي.
        AddEvent("lawyer1", _docNone.Id); // مستند بلا دائرة — مخفي.
        AddEvent("sub_b", null); // فاعل شعبة أخرى — مخفي.
        AddEvent("lawyer1", null); // بلا نسب (لا مستند ولا فاعل شعبة) — مخفي.

        var (total, items) = await _logs.SearchAsync(null, null, 1, 20, default, null, _sectionA);

        Assert.Equal(2, total);
        Assert.Contains(items, a => a.DocumentId == _docA.Id);
        Assert.Contains(items, a => a.UserName == "sub_a" && a.DocumentId == null);
    }

    [Fact]
    public async Task Search_SectionScopeFlowsThroughService()
    {
        AddEvent("lawyer1", _docA.Id);
        AddEvent("lawyer1", _docB.Id);

        var page = await _service.SearchAsync(null, null, 1, 20, default, null, _sectionA);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(_docA.Id, Assert.Single(page.Items).DocumentId);
    }

    [Fact]
    public async Task Search_BranchScopeUnchanged_AndUnscopedSeesAll()
    {
        AddEvent("lawyer1", _docA.Id);
        AddEvent("lawyer1", _docNone.Id);

        var (branchTotal, _) = await _logs.SearchAsync(null, null, 1, 20, default, _branch.Id, null);
        Assert.Equal(2, branchTotal);

        var (fullTotal, _) = await _logs.SearchAsync(null, null, 1, 20);
        Assert.Equal(2, fullTotal);
    }
}
