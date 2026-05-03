using Contracts.Messages;
using Microsoft.EntityFrameworkCore;

namespace EventProcessor;

public class EventProcessorDbContext(DbContextOptions<EventProcessorDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Ignore(e => e.Properties);
            entity.Property(e => e.PropertiesJson).HasColumnType("jsonb");
            entity.ToTable("processed_events");
        });
    }
}
