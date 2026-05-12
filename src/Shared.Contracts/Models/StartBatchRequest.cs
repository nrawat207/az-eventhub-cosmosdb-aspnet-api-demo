using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Shared.Contracts.Models;

/// <summary>
/// Request model for initiating a new batch job.
/// Accepted by BatchProcessor.Api's POST /api/batch/start endpoint.
/// </summary>
public record StartBatchRequest
{
    /// <summary>
    /// Human-readable name for the batch job.
    /// Required and must not be empty.
    /// </summary>
    [JsonPropertyName("jobName")]
    [Required(ErrorMessage = "JobName is required.")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "JobName must be between 1 and 256 characters.")]
    public required string JobName { get; init; }

    /// <summary>
    /// Total number of items to process in this batch.
    /// Must be between 1 and 10,000.
    /// </summary>
    [JsonPropertyName("totalItems")]
    [Required(ErrorMessage = "TotalItems is required.")]
    [Range(1, 10000, ErrorMessage = "TotalItems must be between 1 and 10,000.")]
    public required int TotalItems { get; init; }
}
