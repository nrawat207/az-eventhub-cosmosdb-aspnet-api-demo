using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using BatchProcessor.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Tests;

public class BatchJobServiceTests
{
    [Fact]
    public async Task StartJobAsync_ReturnsValidJobId_AndTransitionsToCompleted()
    {
        var producerClient = new Mock<EventHubProducerClient>();
        producerClient
            .Setup(client => client.SendAsync(
                It.IsAny<IEnumerable<EventData>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new EventHubPublisherService(
            producerClient.Object,
            NullLogger<EventHubPublisherService>.Instance);

        using var service = new BatchJobService(
            publisher,
            NullLogger<BatchJobService>.Instance);

        var jobId = await service.StartJobAsync(new StartBatchRequest
        {
            JobName = "daily-import",
            TotalItems = 2
        });

        jobId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParseExact(jobId, "N", out _).Should().BeTrue();

        service.GetJob(jobId).Should().BeEquivalentTo(new
        {
            JobId = jobId,
            Status = BatchJobStatus.Pending,
            ProcessedItems = 0,
            TotalItems = 2
        });

        await service.StartAsync(CancellationToken.None);

        var runningJob = await WaitForJobStatusAsync(service, jobId, BatchJobStatus.Running);
        runningJob.StartedAt.Should().NotBeNull();

        var completedJob = await WaitForJobStatusAsync(service, jobId, BatchJobStatus.Completed);
        completedJob.ProcessedItems.Should().Be(2);
        completedJob.CompletedAt.Should().NotBeNull();

        await service.StopAsync(CancellationToken.None);
    }

    private static async Task<BatchJob> WaitForJobStatusAsync(
        BatchJobService service,
        string jobId,
        BatchJobStatus expectedStatus)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!timeout.IsCancellationRequested)
        {
            var job = service.GetJob(jobId);
            if (job?.Status == expectedStatus)
            {
                return job;
            }

            await Task.Delay(10, timeout.Token);
        }

        throw new TimeoutException($"Job '{jobId}' did not reach status '{expectedStatus}'.");
    }
}
