using System.Collections.Concurrent;
using Farol.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Farol.Web.Services;

public class SiteMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<FarolDbContext> dbFactory,
    ILogger<SiteMonitorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    private const int MaxParallelChecks = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Site monitor started, ticking every {Interval}", TickInterval);

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Monitoring cycle failed; will retry on next tick");
            }
        }

        logger.LogInformation("Site monitor stopped");
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);

        var dueSites = await db.Sites
            .Where(s => s.IsActive)
            .Select(s => new
            {
                Site = s,
                LastCheckedAt = s.Checks.Max(c => (DateTimeOffset?)c.CheckedAt)
            })
            .Where(x => x.LastCheckedAt == null
                     || x.LastCheckedAt.Value.AddMinutes(x.Site.CheckIntervalMinutes) <= now)
            .Select(x => x.Site)
            .AsNoTracking()
            .ToListAsync(stoppingToken);

        if (dueSites.Count == 0)
            return;

        logger.LogInformation("Checking {Count} site(s)", dueSites.Count);

        var results = new ConcurrentBag<SiteCheck>();

        using var scope = scopeFactory.CreateScope();
        var checker = scope.ServiceProvider.GetRequiredService<SiteChecker>();

        await Parallel.ForEachAsync(
            dueSites,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxParallelChecks,
                CancellationToken = stoppingToken
            },
            async (site, token) =>
            {
                var result = await checker.CheckAsync(site, token);
                results.Add(result);

                if (!result.IsUp)
                    logger.LogWarning("{Url} is down: {Error}", site.Url, result.ErrorMessage);
            });

        if (results.IsEmpty)
            return;

        await using var writeDb = await dbFactory.CreateDbContextAsync(stoppingToken);
        writeDb.SiteChecks.AddRange(results);
        await writeDb.SaveChangesAsync(stoppingToken);

        logger.LogInformation("Saved {Count} check(s)", results.Count);
    }
}