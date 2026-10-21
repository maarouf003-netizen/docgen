using System.Globalization;
using System.Text;
using DocGenerator.Application.Services;
using Microsoft.Data.Sqlite;

namespace DocGenerator.Application.Tests;

/// <summary>
/// حراسة سياسة طول النصوص (الخطة §7.3 و§14.1): المعاينة الواعية بـUnicode،
/// و<code>MessageLength</code> بنقاط Unicode، و<code>PreviewTruncated</code> من الخادم،
/// وسقوف الأمان.
/// </summary>
public class HeadAlertTextLimitsTests
{
    [Fact]
    public void BuildPreview_ShortMessage_ReturnsFullTextAndNotTruncated()
    {
        var (preview, truncated) = HeadAlertTextLimits.BuildPreview("تنبيه قصير");

        Assert.Equal("تنبيه قصير", preview);
        Assert.False(truncated);
    }

    [Fact]
    public void BuildPreview_EmptyMessage_ReturnsEmptyAndNotTruncated()
    {
        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(null);

        Assert.Equal(string.Empty, preview);
        Assert.False(truncated);
    }

    [Fact]
    public void BuildPreview_ExactlyAtLimit_ReturnsFullTextAndNotTruncated()
    {
        var message = new string('أ', HeadAlertTextLimits.PreviewMaxElements);

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.Equal(message, preview);
        Assert.False(truncated);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
    }

    [Fact]
    public void BuildPreview_OneOverLimit_TruncatesWithinLimit()
    {
        var message = new string('أ', HeadAlertTextLimits.PreviewMaxElements + 1);

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.True(truncated);
        Assert.EndsWith(HeadAlertTextLimits.Ellipsis, preview);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
    }

    [Fact]
    public void BuildPreview_DoesNotSplitSurrogatePair()
    {
        // 140 إيموجي = 280 نقطة Unicode (كل إيموجي نقطتان) — حدّ_preview بالضبط.
        var message = string.Concat(Enumerable.Repeat("😀", 140));

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.False(truncated);
        Assert.Equal(message, preview);
        Assert.DoesNotContain('\uFFFD', preview);
    }

    [Fact]
    public void BuildPreview_TruncationKeepsSurrogatePairsIntact()
    {
        // 281 إيموجي = 281 نقطة Unicode (كل إيموجي نقطة واحدة، لكنها وحدتان UTF-16).
        var message = string.Concat(Enumerable.Repeat("😀", 281));

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.True(truncated);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
        Assert.DoesNotContain('\uFFFD', preview);
        // 279 إيموجي كاملة + علامة القصّ (بلا إيموجي نصفية).
        Assert.Equal(279, preview.Split("😀").Length - 1);
        // وحدات UTF-16: كل إيموجي وحدتان + علامة القصّ = 559.
        Assert.Equal(559, preview.Length);
    }

    [Fact]
    public void BuildPreview_DoesNotLeaveDanglingCombiningMark()
    {
        // «ا» + علامة تدرّج (U+064B) — القطع عند الحد يترك العلامة معلّقة فيُحذف آخر محرف
        // مركّب قبل إضافة علامة القصّ.
        var text = new string('ا', HeadAlertTextLimits.PreviewMaxElements) + "َ";
        var message = text + new string('ب', 10);

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.True(truncated);
        var lastBeforeEllipsis = preview[^2];
        Assert.NotEqual('\u064B', lastBeforeEllipsis);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
    }

    [Fact]
    public void BuildPreview_DoesNotLeaveDanglingZeroWidthJoiner()
    {
        // تسلسل إيموجي/Zwj: القطع بعد ZWJ يتركه معلّقًا فيُحذف.
        var message = new string('ا', HeadAlertTextLimits.PreviewMaxElements - 1) + "\u200d" + "👨";

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.True(truncated);
        // مقارنة ترتيبية (Ordinal): المقارنة Culturally تُهمل محارف الصفر فتُبقي ZWJ ضمنيًا.
        Assert.False(preview.Contains("\u200d", StringComparison.Ordinal));
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
    }

    [Fact]
    public void CountCodePoints_MatchesSqliteLength()
    {
        var samples = new[]
        {
            "نص عربي عادي",
            "😀😀😀",
            "اَ بّ",
            string.Concat(Enumerable.Repeat("🧑‍💻", 20)),
        };

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        foreach (var sample in samples)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT length(@text)";
            command.Parameters.AddWithValue("@text", sample);
            var sqlLength = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);

