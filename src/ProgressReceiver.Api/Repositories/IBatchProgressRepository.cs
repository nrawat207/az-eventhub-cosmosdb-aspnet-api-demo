using Shared.Contracts.Models;

namespace ProgressReceiver.Api.Repositories;

public interface IBatchProgressRepository
{
    Task UpsertAsync(BatchProgressEvent evt, CancellationToken ct);
}
