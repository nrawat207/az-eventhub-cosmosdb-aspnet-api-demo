using System.Text.Json.Serialization;
using Shared.Contracts.Enums;

namespace Shared.Contracts.Models;

/// <summary>
/// Represents a batch job document stored in CosmosDB.
/// Used for persistence and state management of long-running batch operations.
/// </summary>
public record BatchJob
{
    /// <summary>
    /// CosmosDB document id (same as JobId for simplicity).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Unique job identifier (GUID string).
    /// Serves as the partition key for CosmosDB.
    /// </summary>
    [JsonPropertyName("jobId")]
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable name for the batch job.
    /// </summary>
    [JsonPropertyName("jobName")]
    public string JobName { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the batch job.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BatchJobStatus Status { get; init; } = BatchJobStatus.Pending;

    /// <summary>
    /// Total number of items to process in this batch.
    /// </summary>
    [JsonPropertyName("totalItems")]
    public int TotalItems { get; init; }

    /// <summary>
    /// Number of items successfully processed so far.
    /// </summary>
    [JsonPropertyName("processedItems")]
    public int ProcessedItems { get; init; }

    /// <summary>
    /// Optional error message if the job failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp when the job processing started.
    /// Null until the job actually begins running.
    /// </summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Timestamp when the job processing completed (success or failure).
    /// Null until the job completes.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Timestamp when the job was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Partition key for CosmosDB (always equals JobId).
    /// Used to optimize query performance and cost.
    /// </summary>
    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; init; } = string.Empty;
}
