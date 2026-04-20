using Identity.Api.Infrastructure.Identity;
using Identity.Api.Infrastructure.Persistence;
using Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

var unsupportedIdentityPaths = new[]
    { "/forgotPassword", "/resetPassword", "/confirmEmail", "/resendConfirmationEmail", "/manage/2fa" };

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
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
                           ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                           ?? "Host=postgres;Database=mcollector;Username=app;Password=app";
    }

    options.UseNpgsql(connectionString);
});

builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<IdentityAppDbContext>();

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityAppDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => { options.SwaggerEndpoint("/openapi/v1.json", "API v1"); });
}

app.UseHttpsRedirection();

app.UseCors();

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
