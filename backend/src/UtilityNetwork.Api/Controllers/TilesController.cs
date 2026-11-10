using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Authorize]
public sealed class TilesController : ControllerBase
{
    private readonly ITileService _tileService;

    public TilesController(ITileService tileService)
    {
        _tileService = tileService;
    }

    /// <summary>
    /// Serves binary Mapbox Vector Tiles (MVT) for all accessible networks at zoom level z, tile x, y.
    /// </summary>
    [HttpGet("api/v1/tiles/{z:int}/{x:int}/{y:int}.mvt")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetGlobalTileAsync(
        [FromRoute] int z,
        [FromRoute] int x,
        [FromRoute] int y,
        CancellationToken ct = default)
    {
        return await ServeTileInternalAsync(null, z, x, y, ct);
    }

    /// <summary>
    /// Serves binary Mapbox Vector Tiles (MVT) for a specific network workspace at zoom level z, tile x, y.
    /// </summary>
    [HttpGet("api/v1/tiles/{networkId:guid}/{z:int}/{x:int}/{y:int}.mvt")]
    [HttpGet("api/v1/workspaces/{networkId:guid}/tiles/{z:int}/{x:int}/{y:int}.mvt")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetNetworkTileAsync(
        [FromRoute] Guid networkId,
        [FromRoute] int z,
        [FromRoute] int x,
        [FromRoute] int y,
        CancellationToken ct = default)
    {
        return await ServeTileInternalAsync(networkId, z, x, y, ct);
    }

    /// <summary>
    /// Triggers a concurrent refresh of the city-scale materialized summary views.
    /// </summary>
    [HttpPost("api/v1/tiles/refresh-summaries")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshSummariesAsync(CancellationToken ct = default)
    {
        await _tileService.RefreshMaterializedSummariesAsync(User, ct);
        return Ok(new { message = "Materialized summaries refreshed successfully." });
    }

    private async Task<IActionResult> ServeTileInternalAsync(Guid? networkId, int z, int x, int y, CancellationToken ct)
    {
        var tileBytes = await _tileService.GetVectorTileAsync(networkId, z, x, y, User, ct);

        if (tileBytes is null || tileBytes.Length == 0)
        {
            return NoContent();
        }

        // Cache-Control headers for high-speed client tile caching
        Response.Headers.CacheControl = "public, max-age=120";

        return File(tileBytes, "application/vnd.mapbox-vector-tile");
    }
}

