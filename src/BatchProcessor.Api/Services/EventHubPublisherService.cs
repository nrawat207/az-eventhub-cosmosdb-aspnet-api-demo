using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Services;

public class EventHubPublisherService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EventHubProducerClient _producerClient;
    private readonly ILogger<EventHubPublisherService> _logger;

    public EventHubPublisherService(
        EventHubProducerClient producerClient,
        ILogger<EventHubPublisherService> logger)
    {
        _producerClient = producerClient;
        _logger = logger;
    }

    public async Task PublishAsync(BatchProgressEvent evt, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(evt, JsonOptions);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _producerClient.SendAsync([CreateEventData(evt, payload)], cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));
                _logger.LogWarning(
                    ex,
                    "Failed publishing progress event for job {JobId} on attempt {Attempt}. Retrying in {DelayMs} ms.",
                    evt.JobId,
                    attempt,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static EventData CreateEventData(BatchProgressEvent evt, string payload)
    {
        return new EventData(BinaryData.FromString(payload))
        {
            MessageId = $"{evt.JobId}:{evt.ProcessedItems}",
            CorrelationId = evt.JobId
        };
    }
}
