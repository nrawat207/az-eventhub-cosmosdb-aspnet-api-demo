using Shared.Contracts.Models;

namespace BatchProcessor.Api.Services;

public interface IBatchJobService
{
    Task<string> StartJobAsync(StartBatchRequest request, CancellationToken cancellationToken = default);

    BatchJob? GetJob(string jobId);
}
