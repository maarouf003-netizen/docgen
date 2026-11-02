using System.Text.RegularExpressions;
using DocGenerator.Api.Controllers;
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

    [Fact]
    public void Controllers_DoNotPutRawExceptionMessagesInResponseBodies()
    {
        var controllers = Path.Combine(
            FindRepoRoot(), "backend", "src", "DocGenerator.Api", "Controllers");

        Assert.True(Directory.Exists(controllers), $"مجلد المتحكمات غير موجود: {controllers}");

        var rawMessage = new Regex(@"\bmessage\s*=\s*\w+\.Message\b", RegexOptions.CultureInvariant);
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(controllers, "*.cs"))
        {
            // هذا الملف نفسه يوثّق النمط الخاطئ داخل تعليق — لا يُفحص.
            if (Path.GetFileName(file) == "ApiErrorMessages.cs")
                continue;

            var text = File.ReadAllText(file);
            if (rawMessage.IsMatch(text))
                offenders.Add(Path.GetFileName(file));
        }

        Assert.True(
            offenders.Count == 0,
            "متحكمات تضع رسالة الاستثناء الخام في الجسم (تسريب ذيل إنجليزي): " +
            string.Join(", ", offenders));
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
