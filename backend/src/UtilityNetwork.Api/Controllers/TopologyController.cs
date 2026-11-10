using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/topology")]
[Authorize]
public sealed class TopologyController : ControllerBase
{
    private readonly ITopologyService _topologyService;

    public TopologyController(ITopologyService topologyService)
    {
        _topologyService = topologyService;
    }

    /// <summary>
    /// Gets current topology state and metric counts for a specified network.
    /// </summary>
    [HttpGet("{networkId:guid}/status")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(TopologyStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusAsync(
        [FromRoute] Guid networkId,
        CancellationToken ct = default)
    {
        var status = await _topologyService.GetStatusAsync(networkId, User, ct);
        return Ok(status);
    }

    /// <summary>
    /// Gets topology status for all accessible networks.
    /// </summary>
    [HttpGet("statuses")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<TopologyStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllStatusesAsync(CancellationToken ct = default)
    {
        var statuses = await _topologyService.GetAllStatusesAsync(User, ct);
        return Ok(statuses);
    }

    /// <summary>
    /// Rebuilds topology for a specified network: clusters line endpoints within snap tolerance into nodes, creates edges, and attaches features.
    /// </summary>
    [HttpPost("{networkId:guid}/rebuild")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(RebuildTopologyResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RebuildTopologyAsync(
        [FromRoute] Guid networkId,
        CancellationToken ct = default)
    {
        var result = await _topologyService.RebuildTopologyAsync(networkId, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Rebuilds topology across all networks (Admin only).
    /// </summary>
    [HttpPost("rebuild-all")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<RebuildTopologyResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RebuildAllAsync(CancellationToken ct = default)
    {
        var results = await _topologyService.RebuildAllAsync(User, ct);
        return Ok(results);
    }
}

