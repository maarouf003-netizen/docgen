using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class PersonalReminderServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPersonalReminderService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer1;
    private readonly User _lawyer2;

    public PersonalReminderServiceTests()
    {
        _db = TestDb.Create();
        // الفرع لازم للمحامي (قيد القاعدة) — التذكيرات user-scoped فالقيمة محايدة.
        var damascus = _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" }).Entity;
        _db.SaveChanges();
        _lawyer1 = new User
        {
            Username = "prem_law1",
            FullName = "محامي أول",
            Role = UserRole.Lawyer,
            BranchId = damascus.Id,
            PasswordHash = "x",
        };
        _lawyer2 = new User
        {
            Username = "prem_law2",
            FullName = "محامي ثان",
            Role = UserRole.Lawyer,
            BranchId = damascus.Id,
            PasswordHash = "x",
        };
        _db.Users.AddRange(_lawyer1, _lawyer2);
        _db.SaveChanges();

        _service = new PersonalReminderService(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private static CreatePersonalReminderRequest ValidRequest(
        string title = "مراجعة الملف",
        string dueDate = "2026-08-08",
        string recurrence = PersonalReminderCatalog.RecurrenceOnce,
        string? recurrenceEnd = null,
        string? color = PersonalReminderCatalog.ColorRed)
        => new(title, "ملاحظة", dueDate, color, recurrence, recurrenceEnd);

    [Fact]
    public async Task Create_Valid_ReturnsDtoWithIsoDates()
    {
        var dto = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        Assert.Equal("مراجعة الملف", dto.Title);
        Assert.Equal("2026-08-08", dto.DueDate);
        Assert.Equal(PersonalReminderCatalog.RecurrenceOnce, dto.Recurrence);
        Assert.False(dto.IsArchived);
        Assert.Empty(dto.CompletedOccurrenceKeys);
        Assert.Contains("personal-reminder.create", _audit.Actions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyTitle_Throws(string title)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(ValidRequest(title: title), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Create_TitleTooLong_Throws()
    {
        var request = ValidRequest(title: new string('ع', PersonalReminderCatalog.TitleMaxLength + 1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, _lawyer1.Id, "lawyer1"));
    }

    [Theory]
    [InlineData("أخضر")]
    [InlineData("red")]
    public async Task Create_InvalidColor_Throws(string color)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(ValidRequest(color: color), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Create_InvalidRecurrence_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(ValidRequest(recurrence: "سنوي"), _lawyer1.Id, "lawyer1"));
    }

    [Theory]
    [InlineData("ليس تاريخًا")]
    [InlineData("32/13/2026")]
    [InlineData("")]
    public async Task Create_InvalidDueDate_Throws(string dueDate)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(ValidRequest(dueDate: dueDate), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Create_AcceptsSystemDateFormats()
    {
        // نفس عقد تواريخ النظام: `d/M/yyyy` والأرقام العربية مقبولة وتُطبَّع `yyyy-MM-dd`.
        var dto = await _service.CreateAsync(ValidRequest(dueDate: "8/8/2026"), _lawyer1.Id, "lawyer1");
        Assert.Equal("2026-08-08", dto.DueDate);
    }

    [Fact]
    public async Task Create_RecurrenceEndBeforeDueDate_Throws()
    {
        var request = ValidRequest(
            recurrence: PersonalReminderCatalog.RecurrenceDaily,
            recurrenceEnd: "2026-08-07");
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(request, _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task Update_DueDateBeyondRecurrenceEnd_Throws()
    {
        var dto = await _service.CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily, recurrenceEnd: "2026-08-10"),
            _lawyer1.Id,
            "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                dto.Id,
                new UpdatePersonalReminderRequest(null, null, "2026-08-15", null, null, null, null),
                _lawyer1.Id,
                "lawyer1"));
    }

    [Fact]
    public async Task Create_BeyondCap_Throws()
    {
        for (var i = 0; i < PersonalReminderCatalog.MaxActivePerUser; i++)
            await _service.CreateAsync(ValidRequest(title: $"تذكير {i}"), _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(ValidRequest(title: "زائد"), _lawyer1.Id, "lawyer1"));
    }

    [Fact]
    public async Task GetById_OwnerReturnsNonOwnerNull()
    {
        var created = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var found = await _service.GetByIdAsync(created.Id, _lawyer1.Id);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("مراجعة الملف", found.Title);

        Assert.Null(await _service.GetByIdAsync(created.Id, _lawyer2.Id));
        Assert.Null(await _service.GetByIdAsync(999999, _lawyer1.Id));
    }

    [Fact]
    public async Task Update_NonOwner_ReturnsNull()
    {
        var dto = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var result = await _service.UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest("معدل", null, null, null, null, null, null),
            _lawyer2.Id,
            "lawyer2");

        Assert.Null(result);
    }

    [Fact]
    public async Task Update_Owner_AppliesFieldsAndArchive()
    {
        var dto = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var updated = await _service.UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest("معدل", null, "2026-09-01", null, null, null, true),
            _lawyer1.Id,
            "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal("معدل", updated.Title);
        Assert.Equal("2026-09-01", updated.DueDate);
        Assert.True(updated.IsArchived);
        Assert.Contains("personal-reminder.update", _audit.Actions);
    }

    [Fact]
    public async Task List_ExcludesArchivedByDefault()
    {
        var dto = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");
        await _service.UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest(null, null, null, null, null, null, true),
            _lawyer1.Id,
            "lawyer1");

        Assert.Empty(await _service.ListAsync(_lawyer1.Id, includeArchived: false));
        Assert.Single(await _service.ListAsync(_lawyer1.Id, includeArchived: true));
    }

    [Fact]
    public async Task SetOccurrence_TogglesDoneAndRejectsBadKey()
    {
        var dto = await _service.CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");

        var done = await _service.SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer1.Id);
        Assert.NotNull(done);
        Assert.Contains("2026-08-09", done.CompletedOccurrenceKeys);

        var undone = await _service.SetOccurrenceAsync(dto.Id, "2026-08-09", false, _lawyer1.Id);
        Assert.NotNull(undone);
        Assert.Empty(undone.CompletedOccurrenceKeys);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetOccurrenceAsync(dto.Id, "ليس تاريخًا", true, _lawyer1.Id));

        Assert.Null(await _service.SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer2.Id));
    }

    [Fact]
    public async Task SetOccurrence_Archived_Throws()
    {
        var dto = await _service.CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");
        await _service.UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest(null, null, null, null, null, null, true),
            _lawyer1.Id,
            "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer1.Id));
    }

    [Fact]
    public async Task Delete_OwnerOnly()
    {
        var dto = await _service.CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        Assert.False(await _service.DeleteAsync(dto.Id, _lawyer2.Id, "lawyer2"));
        Assert.True(await _service.DeleteAsync(dto.Id, _lawyer1.Id, "lawyer1"));
        Assert.Empty(await _service.ListAsync(_lawyer1.Id, includeArchived: true));
        Assert.Contains("personal-reminder.delete", _audit.Actions);
    }
}
