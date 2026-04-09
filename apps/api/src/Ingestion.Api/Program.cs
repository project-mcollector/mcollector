using Contracts.Messages;
using Ingestion.Api.Services;
using Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Allow the JS SDK to send requests from any origin
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

// Stub publisher — replace with Kafka implementation when available
builder.Services.AddScoped<IEventPublisher, StubEventPublisher>();

// Register the ingestion service
builder.Services.AddScoped<IIngestionService, IngestionService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
