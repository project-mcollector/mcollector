using Identity.Api.Infrastructure.Identity;
using Identity.Api.Infrastructure.Persistence;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.Configuration;

var builder = WebApplication.CreateBuilder(args);
var unsupportedIdentityPaths = new[]
    { "/login", "/forgotPassword", "/resetPassword", "/confirmEmail", "/resendConfirmationEmail", "/manage/2fa" };

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        // Hide unsupported Identity endpoints from Swagger/OpenAPI
        foreach (var path in unsupportedIdentityPaths)
            document.Paths.Remove(path);
        return Task.CompletedTask;
    });
});
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddSharedAuthentication(builder.Configuration);

builder.Services.AddDbContext<IdentityAppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                           throw new InvalidConfigurationException("Provide a connection string in configuration");
    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<IdentityAppDbContext>();

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "API v1"); });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Middleware to block access to unsupported Identity endpoints
app.Use(async (context, next) =>
{
    if (unsupportedIdentityPaths.Contains(context.Request.Path.Value, StringComparer.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next(context);
});

app.MapIdentityApi<ApplicationUser>();

app.MapControllers();

app.Run();
