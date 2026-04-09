using Identity.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Infrastructure.Persistence;

public class IdentityAppDbContext(DbContextOptions<IdentityAppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);

            entity.HasMany(e => e.Users)
                  .WithMany(e => e.Projects)
                  .UsingEntity("UserProjects");
        });
    }
}
