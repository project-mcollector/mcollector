using Contracts.Messages;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Api.Infrastructure.Persistence;

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventName);
            entity.Property(e => e.EventName).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.PropertiesJson).HasColumnType("jsonb");
        });
    }
}
