using DocGenerator.Application.Common;
using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Tests;

/// <summary>
/// سياسة رقم الأساس الاستئنافي كوحدة مستقلة: انتخاب الهوية الحالية، وبناء الأرشيف،
/// وحدود السنة. تغطي هذه الفئة القواعد التي كانت موزّعة على الخدمة قبل توحيدها.
/// </summary>
public class AppealBaseNumberPolicyTests
{
    private static AppealBaseNumber Row(int year, string number, DateTime createdAt)
        => new()
        {
            AppealId = 1,
            Year = year,
            BaseNumber = number,
            CreatedAt = createdAt,
            CreatedById = 1,
        };

    private static DocumentAppeal Appeal(string? baseNumber = null, string? year = null)
        => new() { AppealBaseNumber = baseNumber, AppealYear = year };

    [Fact]
    public void ResolveCurrent_PicksNewestYear_AndNewestCreatedAtWithinYear()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));
        appeal.BaseNumbers.Add(Row(2026, "1500", new DateTime(2026, 1, 5)));
        appeal.BaseNumbers.Add(Row(2026, "1501", new DateTime(2026, 1, 10)));

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.NotNull(identity);
        Assert.Equal(2026, identity!.Year);
        Assert.Equal("1501", identity.Number);
        Assert.NotNull(identity.Row);
    }

    [Fact]
    public void ResolveCurrent_NewerYearWinsOverNewerCreatedAt()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2024, "700", new DateTime(2026, 7, 1)));
        appeal.BaseNumbers.Add(Row(2025, "800", new DateTime(2026, 1, 1)));

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.Equal(2025, identity!.Year);
        Assert.Equal("800", identity.Number);
    }

    [Fact]
    public void ResolveCurrent_IgnoresFutureRow()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));
        appeal.BaseNumbers.Add(Row(2027, "2000", new DateTime(2026, 1, 1)));

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.Equal(2025, identity!.Year);
        Assert.Equal("900", identity.Number);
    }

    [Fact]
    public void ResolveCurrent_FutureRegistrationYearIsIgnored()
    {
        // السنة المستقبلية ممنوعة إدخالًا؛ وبيانات قديمة مخالفة لا تُعرض رقمًا حاليًا.
        var appeal = Appeal("2000", "2027");

        Assert.Null(AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026));
    }

    [Fact]
    public void ResolveCurrent_LegacyNonNumericYear_FallsBackWithoutYear()
    {
        var appeal = Appeal("777", "عام غير محدد");

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.NotNull(identity);
        Assert.Null(identity!.Year);
        Assert.Equal("777", identity.Number);
        Assert.Null(identity.Row);
    }

    [Fact]
    public void ResolveCurrent_NumberWithoutYear_IsDisplayedButNotCurrent()
    {
        var appeal = Appeal("700", null);

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.NotNull(identity);
        Assert.Equal("700", identity!.Number);
        Assert.Null(identity.Year);
    }

    [Fact]
    public void ResolveCurrent_RowBeatsRegistrationOnSameYear()
    {
        var appeal = Appeal("900", "2026");
        appeal.BaseNumbers.Add(Row(2026, "1500", new DateTime(2026, 1, 1)));

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.Equal("1500", identity!.Number);
        Assert.NotNull(identity.Row);
    }

    [Fact]
    public void ResolveCurrent_IgnoresEmptyRows()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2026, "   ", new DateTime(2026, 1, 1)));
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));

        var identity = AppealBaseNumberPolicy.ResolveCurrent(appeal, 2026);

        Assert.Equal(2025, identity!.Year);
    }

    [Fact]
    public void BuildHistory_OneEntryPerYear_MarksCurrentYear()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));
        appeal.BaseNumbers.Add(Row(2026, "1500", new DateTime(2026, 1, 5)));
        appeal.BaseNumbers.Add(Row(2026, "1501", new DateTime(2026, 1, 10)));

        var history = AppealBaseNumberPolicy.BuildHistory(appeal, 2026);

        Assert.Equal(2, history.Count);
        Assert.Equal("900", history[0].Number);
        Assert.False(history[0].IsCurrent);
        Assert.Equal("1501", history[1].Number);
        Assert.True(history[1].IsCurrent);
    }

    [Fact]
    public void BuildHistory_ExcludesFutureAndNonPositiveYears()
    {
        var appeal = Appeal("500", "2027");
        appeal.BaseNumbers.Add(Row(2027, "2000", new DateTime(2026, 1, 1)));
        appeal.BaseNumbers.Add(Row(0, "1", new DateTime(2026, 1, 1)));

        Assert.Empty(AppealBaseNumberPolicy.BuildHistory(appeal, 2026));
    }

    [Fact]
    public void BuildHistory_AddsRegistrationWhenNoRowForItsYear()
    {
        var appeal = Appeal("500", "2026");
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));

        var history = AppealBaseNumberPolicy.BuildHistory(appeal, 2026);

        Assert.Equal(2, history.Count);
        Assert.Equal("500", history[1].Number);
        Assert.True(history[1].IsCurrent);
    }

    [Fact]
    public void BuildHistory_LegacyNonNumericYear_HasNoSyntheticEntry()
    {
        var appeal = Appeal("777", "عام غير محدد");

        Assert.Empty(AppealBaseNumberPolicy.BuildHistory(appeal, 2026));
    }

    [Theory]
    [InlineData("2026", 2026)]
    [InlineData("٢٠٢٦", 2026)]
    [InlineData("  2025  ", 2025)]
    public void ValidateYear_AcceptsValidInput(string raw, int expected)
        => Assert.Equal(expected, AppealBaseNumberPolicy.ValidateYear(raw, 2026));

    [Theory]
    [InlineData("2027")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("عام")]
    public void ValidateYear_RejectsInvalidInput(string raw)
        => Assert.Throws<ArgumentException>(
            () => AppealBaseNumberPolicy.ValidateYear(raw, 2026));

    [Fact]
    public void ValidateYear_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(AppealBaseNumberPolicy.ValidateYear("", 2026));
        Assert.Null(AppealBaseNumberPolicy.ValidateYear("   ", 2026));
        Assert.Null(AppealBaseNumberPolicy.ValidateYear(null, 2026));
    }

    [Fact]
    public void TryParseYear_ToleratesStoredGarbage()
    {
        Assert.Null(AppealBaseNumberPolicy.TryParseYear("غير رقمي"));
        Assert.Equal(2026, AppealBaseNumberPolicy.TryParseYear("٢٠٢٦"));
    }

    [Fact]
    public void HasIdentityForYear_ChecksRowsAndRegistration()
    {
        var appeal = Appeal("500", "2026");
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));

        Assert.True(AppealBaseNumberPolicy.HasIdentityForYear(appeal, 2025));
        Assert.True(AppealBaseNumberPolicy.HasIdentityForYear(appeal, 2026));
        Assert.False(AppealBaseNumberPolicy.HasIdentityForYear(appeal, 2024));
    }

    [Fact]
    public void LatestRowForYear_PicksNewestCreatedAt()
    {
        var appeal = Appeal();
        appeal.BaseNumbers.Add(Row(2025, "900", new DateTime(2025, 1, 1)));
        appeal.BaseNumbers.Add(Row(2025, "901", new DateTime(2025, 2, 1)));

        Assert.Equal("901", AppealBaseNumberPolicy.LatestRowForYear(appeal, 2025)!.BaseNumber);
        Assert.Null(AppealBaseNumberPolicy.LatestRowForYear(appeal, 2024));
    }
}
