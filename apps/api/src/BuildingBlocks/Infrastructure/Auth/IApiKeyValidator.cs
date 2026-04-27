namespace Infrastructure.Auth;

/// <summary>
/// Service for validating API keys against projects
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates that an API key belongs to a specific project
    /// </summary>
    /// <param name="projectId">The project ID to validate against</param>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateApiKeyAsync(Guid projectId, string apiKey);
}
