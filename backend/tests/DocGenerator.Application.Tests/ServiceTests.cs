using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

public class FakeAuditLogger : IAuditLogger
{
    public List<string> Actions { get; } = new();

    public Task LogAsync(string? userName, string actionType, int? documentId = null,
        string? documentType = null, string? details = null, CancellationToken ct = default)
    {
        Actions.Add(actionType);
        return Task.CompletedTask;
    }
}

public class DocumentServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IDocumentService _service;
    private readonly FakeAuditLogger _audit = new();

    public DocumentServiceTests()
    {
        _db = TestDb.Create();
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" });
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
        var documents = new DocumentRepository(_db);
        var users = new UserRepository(_db);
        var guarantors = new Repository<Guarantor>(_db);
        var estates = new Repository<RealEstate>(_db);
        var actions = new Repository<ExecutionAction>(_db);
        var registrationDates = new Repository<DocumentRegistrationDate>(_db);
        var uow = new UnitOfWork(_db);
        var tx = new TransactionRunner(_db);
        _service = new DocumentService(documents, users, guarantors, estates, actions, registrationDates, uow, tx, _audit);
    }

    public void Dispose() => _db.Dispose();

    private static DocumentUpsertRequest Sample() => new()
    {
        BorrowerName = "أحمد",
        BorrowerFather = "خالد",
        BorrowerFamily = "الخطيب",
        AmountNumeric = 1250,
        Currency = "ليرة سورية",
        ContractNumber = "12/2024",
        Court = "دمشق",
        Applicant = "المدعي",
        Lawyer = "محامي",
        FileNumber = "520",
        FileYear = "2024",
        Guarantors = new()
        {
            new GuarantorDto(null, 1, "سمير", "حسن", "علي", null, null, null, null, "حلب", "موطن مختار"),
        },
        RealEstates = new()
        {
            new RealEstateDto(null, "المدعى عليه", "بيت", "12345", "المزة", "الصالحية", "تمام العقار"),
        },
    };

    [Fact]
    public async Task Create_FillsDerivedFields()
    {
        var doc = await _service.CreateAsync(Sample(), userId: 1, actorName: "lawyer1", branchId: 1);

        Assert.True(doc.Id > 0);
        Assert.False(doc.IsDraft);
        Assert.False(string.IsNullOrWhiteSpace(doc.AmountWords));
        Assert.Contains("ليرة سورية", doc.AmountWords!);
        Assert.False(string.IsNullOrWhiteSpace(doc.DocumentType));
        Assert.Contains("أحمد", doc.DocumentType!);
        Assert.Single(doc.Guarantors);
        Assert.Single(doc.RealEstates);
        Assert.Contains("create", _audit.Actions);
    }

    [Fact]
    public async Task Create_WithoutFileNumber_IsDraft()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var doc = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);

        Assert.True(doc.IsDraft);
        Assert.Contains("تحت رفع", doc.DocumentType!);
    }

    [Fact]
    public async Task Update_AddingFileNumberAndYear_BecomesMutadawal()
    {
        var req = Sample();
        req.FileNumber = "";
        req.FileYear = "";
        var draft = await _service.CreateAsync(req, userId: 1, actorName: "lawyer1", branchId: 1);
        Assert.True(draft.IsDraft);
        Assert.Contains("تحت رفع", draft.DocumentType!);

        var update = Sample();
        update.DocumentType = draft.DocumentType;
        var doc = await _service.UpdateAsync(draft.Id, update, actorName: "lawyer1");

        Assert.False(doc!.IsDraft);
        Assert.StartsWith("متداول", doc.DocumentType!);
        Assert.DoesNotContain("تحت رفع", doc.DocumentType!);
    }

    [Fact]
    public async Task Search_ByQuery_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.BorrowerName = "سعاد";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("أحمد", result.Items[0].BorrowerName);
    }

    [Fact]
    public async Task Search_ByGuarantorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("سمير", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_RespectsPagination()
    {
        for (int i = 0; i < 5; i++)
        {
            var req = Sample();
            req.BorrowerName = $"شخص {i}";
            await _service.CreateAsync(req, 1, "lawyer1", 1);
        }

        var page = await _service.SearchAsync(null, null, null, null, null, null, 2, 2);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task Search_ByBinaryDebtorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد الخطيب", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByTripleDebtorName_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("أحمد خالد الخطيب", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByContractNumber_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("12/2024", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByCourt_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync("دمشق", null, null, null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByApplicantFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Applicant = "مدعي آخر";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, "المدعي", null, null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByCourtFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Court = "حلب";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, "حلب", null, null, 1, 20);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_ByLawyerFilter_FiltersResults()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Lawyer = "محامي آخر";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, "محامي آخر", null, 1, 20);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("محامي آخر", result.Items[0].Lawyer);
    }

    [Fact]
    public async Task GetFilterOptions_ReturnsDistinctApplicantsAndCourts()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req2 = Sample();
        req2.Applicant = "مدعي آخر";
        await _service.CreateAsync(req2, 1, "lawyer1", 1);

        var (applicants, courts, lawyers) = await _service.GetFilterOptionsAsync();
        Assert.Contains("المدعي", applicants);
        Assert.Contains("مدعي آخر", applicants);
        Assert.Contains("دمشق", courts);
        Assert.Contains("محامي", lawyers);
    }

    [Fact]
    public async Task Search_ReturnsAdministrativeBranchName()
    {
        await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchAsync(null, null, null, null, null, null, 1, 20);
        var doc = Assert.Single(result.Items);
        Assert.Equal("دمشق", doc.AdministrativeBranchName);
    }

    [Fact]
    public async Task Transfer_MovesDocumentToTargetLawyer()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Users.Add(new User
        {
            Username = "lawyer2",
            FullName = "محامي ثانٍ",
            Role = UserRole.Lawyer,
            BranchId = 1,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        var transferred = await _service.TransferAsync(created.Id, 2, "head1");

        Assert.Equal(2, transferred.CreatedById);
        Assert.Equal("محامي ثانٍ", transferred.Lawyer);
        Assert.Equal("محامي ثانٍ", transferred.CreatedByName);
        Assert.Contains("transfer", _audit.Actions);
    }

    [Fact]
    public async Task Transfer_ToLawyerInAnotherBranch_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Branches.Add(new Branch { Name = "حلب", Code = "ALP" });
        _db.Users.Add(new User
        {
            Username = "lawyer3",
            FullName = "محامي حلب",
            Role = UserRole.Lawyer,
            BranchId = 2,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 2, "head1"));
    }

    [Fact]
    public async Task Transfer_ToInactiveLawyer_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        _db.Users.Add(new User
        {
            Username = "lawyer4",
            FullName = "محامي موقوف",
            Role = UserRole.Lawyer,
            BranchId = 1,
            IsActive = false,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 2, "head1"));
    }

    [Fact]
    public async Task Transfer_ToCurrentOwner_Throws()
    {
        var created = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.TransferAsync(created.Id, 1, "head1"));
    }

    [Fact]
    public async Task Update_ChangesBorrower()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var req = Sample();
        req.BorrowerName = "محمود";

        var updated = await _service.UpdateAsync(doc.Id, req, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("محمود", updated!.BorrowerName);
        Assert.Contains("update", _audit.Actions);
    }

    [Fact]
    public async Task Create_SavesFileRegistrationDate()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";

        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);

        Assert.NotNull(doc);
        Assert.Equal("1/8/2026", doc!.FileRegistrationDate);
        var reloaded = await _service.GetAsync(doc.Id);
        Assert.Equal("1/8/2026", reloaded!.FileRegistrationDate);
    }

    [Fact]
    public async Task Update_ChangesFileRegistrationDate()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var update = Sample();
        update.FileRegistrationDate = "2/8/2026";

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("2/8/2026", updated!.FileRegistrationDate);
        var reloaded = await _service.GetAsync(doc.Id);
        Assert.Equal("2/8/2026", reloaded!.FileRegistrationDate);
    }

    [Fact]
    public async Task Update_ClearingFileRegistrationDateRemovesRow()
    {
        var req = Sample();
        req.FileRegistrationDate = "1/8/2026";
        var doc = await _service.CreateAsync(req, 1, "lawyer1", 1);
        var update = Sample();
        update.FileRegistrationDate = "";

        var updated = await _service.UpdateAsync(doc.Id, update, "lawyer1");

        Assert.NotNull(updated);
        Assert.Null(updated!.FileRegistrationDate);
        Assert.Equal(0, _db.DocumentRegistrationDates.Count());
    }

    [Fact]
    public async Task UpdateStatus_Settlement_SetsBaraetFields()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?>
            {
                ["baraetNumber"] = "77",
                ["baraetDate"] = "1/1/2024",
                ["collectedAmount"] = "500.50",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ بالتسوية", updated!.ExecStatus);
        Assert.Equal("77", updated.BaraetNumber);
        Assert.Equal(500.50m, updated.CollectedAmount);
        Assert.Contains("status", _audit.Actions);
    }

    [Fact]
    public async Task UpdateStatus_ForcedPartial_SetsSubStatusAndCollectedAmount()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ جزئيا",
                ["collectedAmount"] = "1000",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ جبريا", updated!.ExecStatus);
        Assert.Equal("منفذ جزئيا", updated.ExecSubStatus);
        Assert.Equal(1000m, updated.CollectedAmount);
        Assert.Null(updated.BaraetNumber);
    }

    [Fact]
    public async Task UpdateStatus_InvalidCollectedAmount_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["collectedAmount"] = "abc" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_NegativeCollectedAmount_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["collectedAmount"] = "-100" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_InvalidExecSubStatus_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
                new Dictionary<string, string?> { ["execSubStatus"] = "غير معروف" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Deferred_MissingTarithNumberDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "تريث",
                new Dictionary<string, string?> { ["tarithNumber"] = "5" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_Settlement_MissingBaraetNumberDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
                new Dictionary<string, string?> { ["baraetNumber"] = "77" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateStatus_ForcedComplete_SetsCollectedAmountOnly()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var ok = await _service.UpdateStatusAsync(doc.Id, "منفذ جبريا",
            new Dictionary<string, string?>
            {
                ["execSubStatus"] = "منفذ كاملا",
                ["collectedAmount"] = "1000",
            }, "lawyer1");

        Assert.True(ok);
        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal("منفذ كاملا", updated!.ExecSubStatus);
        Assert.Equal(1000m, updated.CollectedAmount);
        Assert.Null(updated.BaraetNumber);
        Assert.Null(updated.BaraetDate);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateStatusAsync(doc.Id, "حالة-غير-صالحة", new Dictionary<string, string?>(), "lawyer1"));
    }

    [Fact]
    public async Task CancelStatus_ClearsFields()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.UpdateStatusAsync(doc.Id, "منفذ بالتسوية",
            new Dictionary<string, string?> { ["baraetNumber"] = "77", ["baraetDate"] = "1/1/2024" }, "lawyer1");

        await _service.CancelStatusAsync(doc.Id, "lawyer1");

        var updated = await _service.GetAsync(doc.Id);
        Assert.Equal(string.Empty, updated!.ExecStatus);
        Assert.Null(updated.BaraetNumber);
    }

    [Fact]
    public async Task Delete_SoftDeletesDocument_AndHidesItFromQueries()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var ok = await _service.DeleteAsync(doc.Id, "head1");
        Assert.True(ok);
        Assert.Null(await _service.GetAsync(doc.Id));
        Assert.Contains("delete", _audit.Actions);

        // الصف لا يزال في القاعدة لكن موسوماً كمحذوف منطقياً
        var row = _db.Documents.IgnoreQueryFilters().First(d => d.Id == doc.Id);
        Assert.True(row.IsDeleted);
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task Delete_ThenRestore_MakesDocumentVisibleAgain()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(doc.Id, "head1");
        Assert.Null(await _service.GetAsync(doc.Id));

        var restored = await _service.RestoreAsync(doc.Id, "head1");
        Assert.True(restored);
        Assert.NotNull(await _service.GetAsync(doc.Id));
        Assert.Contains("restore", _audit.Actions);

        var row = _db.Documents.IgnoreQueryFilters().First(d => d.Id == doc.Id);
        Assert.False(row.IsDeleted);
        Assert.Null(row.DeletedAt);
    }

    [Fact]
    public async Task Restore_NonDeletedOrUnknownDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        Assert.False(await _service.RestoreAsync(doc.Id, "head1"));
        Assert.False(await _service.RestoreAsync(99_999, "head1"));
    }

    [Fact]
    public async Task SearchDeleted_ReturnsOnlyDeletedDocuments_WithDeletedAt()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync(null, 1, 20);

        Assert.Equal(1, result.TotalCount);
        Assert.Contains(result.Items, d => d.Id == deleted.Id);
        Assert.DoesNotContain(result.Items, d => d.Id == active.Id);
        Assert.NotNull(result.Items.Single().DeletedAt);
    }

    [Fact]
    public async Task SearchDeleted_AfterRestore_NoLongerListed()
    {
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");
        await _service.RestoreAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync(null, 1, 20);

        Assert.DoesNotContain(result.Items, d => d.Id == deleted.Id);
    }

    [Fact]
    public async Task SearchDeleted_ByQuery_FiltersByName()
    {
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        var result = await _service.SearchDeletedAsync("أحمد خالد الخطيب", 1, 20);
        Assert.Contains(result.Items, d => d.Id == deleted.Id);

        var noMatch = await _service.SearchDeletedAsync("اسم غير موجود", 1, 20);
        Assert.Empty(noMatch.Items);
    }

    [Fact]
    public async Task SearchDeleted_WithNoDeletedDocuments_ReturnsEmpty()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var result = await _service.SearchDeletedAsync(null, 1, 20);
        Assert.Empty(result.Items);
        Assert.NotNull(await _service.GetAsync(active.Id));
    }

    [Fact]
    public async Task GetDeleted_ReturnsOnlyDeletedDocuments()
    {
        var active = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var deleted = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.DeleteAsync(deleted.Id, "head1");

        Assert.Null(await _service.GetDeletedAsync(active.Id));
        Assert.Null(await _service.GetDeletedAsync(99_999));

        var dto = await _service.GetDeletedAsync(deleted.Id);
        Assert.NotNull(dto);
        Assert.Equal(deleted.Id, dto!.Id);
        Assert.NotNull(dto.DeletedAt);
    }

    [Fact]
    public async Task Create_WithoutBorrowerName_Throws()
    {
        var req = Sample();
        req.BorrowerName = "  ";
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(req, 1, "lawyer1", 1));
    }

    [Fact]
    public async Task AddAction_AddsExecutionAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "تم إشعار المنفذ عليه", ActionDate = "1/8/2026" },
            userId: 1, actorName: "lawyer1");

        Assert.True(action.Id > 0);
        Assert.Equal("تم إشعار المنفذ عليه", action.Text);
        Assert.Equal("1/8/2026", action.ActionDate);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Single(list);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task AddAction_SavesReminderDurationAndColor()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "متابعة مع المحكمة",
                ActionDate = "1/8/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أحمر",
            },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("أسبوع", action.ReminderDuration);
        Assert.Equal("أحمر", action.ReminderColor);

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Single(list);
        Assert.Equal("أسبوع", list[0].ReminderDuration);
        Assert.Equal("أحمر", list[0].ReminderColor);
    }

    [Fact]
    public async Task AddAction_InvalidReminderDuration_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest
                {
                    Text = "إجراء",
                    ActionDate = "1/8/2026",
                    ReminderDuration = "سنة",
                    ReminderColor = "أحمر",
                },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_InvalidReminderColor_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest
                {
                    Text = "إجراء",
                    ActionDate = "1/8/2026",
                    ReminderDuration = "أسبوع",
                    ReminderColor = "أخضر",
                },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_PartialReminderWithoutColor_SavesDurationOnly()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "إجراء مع مدة فقط",
                ActionDate = "1/8/2026",
                ReminderDuration = "3 أيام",
            },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("3 أيام", action.ReminderDuration);
        Assert.Null(action.ReminderColor);
    }

    [Fact]
    public async Task UpdateAction_ChangesReminder()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Text = "إجراء",
                ActionDate = "1/1/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أصفر",
            },
            userId: 1, actorName: "lawyer1");

        var updated = await _service.UpdateExecutionActionAsync(doc.Id, action.Id,
            new UpdateExecutionActionRequest
            {
                Type = "action",
                Text = "إجراء محدث",
                ActionDate = "2/2/2026",
                ReminderDuration = "شهر",
                ReminderColor = "بنفسجي",
            }, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("شهر", updated!.ReminderDuration);
        Assert.Equal("بنفسجي", updated.ReminderColor);
    }

    [Fact]
    public async Task AddAction_EmptyText_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id, new AddExecutionActionRequest { Text = "  " },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task GetActions_OrdersNewestFirst()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "أول", ActionDate = "1/1/2026" }, 1, "lawyer1");
        await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Text = "ثان", ActionDate = "2/2/2026" }, 1, "lawyer1");

        var list = await _service.GetExecutionActionsAsync(doc.Id);
        Assert.Equal(2, list.Count);
        Assert.Equal("ثان", list[0].Text);
        Assert.Equal("أول", list[1].Text);
    }

    [Fact]
    public async Task AddAction_MissingDocument_ThrowsKeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.AddExecutionActionAsync(9999,
                new AddExecutionActionRequest { Text = "إجراء" }, userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddNote_WithoutDate_SetsTodayDate()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var note = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة بلا تاريخ" },
            userId: 1, actorName: "lawyer1");

        Assert.Equal("note", note.Type);
        Assert.Equal(DateTime.Today.ToString("yyyy-MM-dd"), note.ActionDate);
    }

    [Fact]
    public async Task AddAction_WithoutDate_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest { Type = "action", Text = "إجراء بلا تاريخ" },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task AddAction_InvalidType_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddExecutionActionAsync(doc.Id,
                new AddExecutionActionRequest { Type = "other", Text = "نص" },
                userId: 1, actorName: "lawyer1"));
    }

    [Fact]
    public async Task UpdateAction_ChangesTextAndType()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء قديم", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var updated = await _service.UpdateExecutionActionAsync(doc.Id, action.Id,
            new UpdateExecutionActionRequest { Type = "note", Text = "ملاحظة محدثة" }, "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("note", updated!.Type);
        Assert.Equal("ملاحظة محدثة", updated.Text);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task UpdateAction_WrongDocument_ThrowsKeyNotFound()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UpdateExecutionActionAsync(doc.Id + 9999, action.Id,
                new UpdateExecutionActionRequest { Type = "note", Text = "x" }, "lawyer1"));
    }

    [Fact]
    public async Task UpdateAction_WithoutDate_WhenAction_Throws()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "note", Text = "ملاحظة" },
            userId: 1, actorName: "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateExecutionActionAsync(doc.Id, action.Id,
                new UpdateExecutionActionRequest { Type = "action", Text = "تحويل إلى إجراء بلا تاريخ" }, "lawyer1"));
    }

    [Fact]
    public async Task DeleteAction_RemovesAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var deleted = await _service.DeleteExecutionActionAsync(doc.Id, action.Id, "lawyer1");

        Assert.True(deleted);
        Assert.Empty(await _service.GetExecutionActionsAsync(doc.Id));
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task DeleteAction_WrongDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var deleted = await _service.DeleteExecutionActionAsync(doc.Id + 9999, action.Id, "lawyer1");
        Assert.False(deleted);
    }

    [Fact]
    public async Task ClearReminder_ClearsReminderFieldsKeepsAction()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest
            {
                Type = "action",
                Text = "إجراء بموعد",
                ActionDate = "1/8/2026",
                ReminderDuration = "أسبوع",
                ReminderColor = "أحمر",
            },
            userId: 1, actorName: "lawyer1");

        var cleared = await _service.ClearReminderAsync(doc.Id, action.Id, "lawyer1");

        Assert.True(cleared);
        var list = await _service.GetExecutionActionsAsync(doc.Id);
        var single = Assert.Single(list);
        Assert.Equal("إجراء بموعد", single.Text);
        Assert.Null(single.ReminderDuration);
        Assert.Null(single.ReminderColor);
        Assert.Contains("action", _audit.Actions);
    }

    [Fact]
    public async Task ClearReminder_WrongDocument_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);
        var action = await _service.AddExecutionActionAsync(doc.Id,
            new AddExecutionActionRequest { Type = "action", Text = "إجراء", ActionDate = "1/1/2026" },
            userId: 1, actorName: "lawyer1");

        var cleared = await _service.ClearReminderAsync(doc.Id + 9999, action.Id, "lawyer1");
        Assert.False(cleared);
    }

    [Fact]
    public async Task ClearReminder_MissingAction_ReturnsFalse()
    {
        var doc = await _service.CreateAsync(Sample(), 1, "lawyer1", 1);

        var cleared = await _service.ClearReminderAsync(doc.Id, 9999, "lawyer1");
        Assert.False(cleared);
    }
}

