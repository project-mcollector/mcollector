using Confluent.Kafka;
using Confluent.Kafka.Admin;
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
    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? "Host=postgres;Database=mcollector;Username=app;Password=app";

builder.Services.AddDbContext<IdentityValidationContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
builder.Services.AddScoped<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

builder.Services.AddApiKeyAuthentication();

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

var bootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
app.Logger.LogInformation("Connecting to Kafka at {BootstrapServers}", bootstrapServers);

try
{
    using var adminClient = new AdminClientBuilder(
        new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

    await adminClient.CreateTopicsAsync(new[]
    {
        new TopicSpecification
        {
            Name = "raw-events",
            NumPartitions = 1,
            ReplicationFactor = 1
        }
    });

    app.Logger.LogInformation("Kafka topic 'raw-events' created successfully");
}
catch (CreateTopicsException e) when
    (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
{
    app.Logger.LogInformation("Kafka topic 'raw-events' already exists");
}
catch (Exception e)
{
    app.Logger.LogError(e, "Failed to create Kafka topic");
}

app.UseCors();
app.MapControllers();
app.Run();
