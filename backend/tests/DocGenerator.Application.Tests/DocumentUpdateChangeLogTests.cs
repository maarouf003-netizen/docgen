using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبار طرف-إلى-طرف لسجل تعديلات الملف: تعديل نموذجي عبر UpdateAsync
/// يُنتج إدخال تدقيق بصفوف «حقل/قبل/بعد»، والتعديل دون تغيير يمر بلا صفوف.
/// </summary>
public class DocumentUpdateChangeLogTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer;

    public DocumentUpdateChangeLogTests()
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

        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        _service = new DocumentService(
            documents, users,
            new Repository<Guarantor>(_db),
            new Repository<Asset>(_db),
            new Repository<ExecutionAction>(_db),
            new Repository<DocumentBaseNumber>(_db),
            new Repository<DocumentRegistrationDate>(_db),
            new Repository<DocumentOccurrence>(_db),
            new DelegationRepository(_db),
            new AppealRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit,
            Microsoft.Extensions.Options.Options.Create(new DocGenerator.Application.Common.ExportOptions()));
    }

    public void Dispose() => _db.Dispose();

    private static DocumentUpsertRequest Sample() => new()
    {
        BorrowerName = "أحمد",
        BorrowerFather = "محمد",
        BorrowerFamily = "العلي",
        AmountNumeric = 1000,
        Currency = "ليرة سورية",
        Court = "دائرة تنفيذ دمشق",
        Applicant = "المصرف",
    };

    [Fact]
    public async Task Update_WithChangedFields_LogsFieldRows()
    {
        var created = await _service.CreateAsync(Sample(), _lawyer.Id, "المحامي الأول",
            branchId: _lawyer.BranchId!.Value);

        var editRequest = Sample();
        editRequest.BorrowerName = "أحمد سعيد";
        editRequest.AmountNumeric = 3000.75m;
        await _service.UpdateAsync(created.Id, editRequest, "المحامي الأول", _lawyer.Id);

        var entry = Assert.Single(_audit.ChangeLogs);
        Assert.Equal("update", entry.ActionType);
        Assert.Equal(created.Id, entry.DocumentId);

        var byKey = entry.Changes.ToDictionary(c => c.FieldKey);
        Assert.Equal("اسم المنفذ عليه", byKey[nameof(Document.BorrowerName)].FieldLabel);
        Assert.Equal("أحمد", byKey[nameof(Document.BorrowerName)].OldValue);
        Assert.Equal("أحمد سعيد", byKey[nameof(Document.BorrowerName)].NewValue);
        Assert.Equal("1000", byKey[nameof(Document.AmountNumeric)].OldValue);
        Assert.Equal("3000.75", byKey[nameof(Document.AmountNumeric)].NewValue);
        // الحقول غير المتغيرة لا تُسجَّل
        Assert.False(byKey.ContainsKey(nameof(Document.Court)));
    }

    [Fact]
    public async Task Update_WithoutChanges_LogsPlainUpdateOnly()
    {
        var created = await _service.CreateAsync(Sample(), _lawyer.Id, "المحامي الأول",
            branchId: _lawyer.BranchId!.Value);

        await _service.UpdateAsync(created.Id, Sample(), "المحامي الأول", _lawyer.Id);

        // لا صفوف تغييرات: التعديل المطابق للأصل يمر كسجل تدقيق نصي فقط
        Assert.Empty(_audit.ChangeLogs);
        Assert.Contains("update", _audit.Actions);
    }
}
