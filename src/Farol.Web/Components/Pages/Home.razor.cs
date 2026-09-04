using Farol.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace Farol.Web.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject]
    private IDbContextFactory<FarolDbContext> DbFactory { get; set; } = default!;

    private List<SiteStatus>? _rows;
    private PeriodicTimer? _refreshTimer;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    // O timer só é criado no render interativo. Em OnInitializedAsync ele nasceria
    // duas vezes, porque a prerenderização executa a inicialização antes do circuito.
    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender) return;

        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        _ = AutoRefreshAsync();
    }

    private async Task LoadAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();

        _rows = await db.Sites
            .OrderBy(s => s.Name)
            .Select(s => new SiteStatus(
                s.Id,
                s.Name,
                s.Url,
                s.IsActive,
                s.Checks.OrderByDescending(c => c.CheckedAt).FirstOrDefault()))
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task AutoRefreshAsync()
    {
        try
        {
            while (await _refreshTimer!.WaitForNextTickAsync())
            {
                await LoadAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // A página foi fechada. Encerramento normal.
        }
    }

    public void Dispose() => _refreshTimer?.Dispose();

    private static string DownLabel(SiteCheck check) =>
        check.StatusCode is int code
            ? $"HTTP {code}"
            : check.ErrorMessage ?? "down";

    private static int DaysUntil(DateTimeOffset expiresAt) =>
        (int)(expiresAt - DateTimeOffset.UtcNow).TotalDays;

    private static string CertificateCss(DateTimeOffset expiresAt) => DaysUntil(expiresAt) switch
    {
        < 0 => "text-danger fw-semibold",
        < 15 => "text-warning fw-semibold",
        _ => "text-muted"
    };

    private static string CertificateText(DateTimeOffset expiresAt)
    {
        var days = DaysUntil(expiresAt);
        return days < 0 ? $"expired {-days} d ago" : $"{days} d left";
    }

    private static string ResponseLabel(SiteCheck? check) =>
        check is null ? "—" : $"{check.ResponseTimeMs} ms";

    private static string Relative(DateTimeOffset? when)
    {
        if (when is null) return "—";

        var elapsed = DateTimeOffset.UtcNow - when.Value;

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes} min ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} h ago",
            _ => $"{(int)elapsed.TotalDays} d ago"
        };
    }

    public record SiteStatus(int Id, string Name, string Url, bool IsActive, SiteCheck? LastCheck);
}
