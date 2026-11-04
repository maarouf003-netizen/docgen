namespace DocGenerator.Api.Security;

/// <summary>
/// تحقق إقلاع صريح لسياسة <c>CSP</c>: قيمة <c>Security:CspReportUri</c> الخاطئة كانت
/// تُتجاهل بصمت فتظل التقارير بلا جامع دون أي إشارة تشغيلية. يُستدعى دائمًا في
/// <c>Program.cs</c> بجوار <see cref="HstsSetup.Validate"/>، فيفشل الإقلاع برسالة تسمّي
/// المفتاح والمثال بدل تدهور صامت.
/// </summary>
public static class CspSetup
{
    public static void Validate(SecurityOptions security)
    {
        ArgumentNullException.ThrowIfNull(security);

        if (!CspReportUriPolicy.TryNormalize(security.CspReportUri, out _, out var error))
            throw new InvalidOperationException(error);
    }
}
