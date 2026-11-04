using System.Text.RegularExpressions;
using DocGenerator.Api.Http;
using DocGenerator.Api.Middleware;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس رسائل الأخطاء العربية على حدود HTTP (`PR‑6c`):
///
/// 1. <see cref="ApiErrorMessages.From"/> يطهّر ذيول إطار العمل الإنجليزية التي
///    يُلحقها .NET بـ<see cref="ArgumentException.Message"/> عند تمرير اسم معامل
///    (<c>(Parameter 'x')</c>) وبـ<see cref="ArgumentOutOfRangeException"/>
///    (<c>Actual value was …</c>) — وإلا وصلت هذه الذيل إلى واجهة المستخدم.
/// 2. **حارس مصدري:** لا متحكم يضع <c>e.Message</c> مباشرة في جسم الاستجابة،
///    فلا يُعاد التسريب من موضع جديد.
/// </summary>
public class ApiErrorMessagesTests
{
    [Fact]
    public void From_ArgumentExceptionWithParamName_StripsFrameworkSuffix()
    {
        var error = new ArgumentException("نص عربي مفهوم", "request");

        // إثبات أن الإطار يُلحق الذيل فعلًا (وإلا لكان الحارس بلا معنى).
        Assert.Contains("Parameter", error.Message, StringComparison.Ordinal);
        Assert.Equal("نص عربي مفهوم", ApiErrorMessages.From(error));
    }

    [Fact]
    public void From_ArgumentOutOfRangeException_RemovesParamNameAndActualValueBlock()
    {
        var error = new ArgumentOutOfRangeException("pageSize", 0, "حجم الصفحة غير صالح");

        var message = ApiErrorMessages.From(error);

        Assert.Equal("حجم الصفحة غير صالح", message);
        Assert.DoesNotContain("Parameter", message, StringComparison.Ordinal);
        Assert.DoesNotContain("Actual value", message, StringComparison.Ordinal);
    }

    [Fact]
    public void From_CleanArabicMessage_IsUnchanged()
    {
        Assert.Equal(
            "قيمة scope غير صالحة — استخدم user",
            ApiErrorMessages.From(new ArgumentException("قيمة scope غير صالحة — استخدم user")));
    }

    [Fact]
    public void From_MultiLineArabicMessage_IsFlattenedToOneLine()
    {
        // سياسة `SEC-012` نفسها: لا أسطر خام في نص يُعاد للمستخدم.
        var message = ApiErrorMessages.From(new ArgumentException("السطر الأول\nالسطر الثاني"));

        Assert.Equal("السطر الأول السطر الثاني", message);
    }

    [Fact]
    public void From_MessageMentioningParameterWithoutMarker_IsUnchanged()
    {
        const string message = "المعامل Parameter مطلوب في هذا الطلب";

        Assert.Equal(message, ApiErrorMessages.From(new ArgumentException(message)));
    }

    [Fact]
    public void From_LegacyFrameworkParameterLine_IsStripped()
    {
        var error = new ArgumentException("رسالة عربية\r\nParameter name: request");

        Assert.Equal("رسالة عربية", ApiErrorMessages.From(error));
    }

    [Fact]
    public void From_EmptyOrWhitespaceMessage_ReturnsArabicFallback()
    {
        Assert.Equal(ApiErrorMessages.Fallback, ApiErrorMessages.From(new ArgumentException("")));
        Assert.Equal(ApiErrorMessages.Fallback, ApiErrorMessages.From(new ArgumentException("   ")));
        Assert.Equal(
            ApiErrorMessages.Fallback,
            ApiErrorMessages.From(new ArgumentException(" ", "request")));
    }

