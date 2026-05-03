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
    private readonly ILogger<ApiKeyValidator> _logger;

    public ApiKeyValidator(IdentityValidationContext context, ILogger<ApiKeyValidator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> ValidateApiKeyAsync(Guid projectId, string apiKey)
    {
        if (projectId == Guid.Empty || string.IsNullOrWhiteSpace(apiKey))
            return false;

        return await _context.Projects
            .AsNoTracking()
            .AnyAsync(p => p.Id == projectId && p.ApiKey == apiKey);
    }

    public async Task<Guid?> GetProjectIdByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("GetProjectIdByApiKey called with empty key");
            return null;
        }

        var project = await _context.Projects
            .AsNoTracking()
            .Select(p => new { p.Id, p.ApiKey })
            .FirstOrDefaultAsync(p => p.ApiKey == apiKey, cancellationToken);

        if (project is null)
            _logger.LogWarning("Invalid write key presented");
        else
            _logger.LogDebug("Write key resolved to project {ProjectId}", project.Id);

        return project?.Id;
    }
}