public class AuthServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly FakeAuditLogger _audit = new();

    public AuthServiceTests()
    {
        _db = TestDb.Create();
        _db.Users.Add(new User
        {
            Username = "lawyer1",
            FullName = "محامي",
            Role = UserRole.Lawyer,
            PasswordHash = new PasswordHasher().Hash("123456"),
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private AuthService CreateService(int maxFailedAttempts = 5, int lockoutMinutes = 15) => new(
        new UserRepository(_db),
        new UnitOfWork(_db),
        new PasswordHasher(),
        new FakeTokenService(),
        new TransactionRunner(_db),
        _audit,
        Microsoft.Extensions.Options.Options.Create(new LockoutOptions
        {
            MaxFailedAttempts = maxFailedAttempts,
            LockoutMinutes = lockoutMinutes,
        }));

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUser()
    {
        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));
        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal("lawyer1", result.Response!.User.Username);
        Assert.Contains("login", _audit.Actions);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsInvalidCredentials()
    {
        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "bad"));
        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Null(result.Response);
        Assert.Contains("login_failed", _audit.Actions);
    }

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_LocksAccount()
    {
        var service = CreateService(maxFailedAttempts: 3);

        for (var i = 0; i < 3; i++)
        {
            var failed = await service.LoginAsync(new LoginRequest("lawyer1", "bad"));
            Assert.Equal(LoginStatus.InvalidCredentials, failed.Status);
        }

        Assert.Contains("login_locked", _audit.Actions);

        var locked = await service.LoginAsync(new LoginRequest("lawyer1", "123456"));
        Assert.Equal(LoginStatus.LockedOut, locked.Status);

        var user = _db.Users.First();
        Assert.NotNull(user.LockoutEndUtc);
        Assert.True(user.LockoutEndUtc > DateTime.UtcNow);
        Assert.Equal(0, user.FailedLoginCount);
    }

    [Fact]
    public async Task Login_WhileLockedOut_CorrectPasswordStillReturnsLockedOut()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(10);
        user.FailedLoginCount = 2;
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.LockedOut, result.Status);
        Assert.Null(result.Response);
        Assert.Equal(2, _db.Users.First().FailedLoginCount);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_CorrectPasswordSucceeds()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        user.FailedLoginCount = 3;
        _db.SaveChanges();

        var result = await CreateService().LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(0, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_WrongPassword_StartsFreshCounter()
    {
        var user = _db.Users.First();
        user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(-1);
        user.FailedLoginCount = 3;
        _db.SaveChanges();

        var service = CreateService(maxFailedAttempts: 3);

        var result = await service.LoginAsync(new LoginRequest("lawyer1", "bad"));

        Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        Assert.Equal(1, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task Login_Success_ResetsFailedCount()
    {
        var service = CreateService();
        for (var i = 0; i < 2; i++)
            await service.LoginAsync(new LoginRequest("lawyer1", "bad"));

        Assert.Equal(2, _db.Users.First().FailedLoginCount);

        var result = await service.LoginAsync(new LoginRequest("lawyer1", "123456"));

        Assert.Equal(LoginStatus.Success, result.Status);
        Assert.Equal(0, _db.Users.First().FailedLoginCount);
        Assert.Null(_db.Users.First().LockoutEndUtc);
    }

    [Fact]
    public async Task ChangePassword_WithCorrectOldPassword_Succeeds()
    {
        var service = CreateService();
        var user = _db.Users.First();
        var ok = await service.ChangePasswordAsync(user.Id, "123456", "newpass");
        Assert.True(ok);

        var login = await service.LoginAsync(new LoginRequest("lawyer1", "newpass"));
        Assert.Equal(LoginStatus.Success, login.Status);
        Assert.NotNull(login.Response);
    }

    [Fact]
    public async Task ChangePassword_BumpsTokenVersion()
    {
        var service = CreateService();
        var user = _db.Users.First();
        var initial = user.TokenVersion;

        var ok = await service.ChangePasswordAsync(user.Id, "123456", "newpass");

        Assert.True(ok);
        Assert.Equal(initial + 1, _db.Users.First().TokenVersion);
    }

    [Fact]
    public async Task ChangePassword_WithShortPassword_Throws()
    {
        var service = CreateService();
        var user = _db.Users.First();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ChangePasswordAsync(user.Id, "123456", "123"));
    }
}

public class FakeTokenService : ITokenService
{
    public string CreateToken(User user) => "fake-token";
}
