using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using BatchProcessor.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Tests;

public class EventHubPublisherServiceTests
{
    [Fact]
    public async Task PublishAsync_SerializesEvent_AndSendsEventData()
    {
        IReadOnlyList<EventData>? sentEvents = null;
        var producerClient = new Mock<EventHubProducerClient>();
        producerClient
            .Setup(client => client.SendAsync(
                It.IsAny<IEnumerable<EventData>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<EventData>, CancellationToken>((events, _) =>
            {
                sentEvents = events.ToList();
            })
            .Returns(Task.CompletedTask);

        var service = new EventHubPublisherService(
            producerClient.Object,
            NullLogger<EventHubPublisherService>.Instance);

        var progressEvent = new BatchProgressEvent
        {
            JobId = "job-123",
            JobName = "daily-import",
            ProcessedItems = 7,
            TotalItems = 10,
            Status = BatchJobStatus.Running,
            Timestamp = DateTimeOffset.UtcNow,
            NodeId = "test-node"
        };

        await service.PublishAsync(progressEvent);

        producerClient.Verify(
            client => client.SendAsync(
                It.IsAny<IEnumerable<EventData>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        sentEvents.Should().ContainSingle();
        var sentEvent = sentEvents![0];
        sentEvent.MessageId.Should().Be("job-123:7");
        sentEvent.CorrelationId.Should().Be("job-123");

        var serialized = sentEvent.EventBody.ToString();
        var deserialized = JsonSerializer.Deserialize<BatchProgressEvent>(
            serialized,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        deserialized.Should().BeEquivalentTo(progressEvent);
    }
}
