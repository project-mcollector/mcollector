using Contracts.Messages;
using EventProcessor;
using EventProcessor.Contracts;
using EventProcessor.HealthChecks;
using Infrastructure.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<Worker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found");
builder.Services.AddDbContext<EventProcessorDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IProcessedEventRepository, EventRepository>();
builder.Services.AddScoped<IEventConsumer<RawEvent>, EventProcessorService>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<KafkaHealthCheck>("kafka", tags: ["ready"]);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EventProcessorDbContext>();
    dbContext.Database.Migrate();
}

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});

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