    [Fact]
    public void From_NullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ApiErrorMessages.From(null!));
    }

    [Fact]
    public void ResponseSanitizer_StripsFrameworkSuffixes_ForUnhandledExceptionPath()
    {
        // المسار الثاني: استثناء لم يلتقطه متحكم فيمرّ بمعالج الاستثناءات العام
        // الذي يستخدم `LogSanitizer.SanitizeForResponse` — فيجب أن يطهّر أيضًا.
        var raw = new ArgumentException("رسالة عربية", "request").Message;

        var sanitized = LogSanitizer.SanitizeForResponse(raw);

        Assert.Equal("رسالة عربية", sanitized);
        Assert.DoesNotContain("Parameter", sanitized, StringComparison.Ordinal);
    }

    /// <summary>
    /// حارس مصدري مشدَّد: يفحص **المتحكمات والوسائط وحيّ `Http`** (حيث تُبنى أجسام الردود)
    /// لا المتحكمات وحدها، ويكشف أي تعريف لـ<c>.Message</c> يدل على استثناء — سواء أُسند
    /// إلى <c>message</c> أو <c>detail</c>/<c>error</c> أو وُضع في استيفاء نصي — ما لم
    /// يكن على السطر نفسه داخل <c>LogSanitizer.SanitizeForResponse(...)</c>.
    /// </summary>
    [Fact]
    public void ApiSources_DoNotReflectUnsanitizedExceptionMessages()
    {
        var apiRoot = Path.Combine(
            FindRepoRoot(), "backend", "src", "DocGenerator.Api");

        var offenders = new List<string>();
        foreach (var folder in new[] { "Controllers", "Middleware", "Http" })
            offenders.AddRange(FindUnsanitizedMessageReflections(Path.Combine(apiRoot, folder)));

        Assert.True(
            offenders.Count == 0,
            "مواضع تعكس رسالة استثناء خام بلا تطهير (تسريب ذيل إنجليزي/سر/سطر): " +
            string.Join(", ", offenders));
    }

    /// <summary>
    /// حارس البند 3: لم يبقَ في المتحكمات أي <c>catch (ArgumentException</c> يترجم إلى
    /// <c>BadRequest</c> — الترجمة صارت في <see cref="GlobalExceptionHandler"/> وحدها.
    /// الاستثناء الوحيد المسموح هو كتل <c>NotFound</c> التسع المضبوطة صراحةً (سلوك مقصود
    /// لأن المورد قد يكون غير موجود أصلًا)، وأي كتلة أخرى أو أي زيادة/نقص = فشل الحارس.
    /// </summary>
    [Fact]
    public void Controllers_DoNotTranslateArgumentExceptionToBadRequest()
    {
        var controllersRoot = Path.Combine(
            FindRepoRoot(), "backend", "src", "DocGenerator.Api", "Controllers");

        var argumentCatches = new List<string>();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(controllersRoot, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!lines[index].TrimStart().StartsWith("catch (ArgumentException", StringComparison.Ordinal))
                    continue;

                var location = $"{Path.GetFileName(file)}:{index + 1}";
                var window = string.Join('\n', lines.Skip(index).Take(4));
                argumentCatches.Add(location);
                if (window.Contains("BadRequest(", StringComparison.Ordinal))
                    offenders.Add(location);
            }
        }

        Assert.True(
            offenders.Count == 0,
            "كتل catch (ArgumentException) ما زالت تترجم إلى BadRequest: " + string.Join(", ", offenders));
        Assert.Equal(9, argumentCatches.Count);
    }

    [Theory]
    [InlineData("return BadRequest(new { message = e.Message });", true)]
    [InlineData("return NotFound(new { message = ex.Message });", true)]
    [InlineData("return StatusCode(500, new { detail = exception.Message });", true)]
    [InlineData("var text = $\"فشل: {dbError.Message}\";", true)]
    [InlineData("throw new InvalidOperationException(innerException.Message);", true)]
    [InlineData("var text = httpEx.Message;", true)]
    [InlineData("var text = index.Message;", false)]
    [InlineData("return BadRequest(new { message = ApiErrorMessages.From(e) });", false)]
    [InlineData("var message = LogSanitizer.SanitizeForResponse(exception.Message);", false)]
    [InlineData("var text = LogSanitizer.SanitizeForLog(report.Message, 200);", false)]
    [InlineData("var text = request.Message;", false)]
    [InlineData("var text = suggestion.Message;", false)]
    [InlineData("// e.Message في تعليق لا يُفحص", false)]
    public void RawExceptionMessageDetector_ClassifiesSourceLines(string line, bool expected)
    {
        Assert.Equal(expected, HasUnsanitizedMessageReflection(line));
    }

    private static readonly Regex MessageAccess =
        new(@"\b(?<receiver>[A-Za-z_]\w*)\.Message\b", RegexOptions.CultureInvariant);

    /// <summary>
    /// مستقبِلات <c>.Message</c> الدالة على استثناء (أو رسالة إطار/قاعدة بيانات)، لا على
    /// خاصية DTO مشروعة مثل <c>request.Message</c> أو <c>report.Message</c>.
    /// </summary>
    private static bool IsExceptionReceiver(string receiver)
    {
        var name = receiver.ToLowerInvariant();
        if (name is "e" or "ex" or "err" or "error"
            or "inner" or "innerexception" or "deepest" or "deep" or "cause" or "wrapped")
            return true;
        if (name.EndsWith("exception", StringComparison.Ordinal) ||
            name.EndsWith("error", StringComparison.Ordinal))
            return true;
        // لاحقة `Ex` الشائعة (`httpEx`/`dbEx`) — مع استبعاد كلمات إنجليزية تنتهي بها.
        return name.EndsWith("ex", StringComparison.Ordinal)
            && !name.EndsWith("index", StringComparison.Ordinal)
            && !name.EndsWith("complex", StringComparison.Ordinal);
    }

    /// <summary>
    /// تمرير نص تطهير على السطر نفسه: `SanitizeForResponse(e.Message)` مقبول، أما
    /// `var m = e.Message;` ثم `SanitizeForResponse(m)` فلا — كي لا يمر التسريب عبر وسيط.
    /// </summary>
    private static bool IsWrappedByResponseSanitizer(string line, int messageIndex)
    {
        const string call = "SanitizeForResponse(";
        var prefix = line[..messageIndex];
        var open = prefix.LastIndexOf(call, StringComparison.Ordinal);
        return open >= 0 && prefix.IndexOf(')', open) < 0;
    }

    private static bool HasUnsanitizedMessageReflection(string line)
    {
        var trimmed = line.TrimStart();
        // تعليقات السطر/التوثيق: `.Message` فيها أمثلة توثيقية لا كود.
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("*", StringComparison.Ordinal))
            return false;

        foreach (Match access in MessageAccess.Matches(line))
        {
            if (!IsExceptionReceiver(access.Groups["receiver"].Value))
                continue;
            if (IsWrappedByResponseSanitizer(line, access.Index))
                continue;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> FindUnsanitizedMessageReflections(string directory)
    {
        Assert.True(Directory.Exists(directory), $"مجلد المصدر غير موجود: {directory}");

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (HasUnsanitizedMessageReflection(lines[index]))
                    yield return $"{Path.GetFileName(file)}:{index + 1}";
            }
        }
    }

    /// <summary>يصعد من مجلد تشغيل الاختبار حتى يجد علامة الجذر (<c>AGENTS.md</c>).</summary>
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"تعذّر العثور على جذر المستودع (AGENTS.md) ابتداءً من '{AppContext.BaseDirectory}'.");
    }
}
