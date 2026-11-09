using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/analysis")]
[Authorize]
public sealed class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;

    public AnalysisController(IAnalysisService analysisService)
    {
        _analysisService = analysisService;
    }

    /// <summary>
    /// Executes a geodesic buffer on geography and returns buffer polygon and intersecting assets.
    /// </summary>
    [HttpPost("buffer")]
    [ProducesResponseType(typeof(BufferAnalysisResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunBufferAsync([FromBody] BufferAnalysisRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await _analysisService.RunBufferAnalysisAsync(request, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Buffer Request",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Computes multi-ring service areas around a geographic point with impact counts.
    /// </summary>
    [HttpPost("service-area")]
    [ProducesResponseType(typeof(ServiceAreaResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunServiceAreaAsync([FromBody] ServiceAreaRequest request, CancellationToken ct = default)
    {
        var result = await _analysisService.RunServiceAreaAnalysisAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Performs a spatial join count (e.g. assets inside administrative districts or service zones).
    /// </summary>
    [HttpPost("spatial-join")]
    [ProducesResponseType(typeof(SpatialJoinResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RunSpatialJoinAsync([FromBody] SpatialJoinRequest request, CancellationToken ct = default)
    {
        var result = await _analysisService.RunSpatialJoinAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Gets aggregated network statistics for charting and summary reporting.
    /// </summary>
    [HttpGet("statistics/{networkId:guid}")]
    [ProducesResponseType(typeof(NetworkStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatisticsAsync([FromRoute] Guid networkId, CancellationToken ct = default)
    {
        var stats = await _analysisService.GetNetworkStatisticsAsync(networkId, ct);
        return Ok(stats);
    }
}
