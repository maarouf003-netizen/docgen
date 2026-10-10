using DocGenerator.Application.Services;

namespace DocGenerator.Api.Background;

/// <summary>
/// مضيف التنظيف اليومي للمنتدى: تمريرة عند الإقلاع ثم كل 24 ساعة — رفيع عمدًا
/// (المنطق كله في `ForumRetentionService` القابل للاختبار). أي عطل يُسجَّل
/// ولا يُسقط المضيف أبدًا (التنظيف مهمة صيانة لا حرجة).
/// </summary>
public sealed class ForumRetentionHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<ForumRetentionHostedService> _logger;

    public ForumRetentionHostedService(IServiceProvider services, ILogger<ForumRetentionHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        await RunOnceAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var retention = scope.ServiceProvider.GetRequiredService<IForumRetentionService>();
            var deleted = await retention.ExecuteOnceAsync(ct);
            _logger.LogInformation("اكتمل تنظيف المنتدى: {Count} رسالة محذوفة", deleted);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل تنظيف المنتدى الدوري — ستُعاد المحاولة بعد 24 ساعة");
        }
    }
}
