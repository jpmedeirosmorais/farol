namespace Farol.Web.Data;

public class SiteCheck
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site? Site { get; set; }

    public DateTimeOffset CheckedAt { get; set; }
    public bool IsUp { get; set; }
    public int? StatusCode { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTimeOffset? SslExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
}