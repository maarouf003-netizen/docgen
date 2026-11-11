using DocGenerator.Application.Common;

namespace DocGenerator.Application.Tests;

public class ActionReminderCalculatorTests
{
    [Fact]
    public void TryComputeDueDate_ValidDateAndDuration_NotSuspect()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, "1/8/2026", "أسبوع", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_EmptyDateAndNullDuration_FallsBackToCreatedAtWithoutSuspect()
    {
        // الغياب قصد تصميمي (تاريخ الإنشاء + المدة) لا تلف — فلا وسم.
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, null, null, createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 1), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_UnparsableDate_SuspectWithCreatedAtBase()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, "ليس تاريخا", "أسبوع", createdAt);

        Assert.True(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_UnknownDuration_SuspectWithZeroDays()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, "1/8/2026", "سنة", createdAt);

        Assert.True(suspect);
        Assert.Equal(new DateTime(2026, 8, 1), dueDate);
    }

    [Fact]
    public void TryComputeDueDate_PaddedDuration_TrimsConsistentlyForFlagAndDays()
    {
        // انحدار مُكتشف بالمراجعة: مسافات زائدة كانت توسم «سليمة» وتحسب `0` أيام معًا.
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(null, "1/8/2026", " أسبوع ", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 8, 8), dueDate);
    }

    /// <summary>
    /// الأساس الصريح (`ReminderBaseDate`) يتقدّم على تاريخ الإجراء، ولا يُوسم «مشتبه» بسبب
    /// نص تاريخ قديم غير قابل للتحليل — الموعد بيانات مؤكدة لا نتيجة حساب احتياطي.
    /// </summary>
    [Fact]
    public void TryComputeDueDate_ExplicitBase_WinsOverUnparsableActionDate_NotSuspect()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var baseDate = new DateTime(2026, 10, 11);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
            baseDate, "ليس تاريخا", "3 أيام", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 10, 14), dueDate);
    }

    /// <summary>الأساس الصريح يتقدّم على تاريخ الإجراء الصالح أيضًا (حالة النقل).</summary>
    [Fact]
    public void TryComputeDueDate_ExplicitBase_WinsOverActionDate()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var baseDate = new DateTime(2026, 11, 11);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
            baseDate, "10/11/2026", "3 أيام", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 11, 14), dueDate);
    }

    /// <summary>غياب الأساس يُبقي السلوك التاريخي: تاريخ الإجراء + المدة (لا انحدار للصفوف القديمة).</summary>
    [Fact]
    public void TryComputeDueDate_NoBase_FallsBackToActionDate()
    {
        var createdAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

        var (dueDate, suspect) = ActionReminderCalculator.TryComputeDueDate(
            null, "10/11/2026", "3 أيام", createdAt);

        Assert.False(suspect);
        Assert.Equal(new DateTime(2026, 11, 13), dueDate);
    }

    /// <summary>قاعدة الإنشاء: الأساس = تاريخ الإجراء عند طلب تذكير، و`null` بلا تذكير.</summary>
    [Theory]
    [InlineData("3 أيام", "أحمر", "10/11/2026", true)]
    [InlineData(null, "أحمر", "10/11/2026", true)]
    [InlineData("3 أيام", null, "10/11/2026", true)]
    [InlineData(null, null, "10/11/2026", false)]
    public void BaseDateForNewReminder_RequiresReminderAndParsesDate(
        string? duration, string? color, string actionDate, bool expectBase)
    {
        var baseDate = ActionReminderCalculator.BaseDateForNewReminder(duration, color, actionDate);

        if (expectBase)
            Assert.Equal(new DateTime(2026, 11, 10), baseDate);
        else
            Assert.Null(baseDate);
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
