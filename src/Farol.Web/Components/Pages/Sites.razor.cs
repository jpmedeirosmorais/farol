using System.ComponentModel.DataAnnotations;
using Farol.Web.Configuration;
using Farol.Web.Data;
using Farol.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Farol.Web.Components.Pages;

public partial class Sites : ComponentBase
{
    [Inject] private IDbContextFactory<FarolDbContext> DbFactory { get; set; } = default!;
    [Inject] private SiteChecker Checker { get; set; } = default!;
    [Inject] private IOptions<FarolOptions> OptionsAccessor { get; set; } = default!;

    private List<Site>? _sites;
    private NewSiteModel _newSite = new();
    private string? _errorMessage;
    private int? _checkingId;
    private int? _pendingDeleteId;
    private bool _isSubmitting;
    private int _slotsUsed;

    private FarolOptions Options => OptionsAccessor.Value;

    private bool IsFull => _slotsUsed >= Options.MaxActiveSites;

    protected override async Task OnInitializedAsync()
    {
        _newSite.CheckIntervalMinutes = Options.MinCheckIntervalMinutes;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        _sites = await db.Sites
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync();

        _slotsUsed = _sites.Count;
    }

    private async Task AddAsync()
    {
        _errorMessage = null;
        _isSubmitting = true;

        try
        {
            if (!Options.PublicRegistrationEnabled)
            {
                _errorMessage = "Public registration is closed.";
                return;
            }

            if (_newSite.CheckIntervalMinutes < Options.MinCheckIntervalMinutes)
            {
                _errorMessage = $"The minimum interval is {Options.MinCheckIntervalMinutes} minutes.";
                return;
            }

            var normalizedUrl = _newSite.Url.Trim().TrimEnd('/');

            // Defesa contra SSRF. Precisa vir antes de qualquer escrita.
            var rejection = await UrlSafety.ValidateAsync(normalizedUrl);
            if (rejection is not null)
            {
                _errorMessage = rejection;
                return;
            }

            await using var db = await DbFactory.CreateDbContextAsync();

            // Teto global reconferido aqui, e não só na tela: entre carregar a página
            // e enviar o formulário, outra pessoa pode ter ocupado a última vaga.
            if (await db.Sites.CountAsync() >= Options.MaxActiveSites)
            {
                _errorMessage = "The demo is full. Try again later.";
                await LoadAsync();
                return;
            }

            if (await db.Sites.AnyAsync(s => s.Url == normalizedUrl))
            {
                _errorMessage = "That URL is already being monitored.";
                return;
            }

            db.Sites.Add(new Site
            {
                Name = _newSite.Name.Trim(),
                Url = normalizedUrl,
                CheckIntervalMinutes = _newSite.CheckIntervalMinutes,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(Options.DemoSiteLifetimeHours)
            });

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // O índice único barrou uma duplicata que passou pela checagem acima.
                _errorMessage = "That URL is already being monitored.";
                return;
            }

            _newSite = new NewSiteModel { CheckIntervalMinutes = Options.MinCheckIntervalMinutes };
            await LoadAsync();
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async Task CheckNowAsync(Site site)
    {
        _checkingId = site.Id;

        try
        {
            var result = await Checker.CheckAsync(site);

            await using var db = await DbFactory.CreateDbContextAsync();
            db.SiteChecks.Add(result);
            await db.SaveChangesAsync();
        }
        finally
        {
            _checkingId = null;
        }

        await LoadAsync();
    }

    private async Task DeleteAsync(Site site)
    {
        _pendingDeleteId = null;

        await using var db = await DbFactory.CreateDbContextAsync();
        await db.Sites.Where(s => s.Id == site.Id).ExecuteDeleteAsync();

        await LoadAsync();
    }

    private static string ExpiryLabel(Site site)
    {
        if (site.ExpiresAt is null)
            return "permanent";

        var remaining = site.ExpiresAt.Value - DateTimeOffset.UtcNow;

        if (remaining <= TimeSpan.Zero)
            return "expiring";

        return remaining.TotalHours >= 1
            ? $"in {(int)remaining.TotalHours} h"
            : $"in {(int)remaining.TotalMinutes} min";
    }

    public class NewSiteModel
    {
        [Required, StringLength(120)]
        public string Name { get; set; } = "";

        [Required, StringLength(500)]
        [Url(ErrorMessage = "Must be a valid URL, including http:// or https://")]
        public string Url { get; set; } = "";

        [Range(1, 1440)]
        public int CheckIntervalMinutes { get; set; } = 15;
    }
}
