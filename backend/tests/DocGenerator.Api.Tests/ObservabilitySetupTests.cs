using System.Diagnostics.Metrics;
using DocGenerator.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace DocGenerator.Api.Tests;

/// <summary>
/// `P5-01` — عقد التفعيل: لا تسجيل إطلاقًا بلا نقطة نهاية، ومزوّدا التتبع/المقاييس
/// ومُصدِّر `OTLP` يُسجَّلون فعليًا عند وجودها، مع قراءة الإعداد ومتغيّر البيئة.
/// </summary>
[Collection(ApiObservabilityTestCollection.Name)]
[Trait("Category", "Unit")]
public class ObservabilitySetupTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value))
            .Build();

    private static TestEnvironment Environment(string name = "Production")
        => new() { EnvironmentName = name };

    [Fact]
    public void Unconfigured_RegistersNothingAtAll()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var before = services.Count;
        var configuration = Configuration(
            (ObservabilitySetup.EndpointConfigurationKey, ""),
            (ObservabilitySetup.OtlpEndpointEnvironmentVariable, "   "));

        var returned = ObservabilitySetup.AddDocGeneratorObservability(
            services, configuration, Environment());

        Assert.Same(services, returned);
        Assert.Equal(before, services.Count);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType.Namespace?.StartsWith("OpenTelemetry", StringComparison.Ordinal) == true);
        Assert.False(ObservabilitySetup.ReadSettings(configuration, Environment()).IsEnabled);
    }

    [Fact]
    public void Configured_ResolvesProvidersAndConfiguresOtlpExporter()
    {
        var endpoint = new Uri("http://127.0.0.1:1");
        var services = new ServiceCollection();
        services.AddLogging();

        ObservabilitySetup.AddDocGeneratorObservability(
            services,
            Configuration((ObservabilitySetup.EndpointConfigurationKey, endpoint.ToString())),
            Environment());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<TracerProvider>());
        Assert.NotNull(provider.GetService<MeterProvider>());

        var exporter = provider.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;
        Assert.Equal(endpoint, exporter.Endpoint);
        Assert.Equal(OtlpExportProtocol.Grpc, exporter.Protocol);
    }

    [Fact]
    public void Configured_SubscribesToNpgsqlMeter_ForPoolMetrics()
    {
        // P5-02b: يجب أن يشترك المزوّد فعليًا بالمتر — إزالة .AddMeter تُفشل هذا
        // الاختبار (Instrument.Enabled=false) بدل فحص ثابت نصي جوف.
        var services = new ServiceCollection();
        services.AddLogging();
        ObservabilitySetup.AddDocGeneratorObservability(
            services,
            Configuration((ObservabilitySetup.EndpointConfigurationKey, "http://127.0.0.1:1")),
            Environment());

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<MeterProvider>());

        using var meter = new Meter(ObservabilitySetup.NpgsqlMetricMeter);
        var connectionCount = meter.CreateUpDownCounter<long>("db.client.connection.count");
        Assert.True(connectionCount.Enabled);
    }

    [Fact]
    public void Settings_PreferApplicationKey_AndTrimValues()
    {
        var configuration = Configuration(
            (ObservabilitySetup.EndpointConfigurationKey, "  http://collector-a:4317  "),
            (ObservabilitySetup.OtlpEndpointEnvironmentVariable, "http://collector-b:4317"),
            (ObservabilitySetup.ServiceNameConfigurationKey, "  custom-service  "));

        var settings = ObservabilitySetup.ReadSettings(configuration, Environment("Staging"));

        Assert.Equal("http://collector-a:4317", settings.OtlpEndpoint);
        Assert.Equal("custom-service", settings.ServiceName);
        Assert.Equal("Staging", settings.DeploymentEnvironment);
        Assert.False(string.IsNullOrWhiteSpace(settings.ServiceVersion));
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public void Settings_FallBackToEnvironmentVariableAndDefaultServiceName()
    {
        var configuration = Configuration(
            (ObservabilitySetup.EndpointConfigurationKey, "  "),
            (ObservabilitySetup.OtlpEndpointEnvironmentVariable, "http://collector:4317"));

        var settings = ObservabilitySetup.ReadSettings(configuration, Environment());

        Assert.Equal("http://collector:4317", settings.OtlpEndpoint);
        Assert.Equal(ObservabilitySetup.DefaultServiceName, settings.ServiceName);
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public void Settings_NoEndpoint_IsDisabled()
    {
        var settings = ObservabilitySetup.ReadSettings(Configuration(), Environment());

        Assert.Null(settings.OtlpEndpoint);
        Assert.False(settings.IsEnabled);
    }

    [Theory]
    [InlineData("not a uri")]
    [InlineData("collector:4317")]
    [InlineData("ftp://collector:4317")]
    public void Add_InvalidEndpoint_FailsFastWithKeyName(string endpoint)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ObservabilitySetup.AddDocGeneratorObservability(
                services,
                Configuration((ObservabilitySetup.EndpointConfigurationKey, endpoint)),
                Environment()));

        Assert.Contains(ObservabilitySetup.EndpointConfigurationKey, exception.Message, StringComparison.Ordinal);
        Assert.Contains(ObservabilitySetup.OtlpEndpointEnvironmentVariable, exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "DocGenerator.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
