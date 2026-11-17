using System.Text.RegularExpressions;

namespace DocGenerator.Api.Tests;

/// <summary>
/// حارس عقد الخطأ (P4-03): لا موضع في طبقة HTTP يبني جسم خطأ يدويًا بصيغة
/// <c>new { message = … }</c> أو يعيد <c>ValidationProblem</c> الإطاري — فكل جسم
/// خطأ يمرّ عبر <see cref="DocGenerator.Api.Http.ApiProblem"/> فيحمل
/// <c>status</c>/<c>code</c>/<c>message</c> و<c>traceId</c> نفسها.
///
/// <para>
/// الرسائل الناجحة (<c>Ok(new { message = … })</c>) مستثناة عمدًا: ليست أخطاء،
/// وعقدها مستقل (رسالة تأكيد للعرض).
/// </para>
/// </summary>
[Trait("Category", "Unit")]
public class ErrorContractGuardTests
{
    /// <summary>
    /// أنماط بناء جسم خطأ بلا العقد الموحّد. تُفحص الأسطر بـ<c>TrimStart</c> كي لا
    /// تفلت المسافات، وتُستثنى أسطر التعليقات كما في حراس المصادر القائمة.
    /// </summary>
    private static readonly Regex AdHocErrorBody = new(
        @"(?<verb>BadRequest|NotFound|Unauthorized|Conflict|JsonResult)\(\s*new\s*\{" +
        @"|WriteAsJsonAsync\(\s*new\s*\{" +
        @"|StatusCode\(\s*(?:[45]\d\d|StatusCodes\.Status[45]\d\d\w*)\s*,\s*new\s*\{" +
        @"|ValidationProblem\(",
        RegexOptions.CultureInvariant);

    [Fact]
    public void ApiSources_DoNotBuildAdHocErrorBodies()
    {
        var apiRoot = Path.Combine(FindRepoRoot(), "backend", "src", "DocGenerator.Api");
        var offenders = new List<string>();

        foreach (var folder in new[] { "Controllers", "Middleware", "Security", "Filters", "Http" })
        {
            var directory = Path.Combine(apiRoot, folder);
            Assert.True(Directory.Exists(directory), $"مجلد المصدر غير موجود: {directory}");

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (BuildsAdHocErrorBody(lines[index]))
                        offenders.Add($"{folder}/{Path.GetFileName(file)}:{index + 1}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "أجسام خطأ خارج العقد الموحّد (استخدم ApiProblem.Result/WriteAsync): " +
            string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("return BadRequest(new { message = \"س\" });", true)]
    [InlineData("return NotFound(new { message = ApiErrorMessages.From(e) });", true)]
    [InlineData("return Conflict(new { message = \"س\", code = \"c\" });", true)]
    [InlineData("return Unauthorized( new { message = \"س\" } );", true)]
    [InlineData("return StatusCode(500, new { message = \"س\" });", true)]
    [InlineData("return StatusCode(StatusCodes.Status429TooManyRequests, new { message = \"س\" });", true)]
    [InlineData("return StatusCode(201, new { id = 7 });", false)]
    [InlineData("await context.Response.WriteAsJsonAsync(new { message = \"س\" });", true)]
    [InlineData("return new JsonResult(new { message = \"س\" });", true)]
    [InlineData("return ValidationProblem(ModelState);", true)]
    [InlineData("return ApiProblem.Result(HttpContext, 400, ApiErrorCodes.ValidationFailed, \"س\");", false)]
    [InlineData("return Ok(new { message = \"تم الحفظ\" });", false)]
    [InlineData("await ApiProblem.WriteAsync(context, 429, ApiErrorCodes.RateLimited, \"س\");", false)]
    [InlineData("// return BadRequest(new { message = \"مثال في تعليق\" });", false)]
    public void AdHocErrorBodyDetector_ClassifiesSourceLines(string line, bool expected)
        => Assert.Equal(expected, BuildsAdHocErrorBody(line));

    private static bool BuildsAdHocErrorBody(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.StartsWith("*", StringComparison.Ordinal))
            return false;

        return AdHocErrorBody.IsMatch(line);
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
