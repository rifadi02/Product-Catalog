namespace Catalog.Infrastructure.BackgroundJobs;

/// <summary>
/// §4.6 cleanup. Expired refresh tokens accumulate forever otherwise.
///
/// Rows are kept for 30 days past expiry rather than deleted on expiry: the
/// <c>ReplacedByTokenHash</c> chain is what makes a reuse-detection event investigable after
/// the fact, and a chain with its links deleted answers no questions.
/// </summary>
public sealed class ExpiredRefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpiredRefreshTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPastExpiry = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();

                var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var cutoff = clock.UtcNow - RetentionPastExpiry;
                var deleted = await tokens.DeleteExpiredBeforeAsync(cutoff, stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Pruned {Count} refresh tokens expired before {Cutoff:O}", deleted, cutoff);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refresh token cleanup failed; will retry on the next interval");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
