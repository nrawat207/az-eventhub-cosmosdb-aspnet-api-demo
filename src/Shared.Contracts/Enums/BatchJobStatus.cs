namespace Shared.Contracts.Enums;

/// <summary>
/// Represents the status of a batch job.
/// </summary>
public enum BatchJobStatus
{
    Pending = 0,
    Running = 1,
    Completed = 2,
    Failed = 3
}
