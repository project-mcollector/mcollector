using Contracts.Messages;
using Infrastructure.Messaging;
using System.ComponentModel.DataAnnotations;
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
                "Invalid event {EventId}. Errors: {Errors}",
                message.EventId,
                string.Join(", ", validationList.Select(x => x.ErrorMessage))
                );
            return;
        }

        var isDuplicate = await repository.ExistsByEventIdAsync(message.EventId, cancellationToken);
        if (isDuplicate)
        {
            logger.LogInformation("Event {EventId} was already processed. Skipping.", message.EventId);
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
            Properties = message.Properties,

            // We are not parsing IP and UserAgent yet, so we leave them empty or null:
            EventCountry = null,
            EventBrowser = null,
            // etc. (other optional properties)

            ProcessedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(processedEvent, cancellationToken);

        logger.LogInformation("Successfully processed event {EventId}", message.EventId);
    }
}
