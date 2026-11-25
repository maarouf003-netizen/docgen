namespace DocGenerator.Api.Observability;

/// <summary>
/// `P5-01` — الإعدادات الفعّالة للرصد، مقروءة مرة واحدة عند الإقلاع. غياب
/// <see cref="OtlpEndpoint"/> (أو كونه فراغًا) يعني «الرصد معطَّل» فلا يُسجَّل أي مزوّد.
/// </summary>
/// <param name="OtlpEndpoint">عنوان مستقبل `OTLP` المطلق (http/https) أو <c>null</c>.</param>
/// <param name="ServiceName">اسم الخدمة المعلن في المورد.</param>
/// <param name="ServiceVersion">نسخة التطبيق من `InformationalVersion`.</param>
/// <param name="DeploymentEnvironment">بيئة النشر (`ASPNETCORE_ENVIRONMENT`).</param>
public sealed record ObservabilitySettings(
    string? OtlpEndpoint,
    string ServiceName,
    string ServiceVersion,
    string DeploymentEnvironment)
{
    public bool IsEnabled => !string.IsNullOrWhiteSpace(OtlpEndpoint);
}
