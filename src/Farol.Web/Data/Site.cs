namespace Farol.Web.Data;

public class Site
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Url { get; set; }
    public bool IsActive { get; set; } = true;
    public int CheckIntervalMinutes { get; set; } = 15;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Quando preenchido, o site é temporário e será removido nesta data.
    /// Sites cadastrados publicamente têm prazo; os permanentes ficam nulos.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public List<SiteCheck> Checks { get; set; } = [];
}
