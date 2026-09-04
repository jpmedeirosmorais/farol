using System.Collections.Concurrent;
using Farol.Web.Configuration;
using Farol.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Farol.Web.Services;

public class SiteMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<FarolDbContext> dbFactory,
    IOptions<FarolOptions> options,
    ILogger<SiteMonitorWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    private readonly FarolOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Site monitor started, ticking every {Interval}, up to {Max} checks per cycle",
            TickInterval, _options.MaxChecksPerCycle);

        using var timer = new PeriodicTimer(TickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PruneAsync(stoppingToken);
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Monitoring cycle failed; will retry on next tick");
            }
        }

        logger.LogInformation("Site monitor stopped");
    }

    /// <summary>
    /// Remove sites temporários vencidos e histórico antigo. Roda antes da checagem
    /// pra que um site vencido não seja checado no mesmo ciclo em que expira.
    /// </summary>
    private async Task PruneAsync(CancellationToken stoppingToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);

        var expiredSites = await db.Sites
            .Where(s => s.ExpiresAt != null && s.ExpiresAt <= now)
            .ExecuteDeleteAsync(stoppingToken);

        if (expiredSites > 0)
            logger.LogInformation("Removed {Count} expired site(s)", expiredSites);

        var cutoff = now.AddDays(-_options.CheckRetentionDays);

        var oldChecks = await db.SiteChecks
            .Where(c => c.CheckedAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken);

        if (oldChecks > 0)
            logger.LogInformation("Removed {Count} check(s) older than {Days} days",
                oldChecks, _options.CheckRetentionDays);
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
            .OrderBy(x => x.LastCheckedAt)
            .Select(x => x.Site)
            // O teto que não confia em nada: mesmo com o banco cheio, um ciclo
            // custa no máximo isto. Os que sobrarem entram no ciclo seguinte,
            // e o OrderBy garante que os mais atrasados vão primeiro.
            .Take(_options.MaxChecksPerCycle)
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
                MaxDegreeOfParallelism = 5,
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
