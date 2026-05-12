using System.Collections;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Tests;

public class HealthControllerTests
{
    [Fact]
    public async Task GetJobs_ReturnsOk()
    {
        await using var factory = new ProgressReceiverApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/jobs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private sealed class ProgressReceiverApiFactory : WebApplicationFactory<Program>
    {
        public ProgressReceiverApiFactory()
        {
            Environment.SetEnvironmentVariable(
                "APPLICATIONINSIGHTS_CONNECTION_STRING",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000");
            Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", "https://localhost:8081/");
            Environment.SetEnvironmentVariable("CosmosDb__AccountKey", "test-key");
            Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", "batch-db");
            Environment.SetEnvironmentVariable("CosmosDb__ContainerName", "batch-progress");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<CosmosClient>();
                services.AddSingleton(CreateCosmosClient());
            });
        }

        protected override void Dispose(bool disposing)
        {
            Environment.SetEnvironmentVariable("CosmosDb__AccountEndpoint", null);
            Environment.SetEnvironmentVariable("CosmosDb__AccountKey", null);
            Environment.SetEnvironmentVariable("CosmosDb__DatabaseName", null);
            Environment.SetEnvironmentVariable("CosmosDb__ContainerName", null);
            base.Dispose(disposing);
        }

        private static CosmosClient CreateCosmosClient()
        {
            var iterator = new Mock<FeedIterator<BatchJob>>();
            iterator.SetupSequence(i => i.HasMoreResults)
                .Returns(true)
                .Returns(false);
            iterator
                .Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateFeedResponse(Array.Empty<BatchJob>()));

            var container = new Mock<Container>();
            container
                .Setup(c => c.GetItemQueryIterator<BatchJob>(
                    It.IsAny<QueryDefinition>(),
                    null,
                    It.IsAny<QueryRequestOptions>()))
                .Returns(iterator.Object);

            var cosmosClient = new Mock<CosmosClient>();
            cosmosClient
                .Setup(client => client.GetContainer("batch-db", "batch-progress"))
                .Returns(container.Object);

            return cosmosClient.Object;
        }

        private static FeedResponse<BatchJob> CreateFeedResponse(IReadOnlyList<BatchJob> jobs)
        {
            var response = new Mock<FeedResponse<BatchJob>>();
            response.Setup(r => r.Count).Returns(jobs.Count);
            response.As<IEnumerable<BatchJob>>()
                .Setup(r => r.GetEnumerator())
                .Returns(() => jobs.GetEnumerator());
            response.As<IEnumerable>()
                .Setup(r => r.GetEnumerator())
                .Returns(() => jobs.GetEnumerator());

            return response.Object;
        }
    }
}
