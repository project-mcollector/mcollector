using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Auth;

public static class ApiKeyAuthenticationExtensions
{
    /// <summary>
    /// Adds API Key authentication to the services
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The authentication builder for further configuration</returns>
    public static AuthenticationBuilder AddApiKeyAuthentication(this IServiceCollection services)
    {
        return services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
                options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName, null);
    }
}
