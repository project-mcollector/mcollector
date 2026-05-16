using Identity.Api.Application.Options;
using Identity.Api.Application.Services;
using Identity.Api.Domain.Entities;
using Identity.Api.Infrastructure.HealthChecks;
using Identity.Api.Infrastructure.Persistence;
using Infrastructure.Auth;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Resend;
using Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("config/appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"config/appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddOpenApi();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddSharedAuthentication(builder.Configuration);

builder.Services.AddAuthentication()
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddCookie(IdentityConstants.ExternalScheme)
    .AddCookie(IdentityConstants.TwoFactorRememberMeScheme)
    .AddCookie(IdentityConstants.TwoFactorUserIdScheme);

builder.Services.AddDbContext<IdentityAppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                           ?? throw new InvalidOperationException(
                               "DefaultConnection is not configured");

    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
        options.SignIn.RequireConfirmedEmail = true;
        options.Stores.SchemaVersion =
            IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<IdentityAppDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager();

builder.Services.Configure<IdentityPasskeyOptions>(options =>
{
    options.ServerDomain = builder.Configuration["Passkey:ServerDomain"]
                           ?? throw new InvalidOperationException("Passkey:ServerDomain is not configured");
    options.AuthenticatorTimeout =
        TimeSpan.FromMinutes(
            TryParseWithDefault(builder.Configuration["Passkey:AuthenticatorTimeoutMinutes"],
                3));
    options.ChallengeSize = TryParseWithDefault(
        builder.Configuration["Passkey:ChallengeSize"],
        64);
    options.UserVerificationRequirement = builder.Configuration["Passkey:UserVerificationRequirement"]
                                          ?? "required";
});

builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<IMailService, MailService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IPasskeyService, PasskeyService>();
builder.Services.AddScoped<IProjectsService, ProjectsService>();
builder.Services.AddScoped<IUsersService, UsersService>();

builder.Services.AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MailOptions>()
    .Bind(builder.Configuration)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<ResendClient>();
builder.Services.Configure<ResendClientOptions>(o =>
{
    o.ApiToken = builder.Configuration["RESEND_APITOKEN"] ?? throw new InvalidOperationException(
        "Resend API Token is not set");
});
builder.Services.AddTransient<IResend, ResendClient>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
builder.Services.AddControllers();

builder.Services.AddRateLimiter(options =>
{
    // IP-based fixed window for unauthenticated auth endpoints
    options.AddPolicy("auth", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            ip,
            _ => new()
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // User ID-based sliding window for authenticated API endpoints
    options.AddPolicy("api", context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.Connection.RemoteIpAddress?.ToString()
                     ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(
            userId,
            _ => new()
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration["AllowedOrigins"]?.Split(',')
                      ?? ["http://localhost:3000"];
        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAppDbContext>();
    if (dbContext.Database.IsRelational())
        dbContext.Database.Migrate();
    else
        dbContext.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "IdentityApi v1"); });
}

app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});
app.MapControllers();

app.Run();
return;

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var result = JsonSerializer.Serialize(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description
        })
    }, JsonSerializerOptions.Web);
    return context.Response.WriteAsync(result);
}

static int TryParseWithDefault(string? value, int defaultValue)
    => int.TryParse(value, out var result)
        ? result
        : defaultValue;
