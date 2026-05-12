using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ProgressReceiver.Api.Repositories;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Tests;

public class CosmosDbBatchProgressRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_UpsertsMappedJobWithJobIdPartitionKey()
    {
        BatchJob? upsertedJob = null;
        PartitionKey? partitionKey = null;

        var container = new Mock<Container>();
        container
            .Setup(c => c.ReadItemAsync<BatchJob>(
                "job-123",
                It.Is<PartitionKey>(pk => pk.ToString().Contains("job-123")),
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Not found", HttpStatusCode.NotFound, 0, "activity-id", 0));

        container
            .Setup(c => c.UpsertItemAsync(
                It.IsAny<BatchJob>(),
                It.IsAny<PartitionKey?>(),
                null,
                It.IsAny<CancellationToken>()))
            .Callback<BatchJob, PartitionKey?, ItemRequestOptions?, CancellationToken>((job, pk, _, _) =>
            {
                upsertedJob = job;
                partitionKey = pk;
            })
            .ReturnsAsync((ItemResponse<BatchJob>)null!);

        var cosmosClient = new Mock<CosmosClient>();
        cosmosClient
            .Setup(client => client.GetContainer("batch-db", "batch-progress"))
            .Returns(container.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CosmosDb:DatabaseName"] = "batch-db",
                ["CosmosDb:ContainerName"] = "batch-progress"
            })
            .Build();

        var repository = new CosmosDbBatchProgressRepository(
            cosmosClient.Object,
            configuration,
            NullLogger<CosmosDbBatchProgressRepository>.Instance);

        var progressEvent = new BatchProgressEvent
        {
            JobId = "job-123",
            JobName = "daily-import",
            ProcessedItems = 5,
            TotalItems = 10,
            Status = BatchJobStatus.Running,
            Timestamp = DateTimeOffset.UtcNow,
            NodeId = "test-node"
        };

        await repository.UpsertAsync(progressEvent, CancellationToken.None);

        upsertedJob.Should().BeEquivalentTo(new
        {
            Id = "job-123",
            JobId = "job-123",
            JobName = "daily-import",
            Status = BatchJobStatus.Running,
            ProcessedItems = 5,
            TotalItems = 10,
            PartitionKey = "job-123"
        });
        upsertedJob!.StartedAt.Should().Be(progressEvent.Timestamp);
        partitionKey.Should().NotBeNull();
        partitionKey!.Value.ToString().Should().Contain("job-123");

        container.Verify(c => c.UpsertItemAsync(
            It.Is<BatchJob>(job => job.JobId == "job-123"),
            It.Is<PartitionKey?>(pk => pk.HasValue && pk.Value.ToString().Contains("job-123")),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
