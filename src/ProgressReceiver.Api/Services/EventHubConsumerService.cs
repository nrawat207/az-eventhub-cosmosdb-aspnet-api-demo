using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using ProgressReceiver.Api.Repositories;
using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Services;

public class EventHubConsumerService : BackgroundService
{
    private const string CheckpointContainerName = "eventhub-checkpoints";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly Azure.Core.TokenCredential _credential;
    private readonly ILogger<EventHubConsumerService> _logger;
    private EventProcessorClient? _processorClient;

    public EventHubConsumerService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        Azure.Core.TokenCredential credential,
        ILogger<EventHubConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _credential = credential;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var eventHubName = GetRequiredConfigurationValue("EventHub:Name");
        var connectionString = GetOptionalConfigurationValue("ConnectionStrings:EventHub")
            ?? GetOptionalConfigurationValue("EventHub:ConnectionString");
        var checkpointStore = CreateCheckpointStore();
        await checkpointStore.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _processorClient = new EventProcessorClient(
                checkpointStore,
                EventHubConsumerClient.DefaultConsumerGroupName,
                connectionString,
                eventHubName);
        }
        else
        {
            var fullyQualifiedNamespace = GetRequiredConfigurationValue("EventHub:NamespaceFQDN");

            _processorClient = new EventProcessorClient(
                checkpointStore,
                EventHubConsumerClient.DefaultConsumerGroupName,
                fullyQualifiedNamespace,
                eventHubName,
                _credential);
        }

        _processorClient.ProcessEventAsync += OnProcessEventAsync;
        _processorClient.ProcessErrorAsync += OnProcessErrorAsync;

        _logger.LogInformation("Starting EventHub processor for {EventHubName}.", eventHubName);

        try
        {
            await _processorClient.StartProcessingAsync(stoppingToken);

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("EventHub consumer service is stopping.");
            }
        }
        finally
        {
            await StopProcessorAsync(CancellationToken.None);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping EventHub consumer service.");
        await StopProcessorAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task OnProcessEventAsync(ProcessEventArgs args)
    {
        var cancellationToken = args.CancellationToken;

        if (!args.HasEvent)
        {
            return;
        }

        var jobId = string.Empty;

        try
        {
            var evt = args.Data.EventBody.ToObjectFromJson<BatchProgressEvent>(JsonOptions)
                ?? throw new JsonException("Batch progress event body was empty.");

            jobId = evt.JobId;

            _logger.LogInformation(
                "Received progress event for job {JobId} from partition {PartitionId} at sequence {SequenceNumber}. Status {Status}, progress {ProcessedItems}/{TotalItems}.",
                evt.JobId,
                args.Partition.PartitionId,
                args.Data.SequenceNumber,
                evt.Status,
                evt.ProcessedItems,
                evt.TotalItems);

            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBatchProgressRepository>();
            await repository.UpsertAsync(evt, cancellationToken);

            await args.UpdateCheckpointAsync(cancellationToken);

            _logger.LogInformation(
                "Checkpoint updated for job {JobId} on partition {PartitionId} at sequence {SequenceNumber}.",
                evt.JobId,
                args.Partition.PartitionId,
                args.Data.SequenceNumber);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "Failed processing EventHub message for job {JobId} from partition {PartitionId} at sequence {SequenceNumber}.",
                jobId,
                args.Partition.PartitionId,
                args.Data.SequenceNumber);

            throw;
        }
    }

    private Task OnProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "EventHub processor error during {Operation} for partition {PartitionId}.",
            args.Operation,
            args.PartitionId);

        return Task.CompletedTask;
    }

    private BlobContainerClient CreateCheckpointStore()
    {
        var storageConnectionString = GetOptionalConfigurationValue("Storage:ConnectionString");

        if (!string.IsNullOrWhiteSpace(storageConnectionString))
        {
            return new BlobContainerClient(storageConnectionString, CheckpointContainerName);
        }

        var containerUri = GetOptionalConfigurationValue("Storage:CheckpointContainerUri");

        if (string.IsNullOrWhiteSpace(containerUri))
        {
            var blobServiceUri = GetRequiredConfigurationValue("Storage:BlobServiceUri").TrimEnd('/');
            containerUri = $"{blobServiceUri}/{CheckpointContainerName}";
        }

        return new BlobContainerClient(new Uri(containerUri), _credential);
    }

    private async Task StopProcessorAsync(CancellationToken cancellationToken)
    {
        if (_processorClient is null)
        {
            return;
        }

        _processorClient.ProcessEventAsync -= OnProcessEventAsync;
        _processorClient.ProcessErrorAsync -= OnProcessErrorAsync;

        await _processorClient.StopProcessingAsync(cancellationToken);
    }

    private string GetRequiredConfigurationValue(string key)
    {
        var value = _configuration[key];

        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"{key} configuration is required.");
    }

    private string? GetOptionalConfigurationValue(string key)
    {
        var value = _configuration[key];

        return !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}
