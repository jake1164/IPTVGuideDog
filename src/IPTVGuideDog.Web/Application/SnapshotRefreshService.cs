using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace IPTVGuideDog.Web.Application;

/// <summary>
/// Singleton background service that runs scheduled and on-demand snapshot refreshes.
/// Also implements <see cref="IRefreshTrigger"/> for manual triggering from API endpoints.
/// </summary>
public sealed class SnapshotRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshOptions> refreshOptions,
    ILogger<SnapshotRefreshService> logger)
    : BackgroundService, IRefreshTrigger
{
    // Semaphore guards the running refresh — at-most-one execution at a time
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    // Bounded channel collapses multiple triggers to at-most-one queued run
    private readonly Channel<bool> _triggerChannel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });

    // -------------------------------------------------------------------------
    // IRefreshTrigger
    // -------------------------------------------------------------------------

    public bool IsRefreshing => _executionGate.CurrentCount == 0;

    public bool TriggerRefresh()
    {
        if (_executionGate.CurrentCount == 0)
        {
            return false; // Already running — caller returns 409
        }

        _triggerChannel.Writer.TryWrite(true);
        return true;
    }

    // -------------------------------------------------------------------------
    // BackgroundService
    // -------------------------------------------------------------------------

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SnapshotRefreshService started.");

        // Startup delay before the first scheduled run
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(refreshOptions.Value.StartupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // Queue the first run immediately after startup delay
        _triggerChannel.Writer.TryWrite(true);

        // Start the schedule loop in background
        _ = ScheduleLoopAsync(stoppingToken);

        // Process triggers
        await foreach (var _ in _triggerChannel.Reader.ReadAllAsync(stoppingToken))
        {
            // Non-blocking acquire: if something is already running, drop the trigger
            if (!await _executionGate.WaitAsync(0, stoppingToken))
            {
                logger.LogDebug("Scheduled refresh skipped — a refresh is already in progress.");
                continue;
            }

            try
            {
                await RunRefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snapshot refresh failed unexpectedly.");
            }
            finally
            {
                _executionGate.Release();
            }
        }

        logger.LogInformation("SnapshotRefreshService stopped.");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task ScheduleLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(refreshOptions.Value.IntervalHours), stoppingToken);

                if (_executionGate.CurrentCount > 0)
                {
                    _triggerChannel.Writer.TryWrite(true);
                }
                else
                {
                    logger.LogDebug("Scheduled refresh trigger skipped — a refresh is already in progress.");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunRefreshAsync(CancellationToken stoppingToken)
    {
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        runCts.CancelAfter(TimeSpan.FromMinutes(refreshOptions.Value.TimeoutMinutes));

        await using var scope = scopeFactory.CreateAsyncScope();
        var builder = scope.ServiceProvider.GetRequiredService<SnapshotBuilder>();
        await builder.RunAsync(runCts.Token);
    }
}
