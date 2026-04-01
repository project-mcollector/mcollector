namespace Contracts;

/// <summary>
/// Интерфейс сервиса приёма событий от JS SDK.
/// Отвечает за получение сырых событий и передачу их в шину событий (Kafka),
/// откуда их заберёт Event Processor для дальнейшей обработки.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// Принимает одно событие от JS SDK и отправляет его в шину событий.
    /// Вызывается когда SDK шлёт POST /api/v1/ingest/event
    /// </summary>
    /// <param name="dto">Данные события — тип, пользователь, свойства и т.д.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    Task IngestAsync(IncomingEventDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Принимает пачку событий от JS SDK и отправляет их в шину событий.
    /// Вызывается когда SDK шлёт POST /api/v1/ingest/batch (до 50 событий за раз).
    /// </summary>
    /// <param name="dtos">Коллекция событий.</param>
    /// <param name="cancellationToken">Токен отмены запроса.</param>
    Task IngestBatchAsync(IEnumerable<IncomingEventDto> dtos, CancellationToken cancellationToken = default);
}
