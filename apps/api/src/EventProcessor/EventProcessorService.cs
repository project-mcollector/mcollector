using System.Data.Common;
using System.Diagnostics;
using Contracts.Messages;
using Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EventProcessor.Contracts;

namespace EventProcessor;

public class EventProcessorService : IEventConsumer<RawEvent>
{
    private readonly ILogger<EventProcessorService> logger;
    private readonly IProcessedEventRepository repository;

    public EventProcessorService(
        ILogger<EventProcessorService> logger
        , IProcessedEventRepository repository)
    {
        this.logger = logger;
        this.repository = repository;
    }

    public async Task ConsumeAsync(RawEvent message, CancellationToken cancellationToken = default)
    {
        var validatorContext = new ValidationContext(message);
        var validationList = new List<ValidationResult>();
        if (!Validator.TryValidateObject(message, validatorContext, validationList, true))
        {
            logger.LogWarning(
                "Dropping invalid event {EventId} for project {ProjectId}. Errors: {Errors}",
                message.EventId, message.ProjectId,
                string.Join(", ", validationList.Select(x => x.ErrorMessage)));
            return;
        }

        var processedEvent = new ProcessedEvent
        {
            EventId = message.EventId,
            ProjectId = message.ProjectId,
            EventName = message.EventName,
            Timestamp = message.ClientTimestamp != default ? message.ClientTimestamp : message.ServerTimestamp,
            UserId = message.UserId,
            SessionId = message.SessionId,
            PropertiesJson = message.Properties is null ? null : JsonSerializer.Serialize(message.Properties),
            EventCountry = null,
            EventBrowser = null,
            ProcessedAt = DateTimeOffset.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            await repository.AddAsync(processedEvent, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            logger.LogWarning("Duplicate event {EventId} for project {ProjectId} — skipping", message.EventId, message.ProjectId);
            return;
        }
        sw.Stop();

        logger.LogInformation(
            "DB write: event {EventId} '{EventName}' for project {ProjectId} in {ElapsedMs}ms",
            message.EventId, message.EventName, message.ProjectId, sw.ElapsedMilliseconds);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        return ex.InnerException is DbException dbEx
            && (dbEx.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || dbEx.Message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || dbEx.Message.Contains("23505"));
    }
}
