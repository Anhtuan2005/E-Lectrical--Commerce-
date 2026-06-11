namespace EcommerceApp.Services;

public class AbandonedCartRecoveryHostedService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ScanInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AbandonedCartRecoveryHostedService> _logger;

    public AbandonedCartRecoveryHostedService(IServiceScopeFactory scopeFactory, ILogger<AbandonedCartRecoveryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            await ScanAsync(stoppingToken);

            using var timer = new PeriodicTimer(ScanInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ScanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAbandonedCartRecoveryService>();
            await service.ProcessDueCartsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Abandoned cart recovery scan failed.");
        }
    }
}
