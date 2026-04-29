using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;

namespace Ingestion.Api.Services;

/// <summary>
/// DbContext for validating API keys from Identity.Api
/// </summary>
public class IdentityValidationContext : DbContext
{
    public IdentityValidationContext(DbContextOptions<IdentityValidationContext> options) : base(options)
    {
    }

    public DbSet<ProjectForValidation> Projects => Set<ProjectForValidation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProjectForValidation>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.ToTable("Projects");
        });
    }
}

/// <summary>
/// Lightweight project model for API key validation purposes
/// </summary>
public class ProjectForValidation
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Validates API keys against the Identity database
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IdentityValidationContext _context;

    public ApiKeyValidator(IdentityValidationContext context)
    {
        _context = context;
    }

    public async Task<bool> ValidateApiKeyAsync(Guid projectId, string apiKey)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(apiKey))
            return false;

        // Получаем секрет из конфига (или внедряем через DI)
        var configuration = _context.GetService<IConfiguration>();
        var hmacSecret = configuration["ApiKey:HmacSecret"];
        var apiKeyHash = Identity.Api.Application.Services.ApiKeyService.ComputeApiKeyHashStatic(apiKey, hmacSecret);

        return await _context.Projects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.ApiKey == apiKeyHash);
    }

    public async Task<Guid?> GetProjectIdByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var project = await _context.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.ApiKey })
            .FirstOrDefaultAsync(p => p.ApiKey == apiKey, cancellationToken);

        return project?.Id;
    }
}
