using System.Security.Cryptography;

namespace Identity.Api.Application.Services;

public interface IApiKeyService
{
    string GenerateApiKey();
}

public class ApiKeyService : IApiKeyService
{
    /// <summary>
    /// Generates a cryptographically secure API key
    /// Format: "proj_" + 32 random characters (base64 URL-safe)
    /// </summary>
    /// <returns>A new API key string</returns>
    public string GenerateApiKey()
    {
        const int keyLength = 32;
        var randomBytes = new byte[keyLength];

        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var base64 = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        return $"proj_{base64}";
    }
}
