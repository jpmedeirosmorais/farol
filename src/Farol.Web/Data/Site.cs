namespace Farol.Web.Data;

public class Site
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public bool IsActive { get; set; } =  true;
    public int CheckIntervalMinutes { get; set; } = 15;
    public DateTimeOffset? CreatedAt { get; set; } =  DateTimeOffset.UtcNow;

    public List<SiteCheck> Checks { get; set; } = [];
}