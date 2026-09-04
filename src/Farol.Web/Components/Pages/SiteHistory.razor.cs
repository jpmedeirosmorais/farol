using Farol.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace Farol.Web.Components.Pages;

public partial class SiteHistory : ComponentBase
{
    private const int MaxChecksShown = 50;

    [Inject]
    private IDbContextFactory<FarolDbContext> DbFactory { get; set; } = default!;

    [Parameter]
    public int Id { get; set; }

    private Site? _site;
    private List<SiteCheck> _checks = [];
    private bool _loading = true;

    private string PageName => _site?.Name ?? "Site";

    protected override async Task OnInitializedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        _site = await db.Sites
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == Id);

        if (_site is not null)
        {
            _checks = await db.SiteChecks
                .Where(c => c.SiteId == Id)
                .OrderByDescending(c => c.CheckedAt)
                .Take(MaxChecksShown)
                .AsNoTracking()
                .ToListAsync();
        }

        _loading = false;
    }

    private static string LocalTime(DateTimeOffset when) =>
        when.ToLocalTime().ToString("dd/MM HH:mm:ss");

    private static string CertificateLabel(SiteCheck check) =>
        check.SslExpiresAt is null
            ? "—"
            : check.SslExpiresAt.Value.ToLocalTime().ToString("dd/MM/yyyy");

    private static string DetailLabel(SiteCheck check)
    {
        if (check.ErrorMessage is not null)
            return check.ErrorMessage;

        return check.StatusCode is int code ? $"HTTP {code}" : "—";
    }
}