            Assert.Equal(sqlLength, HeadAlertTextLimits.CountCodePoints(sample));
        }
    }

    [Fact]
    public void SqlPrefixLength_IsEnoughToBuildFullPreview()
    {
        // البادئة المجلوبة من القاعدة (561 نقطة) أطول من السقف، فتكفي لبناء معاينة
        // كاملة 280 نقطة داخل الذاكرة دون جلب النص كاملًا.
        Assert.True(HeadAlertTextLimits.SqlPrefixLength > HeadAlertTextLimits.PreviewMaxElements * 2);

        var message = new string('ا', HeadAlertTextLimits.SqlPrefixLength);
        var prefix = message[..HeadAlertTextLimits.SqlPrefixLength];
        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(prefix);

        Assert.True(truncated);
        Assert.Equal(HeadAlertTextLimits.PreviewMaxElements, HeadAlertTextLimits.CountCodePoints(preview));
    }

    [Fact]
    public void EnsureWithinSafety_AcceptsLimitAndRejectsBeyond()
    {
        var atLimit = new string('أ', HeadAlertTextLimits.SafetyMessageLimit);
        Assert.Equal(atLimit, HeadAlertTextLimits.EnsureWithinSafety(atLimit));

        var overLimit = new string('أ', HeadAlertTextLimits.SafetyMessageLimit + 1);
        var ex = Assert.Throws<ArgumentException>(() => HeadAlertTextLimits.EnsureWithinSafety(overLimit));
        Assert.Contains(HeadAlertTextLimits.SafetyMessageLimit.ToString(CultureInfo.InvariantCulture), ex.Message);
    }

    [Fact]
    public void EnsureWithinManualLimit_AcceptsLimitAndRejectsBeyond()
    {
        var atLimit = new string('أ', HeadAlertTextLimits.ManualMessageLimit);
        Assert.Equal(atLimit, HeadAlertTextLimits.EnsureWithinManualLimit(atLimit));

        var overLimit = new string('أ', HeadAlertTextLimits.ManualMessageLimit + 1);
        Assert.Throws<ArgumentException>(() => HeadAlertTextLimits.EnsureWithinManualLimit(overLimit));
    }

    [Fact]
    public void PreviewForAudit_IsUnicodeAwareAndBounded()
    {
        var message = string.Concat(Enumerable.Repeat("😀", 200));
        var preview = HeadAlertTextLimits.PreviewForAudit(message);

        Assert.True(HeadAlertTextLimits.CountCodePoints(preview) <= 120);
        Assert.DoesNotContain('\uFFFD', preview);
    }

    [Fact]
    public void BuildPreview_RejectsNonPositiveLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => HeadAlertTextLimits.BuildPreview("نص", 0));
    }

    [Fact]
    public void BuildPreview_HandlesEmptyMessageWithCustomLimit()
    {
        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(string.Empty, 10);
        Assert.Equal(string.Empty, preview);
        Assert.False(truncated);
    }

    [Fact]
    public void BuildPreview_Utf16LengthDiffersFromCodePointCount()
    {
        // إثبات أن الطول بالنقاط لا بوحدات UTF-16: 100 إيموجي = 200 نقطة و200 وحدة UTF-16.
        var message = string.Concat(Enumerable.Repeat("😀", 100));
        Assert.Equal(200, message.Length);
        Assert.Equal(100, HeadAlertTextLimits.CountCodePoints(message));

        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);
        Assert.False(truncated);
        Assert.Equal(message, preview);
    }

    [Fact]
    public void BuildPreview_TruncatedTextIsNeverLongerThanLimit()
    {
        var random = new Random(20261020);
        var builder = new StringBuilder();
        for (var i = 0; i < 500; i++)
            builder.Append((char)random.Next(0x0600, 0x0700));

        var message = builder.ToString();
        var (preview, truncated) = HeadAlertTextLimits.BuildPreview(message);

        Assert.True(truncated);
        Assert.True(HeadAlertTextLimits.CountCodePoints(preview) <= HeadAlertTextLimits.PreviewMaxElements);
    }
}
