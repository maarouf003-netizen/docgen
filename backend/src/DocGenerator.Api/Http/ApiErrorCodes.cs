namespace DocGenerator.Api.Http;

/// <summary>
/// معرّفات مستقرة لرموز الأخطاء في عقد <see cref="ApiProblem"/>: العميل يقرأ
/// <c>code</c> ليتخذ قرارًا (كـ«أعد المحاولة» أو «اعرض تحذير رقم») بلا تحليل نص
/// عربي قابل للتغيير. أي رمز جديد يُضاف هنا ويُستخدم عبر <c>code</c> صريح.
///
/// <para>
/// الرموز العامة مشتقة من حالة HTTP (انظر <see cref="ApiProblem.DefaultCodeFor"/>)
/// فلا يُكرَّر تمريرها، والرموز الخاصة تُمرَّر حيث يكون للخطأ معنى يعالجه العميل
/// فعلًا (تحذير إعادة استخدام الرقم، تعارض التزامن، انشغال التصدير…).
/// </para>
/// </summary>
public static class ApiErrorCodes
{
    /// <summary>مدخلات غير صالحة (400) — الرمز الافتراضي لأي 400 بلا رمز أدق.</summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>حساب رئيس/شعبة/محامٍ بلا فرع: كتابة `400` وقراءة `403` وفق `D11`.</summary>
    public const string BranchRequired = "branch_required";

    public const string Forbidden = "forbidden";
    public const string Unauthorized = "unauthorized";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string UniqueViolation = "unique_violation";
    public const string LockedOut = "locked_out";
    public const string RateLimited = "rate_limited";
    public const string CsrfRejected = "csrf_rejected";
    public const string PortalScopeForbidden = "portal_scope_forbidden";
    public const string ExportBusy = "export_busy";
    public const string ClientErrorLimit = "client_error_limit";
    public const string GenerationDisabled = "generation_disabled";
    public const string GenerationNotAllowed = "generation_not_allowed";
    public const string ServerError = "server_error";
}
