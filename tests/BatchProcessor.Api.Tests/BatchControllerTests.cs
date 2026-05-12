using System.Net;
using System.Net.Http.Json;
using BatchProcessor.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Tests;

public class BatchControllerTests
{
    [Fact]
    public async Task Start_ReturnsAcceptedWithJobId()
    {
        await using var factory = new BatchProcessorApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/batch/start", new StartBatchRequest
        {
            JobName = "daily-import",
            TotalItems = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var payload = await response.Content.ReadFromJsonAsync<StartJobResponse>();
        payload.Should().NotBeNull();
        payload!.JobId.Should().Be(BatchProcessorApiFactory.JobId);
    }

    [Fact]
    public async Task GetStatus_ReturnsOkWithExpectedShape()
    {
        await using var factory = new BatchProcessorApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/batch/{BatchProcessorApiFactory.JobId}/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var job = await response.Content.ReadFromJsonAsync<BatchJob>();
        job.Should().BeEquivalentTo(new
        {
            JobId = BatchProcessorApiFactory.JobId,
            JobName = "daily-import",
            Status = BatchJobStatus.Completed,
            TotalItems = 3,
            ProcessedItems = 3,
            PartitionKey = BatchProcessorApiFactory.JobId
        });
    }

    private sealed record StartJobResponse(string JobId);

    private sealed class BatchProcessorApiFactory : WebApplicationFactory<Program>
    {
        public const string JobId = "11111111111111111111111111111111";

        public BatchProcessorApiFactory()
        {
            Environment.SetEnvironmentVariable(
                "APPLICATIONINSIGHTS_CONNECTION_STRING",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000");
            Environment.SetEnvironmentVariable("EventHub__Name", "progress");
            Environment.SetEnvironmentVariable(
                "EventHub__ConnectionString",
                "Endpoint=sb://localhost/;SharedAccessKeyName=test;SharedAccessKey=test");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IBatchJobService>();
                services.RemoveAll<BatchJobService>();
                services.AddSingleton<IBatchJobService, FakeBatchJobService>();
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("EventHub__Name", null);
            Environment.SetEnvironmentVariable("EventHub__ConnectionString", null);
            base.Dispose(disposing);
        }
    }

    private sealed class FakeBatchJobService : IBatchJobService
    {
        private readonly BatchJob _job = new()
        {
            Id = BatchProcessorApiFactory.JobId,
            JobId = BatchProcessorApiFactory.JobId,
            JobName = "daily-import",
            Status = BatchJobStatus.Completed,
            TotalItems = 3,
            ProcessedItems = 3,
            CreatedAt = DateTimeOffset.UtcNow,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow,
            PartitionKey = BatchProcessorApiFactory.JobId
        };

        public Task<string> StartJobAsync(StartBatchRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_job.JobId);
        }

        public BatchJob? GetJob(string jobId)
        {
            return jobId == _job.JobId ? _job : null;
        }
    }
}
