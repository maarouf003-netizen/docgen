using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.UseCases.Reminders;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

public class PersonalReminderServiceTests : PostgresDbTest
{
    private readonly PersonalReminderReadHandlers _reads;
    private readonly ICommandHandler<DeletePersonalReminderCommand, bool> _deleteReminder;
    private readonly ICommandHandler<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?> _setOccurrence;
    private readonly ICommandHandler<CreatePersonalReminderCommand, PersonalReminderDto> _createReminder;
    private readonly ICommandHandler<UpdatePersonalReminderCommand, PersonalReminderDto?> _updateReminder;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer1;
    private readonly User _lawyer2;

    public PersonalReminderServiceTests()
    {
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

        _reads = PersonalReminderReadHandlerFactory.Create(_db);
        _deleteReminder = new DeletePersonalReminderCommandHandler(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            _audit,
            new IPipelineBehavior<DeletePersonalReminderCommand, bool>[]
            {
                new TransactionBehavior<DeletePersonalReminderCommand, bool>(new TransactionRunner(_db)),
            });
        _setOccurrence = new SetPersonalReminderOccurrenceCommandHandler(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            new IPipelineBehavior<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?>[]
            {
                new TransactionBehavior<SetPersonalReminderOccurrenceCommand, PersonalReminderDto?>(
                    new TransactionRunner(_db)),
            });
        _createReminder = new CreatePersonalReminderCommandHandler(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            _audit,
            new IPipelineBehavior<CreatePersonalReminderCommand, PersonalReminderDto>[]
            {
                new TransactionBehavior<CreatePersonalReminderCommand, PersonalReminderDto>(
                    new TransactionRunner(_db)),
            });
        _updateReminder = new UpdatePersonalReminderCommandHandler(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            _audit,
            new IPipelineBehavior<UpdatePersonalReminderCommand, PersonalReminderDto?>[]
            {
                new TransactionBehavior<UpdatePersonalReminderCommand, PersonalReminderDto?>(
                    new TransactionRunner(_db)),
            });
    }

    private Task<PersonalReminderDto> CreateAsync(
        CreatePersonalReminderRequest request, int userId, string? actorName)
        => _createReminder.HandleAsync(new CreatePersonalReminderCommand(request, userId, actorName));

    private Task<PersonalReminderDto?> UpdateAsync(
        int id, UpdatePersonalReminderRequest request, int userId, string? actorName)
        => _updateReminder.HandleAsync(
            new UpdatePersonalReminderCommand(id, request, userId, actorName));

    private Task<PersonalReminderDto?> SetOccurrenceAsync(
        int id, string occurrenceDate, bool done, int userId, long? version = null)
        => _setOccurrence.HandleAsync(new SetPersonalReminderOccurrenceCommand(
            id, occurrenceDate, done, userId, version));


    private static CreatePersonalReminderRequest ValidRequest(
        string title = "مراجعة الملف",
        string dueDate = "2026-08-08",
        string recurrence = PersonalReminderCatalog.RecurrenceOnce,
        string? recurrenceEnd = null,
        string? color = PersonalReminderCatalog.ColorRed)
        => new(title, "ملاحظة", dueDate, color, recurrence, recurrenceEnd);

