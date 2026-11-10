using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Authorize]
public sealed class TraceController : ControllerBase
{
    private readonly ITraceService _traceService;

    public TraceController(ITraceService traceService)
    {
        _traceService = traceService;
    }

    /// <summary>
    /// Executes a directed Network Trace (UPSTREAM, DOWNSTREAM, or ISOLATION) on a utility network.
    /// </summary>
    [HttpPost("api/v1/topology/{networkId:guid}/trace")]
    [HttpPost("api/v1/workspaces/{networkId:guid}/trace")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(NetworkTraceResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExecuteTraceAsync(
        [FromRoute] Guid networkId,
        [FromBody] NetworkTraceRequest request,
        CancellationToken ct = default)
    {
        // Enforce path route network ID
        var boundRequest = request with { NetworkId = networkId };
        var result = await _traceService.ExecuteTraceAsync(boundRequest, User, ct);
        return Ok(result);
    }
}

