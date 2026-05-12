using BatchProcessor.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.Contracts.Models;

namespace BatchProcessor.Api.Controllers;

[ApiController]
[Route("api/batch")]
public class BatchController : ControllerBase
{
    private readonly IBatchJobService _batchJobService;

    public BatchController(IBatchJobService batchJobService)
    {
        _batchJobService = batchJobService;
    }

    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(StartBatchRequest request, CancellationToken cancellationToken)
    {
        var jobId = await _batchJobService.StartJobAsync(request, cancellationToken);

        return AcceptedAtAction(
            nameof(GetStatus),
            new { jobId },
            new { jobId });
    }

    [HttpGet("{jobId}/status")]
    [ProducesResponseType(typeof(BatchJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus(string jobId)
    {
        var job = _batchJobService.GetJob(jobId);

        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health()
    {
        return Ok(new { status = "Healthy" });
    }
}
