using System.Collections.Concurrent;
using System.Threading.Channels;
using Shared.Contracts.Enums;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Services;

public class BatchJobService : BackgroundService, IBatchJobService
{
    private static readonly string NodeId =
        Environment.GetEnvironmentVariable("HOSTNAME")
        ?? Environment.GetEnvironmentVariable("COMPUTERNAME")
        ?? Environment.MachineName;

    private readonly ConcurrentDictionary<string, BatchJob> _jobs = new();
    private readonly Channel<BatchJob> _queue = Channel.CreateUnbounded<BatchJob>();
    private readonly EventHubPublisherService _publisher;
    private readonly ILogger<BatchJobService> _logger;

    public BatchJobService(
        EventHubPublisherService publisher,
        ILogger<BatchJobService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<string> StartJobAsync(StartBatchRequest request, CancellationToken cancellationToken = default)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var job = new BatchJob
        {
            Id = jobId,
            JobId = jobId,
            JobName = request.JobName,
            Status = BatchJobStatus.Pending,
            TotalItems = request.TotalItems,
            ProcessedItems = 0,
            CreatedAt = now,
            PartitionKey = jobId
        };

        if (!_jobs.TryAdd(jobId, job))
        {
            throw new InvalidOperationException($"Unable to start batch job '{jobId}'.");
        }

        await _queue.Writer.WriteAsync(job, cancellationToken);
        _logger.LogInformation("Queued batch job {JobId} with {TotalItems} items.", jobId, request.TotalItems);

        return jobId;
    }

    public BatchJob? GetJob(string jobId)
    {
        return _jobs.TryGetValue(jobId, out var job) ? job : null;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch job background service started.");

        await foreach (var job in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessJobAsync(job, stoppingToken);
        }
    }

    private async Task ProcessJobAsync(BatchJob queuedJob, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var job = queuedJob with
        {
            Status = BatchJobStatus.Running,
            StartedAt = startedAt
        };

        _jobs[job.JobId] = job;
        _logger.LogInformation("Started processing batch job {JobId}.", job.JobId);

        try
        {
            for (var processedItems = 1; processedItems <= job.TotalItems; processedItems++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);

                job = job with { ProcessedItems = processedItems };
                _jobs[job.JobId] = job;

                await _publisher.PublishAsync(CreateProgressEvent(job), cancellationToken);
            }

            job = job with
            {
                Status = BatchJobStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow
            };

            _jobs[job.JobId] = job;
            await _publisher.PublishAsync(CreateProgressEvent(job), cancellationToken);

            _logger.LogInformation("Completed batch job {JobId}.", job.JobId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Batch job {JobId} stopped because the service is shutting down.", job.JobId);
        }
        catch (Exception ex)
        {
            job = job with
            {
                Status = BatchJobStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow
            };

            _jobs[job.JobId] = job;
            _logger.LogError(ex, "Batch job {JobId} failed.", job.JobId);

            try
            {
                await _publisher.PublishAsync(CreateProgressEvent(job), CancellationToken.None);
            }
            catch (Exception publishEx)
            {
                _logger.LogError(publishEx, "Failed publishing failure event for batch job {JobId}.", job.JobId);
            }
        }
    }

    private static BatchProgressEvent CreateProgressEvent(BatchJob job)
    {
        return new BatchProgressEvent
        {
            JobId = job.JobId,
            JobName = job.JobName,
            ProcessedItems = job.ProcessedItems,
            TotalItems = job.TotalItems,
            Status = job.Status,
            Timestamp = DateTimeOffset.UtcNow,
            NodeId = NodeId
        };
    }
}
