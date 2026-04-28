using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Auth;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";
    public const string SchemeName = "ApiKey";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ProjectIdHeaderName = "X-Project-Id";
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IApiKeyValidator _apiKeyValidator;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator apiKeyValidator)
        : base(options, logger, encoder)
    {
        _apiKeyValidator = apiKeyValidator;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ProjectIdHeaderName, out var projectIdHeader))
        {
            return AuthenticateResult.Fail($"Missing header: {ProjectIdHeaderName}");
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyHeader))
        {
            return AuthenticateResult.Fail($"Missing header: {ApiKeyHeaderName}");
        }

        if (!Guid.TryParse(projectIdHeader.ToString(), out var projectId))
        {
            return AuthenticateResult.Fail($"Invalid {ProjectIdHeaderName} format");
        }

        var apiKey = apiKeyHeader.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail($"{ApiKeyHeaderName} cannot be empty");
        }

        try
        {
            var isValid = await _apiKeyValidator.ValidateApiKeyAsync(projectId, apiKey);
            if (!isValid)
            {
                return AuthenticateResult.Fail("Invalid API key or project");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating API key");
            return AuthenticateResult.Fail("An error occurred during validation");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, projectId.ToString()),
            new Claim("project_id", projectId.ToString()),
            new Claim("api_key_auth", "true")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
