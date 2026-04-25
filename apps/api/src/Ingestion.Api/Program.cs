using Contracts.Messages;
using Infrastructure.Messaging;
using Ingestion.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()));

builder.Services.AddScoped<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IIngestionService, IngestionService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();
app.Run();