    [PostgresFact]
    public async Task Create_Valid_ReturnsDtoWithIsoDates()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        Assert.Equal("مراجعة الملف", dto.Title);
        Assert.Equal("2026-08-08", dto.DueDate);
        Assert.Equal(PersonalReminderCatalog.RecurrenceOnce, dto.Recurrence);
        Assert.False(dto.IsArchived);
        Assert.Empty(dto.CompletedOccurrenceKeys);
        Assert.Contains("personal-reminder.create", _audit.Actions);
        Assert.Contains(_audit.Details, d => d == "أنشأ تذكيرًا شخصيًا: مراجعة الملف");
    }

    [PostgresTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyTitle_Throws(string title)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(title: title), _lawyer1.Id, "lawyer1"));
    }

    [PostgresFact]
    public async Task Create_TitleTooLong_Throws()
    {
        var request = ValidRequest(title: new string('ع', PersonalReminderCatalog.TitleMaxLength + 1));
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(request, _lawyer1.Id, "lawyer1"));
    }

    [PostgresTheory]
    [InlineData("أخضر")]
    [InlineData("red")]
    public async Task Create_InvalidColor_Throws(string color)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(color: color), _lawyer1.Id, "lawyer1"));
    }

    [PostgresFact]
    public async Task Create_InvalidRecurrence_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(recurrence: "سنوي"), _lawyer1.Id, "lawyer1"));
    }

    [PostgresTheory]
    [InlineData("ليس تاريخًا")]
    [InlineData("32/13/2026")]
    [InlineData("")]
    public async Task Create_InvalidDueDate_Throws(string dueDate)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(dueDate: dueDate), _lawyer1.Id, "lawyer1"));
    }

    [PostgresFact]
    public async Task Create_AcceptsSystemDateFormats()
    {
        // نفس عقد تواريخ النظام: `d/M/yyyy` والأرقام العربية مقبولة وتُطبَّع `yyyy-MM-dd`.
        var dto = await CreateAsync(ValidRequest(dueDate: "8/8/2026"), _lawyer1.Id, "lawyer1");
        Assert.Equal("2026-08-08", dto.DueDate);
    }

    [PostgresFact]
    public async Task Create_RecurrenceEndBeforeDueDate_Throws()
    {
        var request = ValidRequest(
            recurrence: PersonalReminderCatalog.RecurrenceDaily,
            recurrenceEnd: "2026-08-07");
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(request, _lawyer1.Id, "lawyer1"));
    }

    [PostgresFact]
    public async Task Update_DueDateBeyondRecurrenceEnd_Throws()
    {
        var dto = await CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily, recurrenceEnd: "2026-08-10"),
            _lawyer1.Id,
            "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => UpdateAsync(
                dto.Id,
                new UpdatePersonalReminderRequest(null, null, "2026-08-15", null, null, null, null),
                _lawyer1.Id,
                "lawyer1"));
    }

    [PostgresFact]
    public async Task Create_BeyondCap_Throws()
    {
        for (var i = 0; i < PersonalReminderCatalog.MaxActivePerUser; i++)
            await CreateAsync(ValidRequest(title: $"تذكير {i}"), _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(title: "زائد"), _lawyer1.Id, "lawyer1"));
    }

    [PostgresFact]
    public async Task Create_BeyondCap_MessageDirectsToDelete()
    {
        // بلا خيار أرشفة في الواجهة: الرسالة توجه للحذف لا للأرشفة.
        for (var i = 0; i < PersonalReminderCatalog.MaxActivePerUser; i++)
            await CreateAsync(ValidRequest(title: $"تذكير {i}"), _lawyer1.Id, "lawyer1");

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateAsync(ValidRequest(title: "زائد"), _lawyer1.Id, "lawyer1"));
        Assert.Equal(
            $"تجاوزت الحد الأقصى للتذكيرات النشطة ({PersonalReminderCatalog.MaxActivePerUser}) — احذف القديم منها",
            ex.Message);
    }

    [PostgresFact]
    public async Task SetOccurrence_Overflow_MessageDirectsToEndDateOrDelete()
    {
        var dto = await CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");
        // ملء السجل حتى حده مباشرة بمفاتيح مميزة (أسرع من مئات الاستدعاءات) ثم تجاوزه بنداء واحد.
        var stuffed = await _db.PersonalReminders.FindAsync(dto.Id);
        Assert.NotNull(stuffed);
        stuffed!.CompletedOccurrenceKeys = string.Join(',',
            Enumerable.Range(0, PersonalReminderCatalog.CompletedKeysMaxLength / 11)
                .Select(i => new DateOnly(2025, 1, 1).AddDays(i).ToString("yyyy-MM-dd")));
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => SetOccurrenceAsync(dto.Id, "2026-08-08", true, _lawyer1.Id));
        Assert.Equal("تجاوز سجل التكرارات المنجزة حده — حدّد تاريخ انتهاء للتكرار أو احذف التذكير", ex.Message);
    }

    [PostgresFact]
    public async Task GetById_OwnerReturnsNonOwnerNull()
    {
        var created = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var found = await _reads.GetByIdAsync(created.Id, _lawyer1.Id);
        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("مراجعة الملف", found.Title);

        Assert.Null(await _reads.GetByIdAsync(created.Id, _lawyer2.Id));
        Assert.Null(await _reads.GetByIdAsync(999999, _lawyer1.Id));
    }

    [PostgresFact]
    public async Task Update_NonOwner_ReturnsNull()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var result = await UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest("معدل", null, null, null, null, null, null),
            _lawyer2.Id,
            "lawyer2");

        Assert.Null(result);
    }

    [PostgresFact]
    public async Task Update_Owner_AppliesFieldsAndArchive()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var updated = await UpdateAsync(
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

    [PostgresFact]
    public async Task Update_StaleVersion_ThrowsConflict_WithoutChanges()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        var ex = await Assert.ThrowsAsync<DocumentConflictException>(() =>
            UpdateAsync(
                dto.Id,
                new UpdatePersonalReminderRequest("لا يُحفظ", null, null, null, null, null, null, dto.Version + 3),
                _lawyer1.Id,
                "lawyer1"));

        Assert.Contains("التذكير الشخصي", ex.Message);
        Assert.Equal("مراجعة الملف", (await _reads.GetByIdAsync(dto.Id, _lawyer1.Id))!.Title);
        Assert.DoesNotContain("personal-reminder.update", _audit.Actions);
    }

    [PostgresFact]
    public async Task Update_MatchingVersion_IncrementsVersion()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");
        Assert.Equal(0, dto.Version);

        var updated = await UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest("معدل", null, null, null, null, null, null, dto.Version),
            _lawyer1.Id,
            "lawyer1");

        Assert.NotNull(updated);
        Assert.Equal(1, updated.Version);
    }

    [PostgresFact]
    public async Task SetOccurrence_StaleVersion_ThrowsConflict()
    {
        var dto = await CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");

        await Assert.ThrowsAsync<DocumentConflictException>(() =>
            SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer1.Id, dto.Version + 1));

        Assert.Empty((await _reads.GetByIdAsync(dto.Id, _lawyer1.Id))!.CompletedOccurrenceKeys);
    }

    [PostgresFact]
    public async Task List_ExcludesArchivedByDefault()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");
        await UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest(null, null, null, null, null, null, true),
            _lawyer1.Id,
            "lawyer1");

        Assert.Empty(await _reads.ListAsync(_lawyer1.Id, includeArchived: false));
        Assert.Single(await _reads.ListAsync(_lawyer1.Id, includeArchived: true));
    }

    [PostgresFact]
    public async Task SetOccurrence_TogglesDoneAndRejectsBadKey()
    {
        var dto = await CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");

        var done = await SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer1.Id);
        Assert.NotNull(done);
        Assert.Contains("2026-08-09", done.CompletedOccurrenceKeys);

        var undone = await SetOccurrenceAsync(dto.Id, "2026-08-09", false, _lawyer1.Id);
        Assert.NotNull(undone);
        Assert.Empty(undone.CompletedOccurrenceKeys);

        await Assert.ThrowsAsync<ArgumentException>(
            () => SetOccurrenceAsync(dto.Id, "ليس تاريخًا", true, _lawyer1.Id));

        Assert.Null(await SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer2.Id));
    }

    [PostgresFact]
    public async Task SetOccurrence_Archived_Throws()
    {
        var dto = await CreateAsync(
            ValidRequest(recurrence: PersonalReminderCatalog.RecurrenceDaily), _lawyer1.Id, "lawyer1");
        await UpdateAsync(
            dto.Id,
            new UpdatePersonalReminderRequest(null, null, null, null, null, null, true),
            _lawyer1.Id,
            "lawyer1");

        await Assert.ThrowsAsync<ArgumentException>(
            () => SetOccurrenceAsync(dto.Id, "2026-08-09", true, _lawyer1.Id));
    }

    [PostgresFact]
    public async Task Delete_OwnerOnly()
    {
        var dto = await CreateAsync(ValidRequest(), _lawyer1.Id, "lawyer1");

        Assert.False(await _deleteReminder.HandleAsync(new DeletePersonalReminderCommand(dto.Id, _lawyer2.Id, "lawyer2")));
        Assert.True(await _deleteReminder.HandleAsync(new DeletePersonalReminderCommand(dto.Id, _lawyer1.Id, "lawyer1")));
        Assert.Empty(await _reads.ListAsync(_lawyer1.Id, includeArchived: true));
        Assert.Contains("personal-reminder.delete", _audit.Actions);
        Assert.Contains(_audit.Details, d => d is not null && d.StartsWith("حذف تذكيره الشخصي:", StringComparison.Ordinal));
    }
}
