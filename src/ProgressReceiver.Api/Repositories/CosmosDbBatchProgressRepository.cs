using System.Net;
using Microsoft.Azure.Cosmos;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Repositories;

public class CosmosDbBatchProgressRepository : IBatchProgressRepository
{
    private const int MaxThrottleRetries = 3;

    private readonly Container _container;
    private readonly ILogger<CosmosDbBatchProgressRepository> _logger;

    public CosmosDbBatchProgressRepository(
        CosmosClient cosmosClient,
        IConfiguration configuration,
        ILogger<CosmosDbBatchProgressRepository> logger)
    {
        var databaseName = GetRequiredConfigurationValue(configuration, "CosmosDb:DatabaseName");
        var containerName = GetRequiredConfigurationValue(configuration, "CosmosDb:ContainerName");

        _container = cosmosClient.GetContainer(databaseName, containerName);
        _logger = logger;
    }

    public async Task UpsertAsync(BatchProgressEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        await ExecuteWithThrottleRetryAsync(async () =>
        {
            var existingJob = await TryReadExistingJobAsync(evt.JobId, ct);
            var job = MapToBatchJob(evt, existingJob);

            await _container.UpsertItemAsync(
                job,
                new PartitionKey(job.JobId),
                cancellationToken: ct);

            _logger.LogInformation(
                "Upserted batch job {JobId} with status {Status} and progress {ProcessedItems}/{TotalItems}.",
                job.JobId,
                job.Status,
                job.ProcessedItems,
                job.TotalItems);
        }, evt.JobId, ct);
    }

    private async Task<BatchJob?> TryReadExistingJobAsync(string jobId, CancellationToken ct)
    {
        try
        {
            var response = await _container.ReadItemAsync<BatchJob>(
                jobId,
                new PartitionKey(jobId),
                cancellationToken: ct);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task ExecuteWithThrottleRetryAsync(
        Func<Task> operation,
        string jobId,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxThrottleRetries + 1; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            catch (CosmosException ex) when (
                ex.StatusCode == HttpStatusCode.TooManyRequests
                && attempt <= MaxThrottleRetries
                && !ct.IsCancellationRequested)
            {
                var delay = ex.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero
                    ? retryAfter
                    : TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1));

                _logger.LogWarning(
                    ex,
                    "CosmosDB throttled upsert for job {JobId} on attempt {Attempt}. Retrying in {DelayMs} ms.",
                    jobId,
                    attempt,
                    delay.TotalMilliseconds);

                await Task.Delay(delay, ct);
            }
        }
    }

    private static BatchJob MapToBatchJob(BatchProgressEvent evt, BatchJob? existingJob)
    {
        var startedAt = existingJob?.StartedAt
            ?? (evt.Status == BatchJobStatus.Running || evt.ProcessedItems > 0 ? evt.Timestamp : null);

        var completedAt = evt.Status is BatchJobStatus.Completed or BatchJobStatus.Failed
            ? evt.Timestamp
            : existingJob?.CompletedAt;

        return new BatchJob
        {
            Id = evt.JobId,
            JobId = evt.JobId,
            JobName = evt.JobName,
            Status = evt.Status,
            TotalItems = evt.TotalItems,
            ProcessedItems = evt.ProcessedItems,
            ErrorMessage = existingJob?.ErrorMessage,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            CreatedAt = existingJob?.CreatedAt ?? evt.Timestamp,
            PartitionKey = evt.JobId
        };
    }

    private static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} configuration is required.");
    }
}
