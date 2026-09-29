using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;

namespace DocGenerator.Application.Tests;

public class AppSuggestionServiceTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IAppSuggestionService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer;

    public AppSuggestionServiceTests()
    {
        _db = TestDb.Create();
        _lawyer = new User
        {
            Username = "sugg_law",
            FullName = "محامي مقترح",
            Role = UserRole.Lawyer,
            PasswordHash = "x",
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();

        _service = new AppSuggestionService(
            new AppSuggestionRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_Valid_ReturnsDtoAndAudits()
    {
        var dto = await _service.CreateAsync("أضيفوا تصدير إكسل", _lawyer.Id, "lawyer");

        Assert.Equal("أضيفوا تصدير إكسل", dto.Message);
        Assert.False(dto.IsRead);
        Assert.Null(dto.SenderName);
        Assert.Contains("app-suggestion.create", _audit.Actions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_EmptyMessage_Throws(string message)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(message, _lawyer.Id, "lawyer"));
    }

    [Fact]
    public async Task Create_TooLong_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(new string('ع', AppSuggestionService.MessageMaxLength + 1), _lawyer.Id, "lawyer"));
    }

    [Fact]
    public async Task ListMine_OnlyOwnNewestFirst()
    {
        await _service.CreateAsync("الأول", _lawyer.Id, "lawyer");
        await _service.CreateAsync("الثاني", _lawyer.Id, "lawyer");
        var other = new User { Username = "sugg_other", FullName = "آخر", Role = UserRole.Lawyer, PasswordHash = "x" };
        _db.Users.Add(other);
        await _db.SaveChangesAsync();
        await _service.CreateAsync("للآخر", other.Id, "other");

        var mine = await _service.ListMineAsync(_lawyer.Id);
        Assert.Equal(2, mine.Count);
        Assert.Equal("الثاني", mine[0].Message);
    }

    [Fact]
    public async Task MarkRead_IdempotentAndNotFound()
    {
        Assert.False(await _service.MarkReadAsync(999));
        var dto = await _service.CreateAsync("اقتراح", _lawyer.Id, "lawyer");

        Assert.True(await _service.MarkReadAsync(dto.Id));
        Assert.True(await _service.MarkReadAsync(dto.Id));

        var admin = await _service.ListForAdminAsync(1, 20);
        Assert.Contains(admin.Items, s => s.Id == dto.Id && s.IsRead && s.SenderName == "محامي مقترح");
    }

    [Fact]
    public async Task ListForAdmin_PagesAndClamps()
    {
        await _service.CreateAsync("الأول", _lawyer.Id, "lawyer");
        await _service.CreateAsync("الثاني", _lawyer.Id, "lawyer");
        await _service.CreateAsync("الثالث", _lawyer.Id, "lawyer");

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

    [Fact]
    public async Task GetById_OwnerAdminAndMissingViews()
    {
        var other = new User
        {
            Username = "sugg_law2",
            FullName = "محامي ثانٍ",
            Role = UserRole.Lawyer,
            PasswordHash = "x",
        };
        _db.Users.Add(other);
        await _db.SaveChangesAsync();

        var dto = await _service.CreateAsync("اقتراح للقراءة", _lawyer.Id, "lawyer");

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
