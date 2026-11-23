using DocGenerator.Application.Common.Pipeline;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Application.UseCases.Suggestions;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

using DocGenerator.Application.Tests.TestHelpers;

namespace DocGenerator.Application.Tests;

public class AppSuggestionServiceTests : PostgresDbTest
{
    private readonly IAppSuggestionService _service;
    private readonly ICommandHandler<CreateAppSuggestionCommand, AppSuggestionDto> _createSuggestion;
    private readonly ICommandHandler<MarkAppSuggestionReadCommand, bool> _markRead;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer;
    private readonly int _damascusId;

    public AppSuggestionServiceTests()
    {
        // الفرع لازم للمحامين (قيد القاعدة CK_Users_BranchRequiredForBranchRoles) —
        // الاقتراحات user-scoped فقيمة الفرع لا تؤثر في أي تأكيد هنا.
        _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM", Governorate = "دمشق" });
        _db.SaveChanges();
        _damascusId = _db.Branches.Single(b => b.Code == "DAM").Id;
        _lawyer = new User
        {
            Username = "sugg_law",
            FullName = "محامي مقترح",
            Role = UserRole.Lawyer,
            BranchId = _damascusId,
            PasswordHash = "x",
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();

        _service = new AppSuggestionService(
            new AppSuggestionRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
        _markRead = new MarkAppSuggestionReadCommandHandler(
            new AppSuggestionRepository(_db),
            new UnitOfWork(_db),
            _audit,
            new IPipelineBehavior<MarkAppSuggestionReadCommand, bool>[]
            {
                new TransactionBehavior<MarkAppSuggestionReadCommand, bool>(new TransactionRunner(_db)),
            });
        _createSuggestion = new CreateAppSuggestionCommandHandler(
            new AppSuggestionRepository(_db),
            new UnitOfWork(_db),
            _audit,
            new IPipelineBehavior<CreateAppSuggestionCommand, AppSuggestionDto>[]
            {
                new TransactionBehavior<CreateAppSuggestionCommand, AppSuggestionDto>(new TransactionRunner(_db)),
            });
    }

    private Task<AppSuggestionDto> CreateSuggestionAsync(
        string message, int senderId, string? actorName = null)
        => _createSuggestion.HandleAsync(new CreateAppSuggestionCommand(message, senderId, actorName));


    [PostgresFact]
    public async Task Create_Valid_ReturnsDtoAndAudits()
    {
        var dto = await CreateSuggestionAsync("أضيفوا تصدير إكسل", _lawyer.Id, "lawyer");

        Assert.Equal("أضيفوا تصدير إكسل", dto.Message);
        Assert.False(dto.IsRead);
        Assert.Null(dto.SenderName);
        Assert.Contains("app-suggestion.create", _audit.Actions);
    }

    [PostgresTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyMessage_Throws(string message)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSuggestionAsync(message, _lawyer.Id, "lawyer"));
    }

    [PostgresFact]
    public async Task Create_TooLong_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => CreateSuggestionAsync(new string('ع', AppSuggestionService.MessageMaxLength + 1), _lawyer.Id, "lawyer"));
    }

    [PostgresFact]
    public async Task ListMine_OnlyOwnNewestFirst()
    {
        await CreateSuggestionAsync("الأول", _lawyer.Id, "lawyer");
        await CreateSuggestionAsync("الثاني", _lawyer.Id, "lawyer");
        var other = new User { Username = "sugg_other", FullName = "آخر", Role = UserRole.Lawyer, BranchId = _damascusId, PasswordHash = "x" };
        _db.Users.Add(other);
        await _db.SaveChangesAsync();
        await CreateSuggestionAsync("للآخر", other.Id, "other");

        var mine = await _service.ListMineAsync(_lawyer.Id);
        Assert.Equal(2, mine.Count);
        Assert.Equal("الثاني", mine[0].Message);
    }

    [PostgresFact]
    public async Task MarkRead_IdempotentAndNotFound()
    {
        Assert.False(await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(999, null)));
        var dto = await CreateSuggestionAsync("اقتراح", _lawyer.Id, "lawyer");

        Assert.True(await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(dto.Id, null)));
        Assert.True(await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(dto.Id, null)));

        var admin = await _service.ListForAdminAsync(1, 20);
        Assert.Contains(admin.Items, s => s.Id == dto.Id && s.IsRead && s.SenderName == "محامي مقترح");
    }

    [PostgresFact]
    public async Task CountUnread_TracksUnreadOnly()
    {
        Assert.Equal(0, await _service.CountUnreadAsync());

        var first = await CreateSuggestionAsync("أول اقتراح", _lawyer.Id, "lawyer");
        await CreateSuggestionAsync("ثانٍ اقتراح", _lawyer.Id, "lawyer");
        Assert.Equal(2, await _service.CountUnreadAsync());

        Assert.True(await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(first.Id, null)));
        Assert.Equal(1, await _service.CountUnreadAsync());
        // التعليم المتكرر لا ينقص العدّاد مرتين.
        Assert.True(await _markRead.HandleAsync(new MarkAppSuggestionReadCommand(first.Id, null)));
        Assert.Equal(1, await _service.CountUnreadAsync());
    }

    [PostgresFact]
    public async Task ListForAdmin_PagesAndClamps()
    {
        await CreateSuggestionAsync("الأول", _lawyer.Id, "lawyer");
        await CreateSuggestionAsync("الثاني", _lawyer.Id, "lawyer");
        await CreateSuggestionAsync("الثالث", _lawyer.Id, "lawyer");

        var first = await _service.ListForAdminAsync(1, 2);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, first.Items.Count);
        Assert.Equal("الثالث", first.Items[0].Message);

        var second = await _service.ListForAdminAsync(2, 2);
        Assert.Single(second.Items);
        Assert.Equal("الأول", second.Items[0].Message);

        // تقويم الحدود: صفحة دون 1 تُرفع، وحجم فوق الأقصى يُخفَّض للافتراضي.
        var clamped = await _service.ListForAdminAsync(0, 999);
        Assert.Equal(1, clamped.Page);
        Assert.Equal(AppSuggestionService.DefaultPerPage, clamped.PerPage);
    }

    [PostgresFact]
    public async Task GetById_OwnerAdminAndMissingViews()
    {
        var other = new User
        {
            Username = "sugg_law2",
            FullName = "محامي ثانٍ",
            Role = UserRole.Lawyer,
            BranchId = _damascusId,
            PasswordHash = "x",
        };
        _db.Users.Add(other);
        await _db.SaveChangesAsync();

        var dto = await CreateSuggestionAsync("اقتراح للقراءة", _lawyer.Id, "lawyer");

        var mine = await _service.GetByIdAsync(dto.Id, _lawyer.Id);
        Assert.NotNull(mine);
        Assert.Equal(dto.Id, mine.Id);
        Assert.Null(mine.SenderName);

        Assert.Null(await _service.GetByIdAsync(dto.Id, other.Id));
        Assert.Null(await _service.GetByIdAsync(999999, _lawyer.Id));

        var adminView = await _service.GetByIdAsync(dto.Id, null);
        Assert.NotNull(adminView);
        Assert.Equal("محامي مقترح", adminView.SenderName);
        Assert.Null(await _service.GetByIdAsync(999999, null));
    }
}
