using LmsTracker.Api.Modules.Courses;

namespace LmsTracker.Api.Infrastructure.BackgroundTasks;

/// <summary>
/// Durable replacement for the old in-memory work queue: instead of draining a process-only
/// Channel of delegates (lost on crash before they run), this polls on a fixed interval for
/// courses flagged via Course.SkillTagExtractionRequestedAtUtc - a column set by
/// CourseCatalogService.SyncFromProvidersAsync and cleared by
/// ProcessPendingSkillTagExtractionsAsync. The "pending work" lives in SQL Server, not process
/// memory, so a crash between a sync completing and extraction running no longer silently drops
/// it - the next poll after restart just finds the same flagged rows and retries them.
/// </summary>
public sealed class SkillTagExtractionPollingService(
    IServiceScopeFactory scopeFactory,
    ILogger<SkillTagExtractionPollingService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        // Process once immediately on startup (picks up anything flagged before a crash/restart
        // right away) and then every PollInterval after.
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var catalogService = scope.ServiceProvider.GetRequiredService<CourseCatalogService>();
                await catalogService.ProcessPendingSkillTagExtractionsAsync(BatchSize, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Unhandled exception polling for pending skill-tag extractions.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
