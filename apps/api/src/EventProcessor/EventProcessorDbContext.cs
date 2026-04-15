using Contracts.Messages;
using Microsoft.EntityFrameworkCore;

namespace EventProcessor;

public class EventProcessorDbContext(DbContextOptions<EventProcessorDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Ignore(e => e.Properties);

            entity.ToTable("processed_events");
        });
    }
}
