using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using DocGenerator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Application.Tests;

/// <summary>
/// اختبارات RF-013 (INT-011 + ARC-007): لا إزاحة منطقة لتذكيرات المحامي.
/// تفشل الإظهار قبل الإصلاح (انحراف `datetime2` + `Unspecified`) وتخضر بعده.
/// بناء نموذج `Postgres` بلا اتصال (سلسلة وهمية — لا خادم).
/// </summary>
public class RF013TimestampTests : IDisposable
{
    private readonly DocGeneratorDbContext _db;
    private readonly IPersonalReminderService _service;
    private readonly FakeAuditLogger _audit = new();
    private readonly User _lawyer;

    public RF013TimestampTests()
    {
        _db = TestDb.Create();
        var damascus = _db.Branches.Add(new Branch { Name = "دمشق", Code = "DAM" }).Entity;
        _db.SaveChanges();
        _lawyer = new User
        {
            Username = "ts_lawyer",
            FullName = "محامي الطوابع",
            Role = UserRole.Lawyer,
            BranchId = damascus.Id,
            PasswordHash = "x",
        };
        _db.Users.Add(_lawyer);
        _db.SaveChanges();

        _service = new PersonalReminderService(
            new PersonalReminderRepository(_db),
            new UnitOfWork(_db),
            new TransactionRunner(_db),
            _audit);
    }

    public void Dispose() => _db.Dispose();

    private static DocGeneratorPostgresDbContext PostgresModel()
    {
        var options = new DbContextOptionsBuilder<DocGeneratorPostgresDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=docgen_dummy;Username=dummy;Password=dummy")
            .Options;
        return new DocGeneratorPostgresDbContext(options);
    }

    [Fact]
    public void PostgresModel_MapsReminderDatesToTimestamptz()
    {
        // اليوم: `datetime2` (انحراف INT-011)؛ بعد RF-013: `timestamptz` كالبقية.
        using var pg = PostgresModel();
        var entity = pg.Model.FindEntityType(typeof(PersonalReminder))!;

        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(PersonalReminder.DueDate))!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty(nameof(PersonalReminder.RecurrenceEnd))!.GetColumnType());
    }

    [Fact]
    public void ParseDay_ReturnsUtcKind()
    {
        // اليوم: `Unspecified` (مرفوض/مُزاح على `Postgres`)؛ بعد RF-013: `Utc`.
        Assert.Equal(DateTimeKind.Utc, PersonalReminderDates.ParseDay("1/1/2026", "تاريخ الاستحقاق").Kind);
    }

    [Fact]
    public async Task ReminderDay_RoundTrips_OnNewYearMidnight()
    {
        // توصيف (يخضر قبل/بعد): يوم رأس السنة الحرج يُحفَظ ويُقرأ نفسه على `SQLite`.
        var dto = await _service.CreateAsync(
            new CreatePersonalReminderRequest("رأس السنة", null, "1/1/2026", null, PersonalReminderCatalog.RecurrenceOnce, "1/1/2026"),
            _lawyer.Id, "tester");

        Assert.Equal("2026-01-01", dto.DueDate);
        Assert.Equal("2026-01-01", dto.RecurrenceEnd);

        var reread = await _service.GetByIdAsync(dto.Id, _lawyer.Id);
        Assert.Equal("2026-01-01", reread!.DueDate);
        Assert.Equal("2026-01-01", reread.RecurrenceEnd);
    }

    [Fact]
    public void Format_OfParsedDay_IsStable()
    {
        // توصيف (يخضر قبل/بعد): العقد `yyyy-MM-dd` ثابت لا يتأثر بالـ`Kind`.
        Assert.Equal("2026-01-01", PersonalReminderDates.Format(PersonalReminderDates.ParseDay("1/1/2026", "تاريخ الاستحقاق")));
    }
}
