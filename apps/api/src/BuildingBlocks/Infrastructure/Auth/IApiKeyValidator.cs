namespace Infrastructure.Auth;

public interface IApiKeyValidator
{
    Task<bool> ValidateApiKeyAsync(Guid projectId, string apiKey);
    Task<Guid?> GetProjectIdByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
}
