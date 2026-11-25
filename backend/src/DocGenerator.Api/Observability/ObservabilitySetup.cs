using System.Reflection;
using DocGenerator.Application.Common.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DocGenerator.Api.Observability;

/// <summary>
/// `P5-01` — رصد اختياري (`OpenTelemetry`): تتبع + مقاييس مع مُصدِّر `OTLP`،
/// **ولا شيء إطلاقًا** عند غياب نقطة نهاية. التسجيل هنا لا يبني مزوّدًا ولا يفتح
/// اتصالًا؛ مزوّد التتبع/المقاييس يُنشأ فقط عند تفعيل الرصد.
/// </summary>
public static class ObservabilitySetup
{
    /// <summary>مفتاح الإعداد المفضّل لنقطة نهاية `OTLP`.</summary>
    public const string EndpointConfigurationKey = "Observability:OtlpEndpoint";

    /// <summary>متغيّر البيئة القياسي — يُقرأ كبديل عند غياب المفتاح أعلاه.</summary>
    public const string OtlpEndpointEnvironmentVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public const string ServiceNameConfigurationKey = "Observability:ServiceName";

    public const string DefaultServiceName = "docgen-api";

    /// <summary>سمة المورد القياسية (اسمها كما في متطلبات `P5-01`).</summary>
    public const string DeploymentEnvironmentAttributeName = "deployment.environment";

    /// <summary>
    /// يقرأ الإعدادات الفعّالة: نقطة النهاية (إعداد التطبيق ثم متغيّر البيئة)،
    /// واسم الخدمة (افتراضي <c>docgen-api</c>)، ونسخة التطبيق وبيئة النشر.
    /// </summary>
    public static ObservabilitySettings ReadSettings(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var endpoint = ReadNonEmpty(configuration[EndpointConfigurationKey])
            ?? ReadNonEmpty(configuration[OtlpEndpointEnvironmentVariable]);
        var serviceName = ReadNonEmpty(configuration[ServiceNameConfigurationKey]) ?? DefaultServiceName;
        var deploymentEnvironment = ReadNonEmpty(environment.EnvironmentName) ?? "Unknown";

        return new ObservabilitySettings(
            endpoint,
            serviceName,
            ReadServiceVersion(),
            deploymentEnvironment);
    }

    /// <summary>
    /// يسجّل التتبع والمقاييس ومُصدِّر `OTLP` **فقط** إذا وُجدت نقطة نهاية غير فارغة.
    /// عند الغياب يعيد المجموعة كما هي بلا أي تسجيل (ولا حتى مُصدِّر معطَّل).
    /// </summary>
    public static IServiceCollection AddDocGeneratorObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var settings = ReadSettings(configuration, environment);
        if (!settings.IsEnabled)
            return services;

        var endpoint = ParseEndpoint(settings.OtlpEndpoint!);

        services.Configure<OtlpExporterOptions>(options =>
        {
            options.Endpoint = endpoint;
            // gRPC هو الافتراضي القياسي لـOTLP (منفذ 4317)؛ تُوثَّق نقطة النهاية كاملة.
            options.Protocol = OtlpExportProtocol.Grpc;
        });

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(settings.ServiceName, serviceVersion: settings.ServiceVersion)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new(DeploymentEnvironmentAttributeName, settings.DeploymentEnvironment),
                }))
            .WithTracing(tracing => tracing
                // المصدر المشترك لمعالجات `Application` (أوامر/استعلامات).
                .AddSource(ApplicationDiagnostics.ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // PII: لا نُثري الوسوم بأي محتوى طلب؛ حزمة EF 1.21 تُصدر نص SQL
                // المُعقَّم (placeholders) ولا تُصدر قيم المعاملات افتراضيًا (الخاصية
                // الداخلية في 1.21 غير قابلة للضبط من الخارج، وقيمتها الافتراضية false).
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter());

        return services;
    }

    private static Uri ParseEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{EndpointConfigurationKey} (or {OtlpEndpointEnvironmentVariable}) must be an absolute "
                + $"http/https URI, for example http://collector:4317. Actual value: '{endpoint}'.");
        }

        return uri;
    }

    private static string? ReadNonEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ReadServiceVersion()
    {
        var assembly = typeof(Program).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Trim();

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
