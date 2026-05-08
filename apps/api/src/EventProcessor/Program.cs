using Contracts.Messages;
using EventProcessor;
using EventProcessor.Contracts;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<EventProcessorDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IProcessedEventRepository, EventRepository>();
builder.Services.AddScoped<IEventConsumer<RawEvent>, EventProcessorService>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<EventProcessorDbContext>();
    dbContext.Database.Migrate();
}

host.Run();
