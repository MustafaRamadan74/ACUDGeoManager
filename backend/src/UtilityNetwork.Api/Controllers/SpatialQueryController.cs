using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/query")]
[Authorize(Policy = "Viewer")]
public sealed class SpatialQueryController : ControllerBase
{
    private readonly ISpatialQueryRepository _spatialQueryRepository;

    public SpatialQueryController(ISpatialQueryRepository spatialQueryRepository)
    {
        _spatialQueryRepository = spatialQueryRepository;
    }

    [HttpPost("within")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollection), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueryWithinAsync(
        [FromBody] QueryWithinRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _spatialQueryRepository.QueryWithinAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Npgsql.PostgresException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Spatial Geometry",
                Detail = ex.MessageText,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    [HttpGet("nearest")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollection), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> QueryNearestAsync(
        [FromQuery] double x,
        [FromQuery] double y,
        [FromQuery] Guid? networkId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] double? maxDistanceM,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var filter = new NearestQueryFilter(
            X: x,
            Y: y,
            NetworkId: networkId,
            FeatureType: type,
            Status: status,
            MaxDistanceM: maxDistanceM,
            Limit: limit);

        var result = await _spatialQueryRepository.QueryNearestAsync(filter, cancellationToken);
        return Ok(result);
    }
}
