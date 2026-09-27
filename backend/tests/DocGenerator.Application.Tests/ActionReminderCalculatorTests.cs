using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class ActionReminderCalculatorTests
{
    [Fact]
    public void TryComputeDueDate_ValidDateAndDuration_NotSuspect()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate("1/8/2026", "أسبوع", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_EmptyDateAndNullDuration_FallsBackToCreatedAtWithoutSuspect()
    {
        // الغياب قصد تصميمي (تاريخ الإنشاء + المدة) لا تلف — فلا وسم.
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, null, createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 1), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_UnparsableDate_SuspectWithCreatedAtBase()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate("ليس تاريخا", "أسبوع", createdAt);

        Assert.True(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_UnknownDuration_SuspectWithZeroDays()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate("1/8/2026", "سنة", createdAt);

        Assert.True(suspect);
        Assert.Equal(new DateTime(2026, 8, 1), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_PaddedDuration_TrimsConsistentlyForFlagAndDays()
    {
        // انحدار مُكتشف بالمراجعة: مسافات زائدة كانت توسم «سليمة» وتحسب `0` أيام معًا.
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate("1/8/2026", " أسبوع ", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    [Fact]
    public void ComputeDueDate_MatchesTryComputeDueDate()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(
            ActionReminderCalculator.TryComputeDueDate("1/8/2026", "شهر", createdAt).DueDate,
            ActionReminderCalculator.ComputeDueDate("1/8/2026", "شهر", createdAt));
    }

    [Theory]
    [InlineData("سنة", "أحمر")]
    [InlineData("أسبوع", "أخضر")]
    public void ValidateReminder_InvalidDurationOrColor_Throws(string? duration, string? color)
    {
        Assert.Throws<ArgumentException>(() => ActionReminderCalculator.ValidateReminder(duration, color));
    }

    [Theory]
    [InlineData("أسبوع", "أحمر")]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("شهر", null)]
    public void ValidateReminder_ValidOrEmpty_Passes(string? duration, string? color)
    {
        ActionReminderCalculator.ValidateReminder(duration, color);
    }

    [Fact]
    public void ValidateActionDate_Unparsable_ThrowsWithFieldName()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ActionReminderCalculator.ValidateActionDate("ليس تاريخا", "تاريخ الإجراء"));
        Assert.Contains("تاريخ الإجراء", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1/8/2026")]
    public void ValidateActionDate_EmptyOrValid_Passes(string? value)
    {
        ActionReminderCalculator.ValidateActionDate(value, "تاريخ الإجراء");
    }

    [Theory]
    [InlineData("action")]
    [InlineData("note")]
    [InlineData(" action ")]
    [InlineData(null)]
    public void ValidateActionType_ValidOrEmpty_Passes(string? value)
    {
        ActionReminderCalculator.ValidateActionType(value, "نوع الإجراء");
    }

    [Fact]
    public void ValidateActionType_Invalid_ThrowsWithFieldName()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ActionReminderCalculator.ValidateActionType("xyz", "نوع الإجراء"));
        Assert.Contains("نوع الإجراء", ex.Message);
    }
}
