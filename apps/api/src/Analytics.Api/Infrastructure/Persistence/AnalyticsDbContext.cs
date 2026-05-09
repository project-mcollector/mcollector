using Contracts.Messages;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Api.Infrastructure.Persistence;

// Read-only projection of the Identity UserProjects join table
public class UserProject
{
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ProcessedEvent> ProcessedEvents { get; set; }
    public DbSet<UserProject> UserProjects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.ToTable("processed_events");
            entity.HasKey(e => e.EventId);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.EventName);
            entity.Property(e => e.EventName).IsRequired();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.PropertiesJson).HasColumnType("jsonb");
            entity.Ignore(e => e.Properties);
        });

        modelBuilder.Entity<UserProject>(entity =>
        {
            entity.ToTable("UserProjects");
            entity.HasKey(e => new { e.ProjectId, e.UserId });
            entity.Property(e => e.ProjectId).HasColumnName("ProjectsId");
            entity.Property(e => e.UserId).HasColumnName("UsersId");
        });
    }
}
