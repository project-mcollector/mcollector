using System;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Api.Application.Services;

public interface IApiKeyService
{
    string GenerateApiKey();
    string ComputeApiKeyHash(string key);
}

public class ApiKeyService : IApiKeyService
{
    /// <summary>
    /// Generates a cryptographically secure API key
    /// Format: "proj_" + 32 random characters (base64 URL-safe)
    /// </summary>
    /// <returns>A new API key string</returns>
    private readonly string _hmacSecret;
    public ApiKeyService(IConfiguration configuration)
    {
        _hmacSecret = configuration["ApiKey:HmacSecret"];
    }

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

    public string ComputeApiKeyHash(string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hashBytes);
    }

    // Для валидации из других сервисов (например, Ingestion.Api)
    public static string ComputeApiKeyHashStatic(string key, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hashBytes);
    }
}
