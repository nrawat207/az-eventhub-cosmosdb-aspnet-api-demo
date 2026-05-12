using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly Container _container;

    public HealthController(CosmosClient cosmosClient, IConfiguration configuration)
    {
        var databaseName = GetRequiredConfigurationValue(configuration, "CosmosDb:DatabaseName");
        var containerName = GetRequiredConfigurationValue(configuration, "CosmosDb:ContainerName");

        _container = cosmosClient.GetContainer(databaseName, containerName);
    }

    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy" });
    }

    [HttpGet("/api/jobs/{jobId}")]
    [ProducesResponseType(typeof(BatchJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJob(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<BatchJob>(
                jobId,
                new PartitionKey(jobId),
                cancellationToken: cancellationToken);

            return Ok(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }

    [HttpGet("/api/jobs")]
    [ProducesResponseType(typeof(IReadOnlyList<BatchJob>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP 20 * FROM c
            ORDER BY c.createdAt DESC
            """;

        var jobs = new List<BatchJob>();
        using var iterator = _container.GetItemQueryIterator<BatchJob>(
            new QueryDefinition(sql),
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = 20
            });

        while (iterator.HasMoreResults && jobs.Count < 20)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            jobs.AddRange(response);
        }

        return Ok(jobs.Take(20));
    }

    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} configuration is required.");
    }
}
