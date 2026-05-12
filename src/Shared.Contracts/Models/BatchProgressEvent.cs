using System.Text.Json.Serialization;
using Shared.Contracts.Enums;

namespace Shared.Contracts.Models;

/// <summary>
/// Represents a progress event published to Azure EventHub.
/// This is the message payload sent by BatchProcessor.Api as it processes items.
/// Consumed by ProgressReceiver.Api and persisted to CosmosDB.
/// </summary>
public record BatchProgressEvent
{
    /// <summary>
    /// Unique job identifier (GUID string).
    /// Correlates with the original batch job.
    /// </summary>
    [JsonPropertyName("jobId")]
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable name for the batch job.
    /// </summary>
    [JsonPropertyName("jobName")]
    public string JobName { get; init; } = string.Empty;

    /// <summary>
    /// Number of items processed so far in this batch.
    /// </summary>
    [JsonPropertyName("processedItems")]
    public int ProcessedItems { get; init; }

    /// <summary>
    /// Total number of items in this batch.
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    /// <summary>
    /// Current status of the batch job.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BatchJobStatus Status { get; init; } = BatchJobStatus.Pending;

    /// <summary>
    /// Timestamp when this progress event was created.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Identifies which ACA (Azure Container Apps) instance sent this event.
    /// Useful for debugging and tracing across multiple replicas.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = string.Empty;
}
