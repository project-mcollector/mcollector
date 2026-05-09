using Contracts.Messages;
using Infrastructure.Auth;
using Infrastructure.Messaging;
using Ingestion.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured");

builder.Services.AddDbContext<IdentityValidationContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

builder.Services.AddApiKeyAuthentication();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
