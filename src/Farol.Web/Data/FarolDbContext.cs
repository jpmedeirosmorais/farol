using Microsoft.EntityFrameworkCore;

namespace Farol.Web.Data;

public class FarolDbContext(DbContextOptions<FarolDbContext> options)
    : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SiteCheck> SiteChecks => Set<SiteCheck>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Site>(site =>
        {
            site.Property(s => s.Name).HasMaxLength(120);
            site.Property(s => s.Url).HasMaxLength(500);
            site.HasIndex(s => s.Url).IsUnique();
        });

        builder.Entity<SiteCheck>(check =>
        {
            check.Property(c => c.ErrorMessage).HasMaxLength(1000);
            check.HasIndex(c => new { c.SiteId, c.CheckedAt });

            check.HasOne(c => c.Site)
                .WithMany(s => s.Checks)
                .HasForeignKey(c => c.SiteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}