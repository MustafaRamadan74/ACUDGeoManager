using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/map")]
[Authorize(Policy = "Viewer")]
public sealed class MapFeaturesController : ControllerBase
{
    private readonly IFeatureService _featureService;

    public MapFeaturesController(IFeatureService featureService)
    {
        _featureService = featureService;
    }

    [HttpGet("features")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollection), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMapFeaturesAsync(
        [FromQuery] string? bbox,
        [FromQuery] string? networks,
        [FromQuery] string? types,
        [FromQuery] int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        double minX = -180, minY = -90, maxX = 180, maxY = 90;

        if (!string.IsNullOrWhiteSpace(bbox))
        {
            var parts = bbox.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 4 ||
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out minX) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out minY) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out maxX) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out maxY))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid BBox Parameter",
                    Detail = "BBox must be in the format 'minLon,minLat,maxLon,maxLat'."
                });
            }
        }

        List<Guid>? networkIds = null;
        if (!string.IsNullOrWhiteSpace(networks))
        {
            networkIds = networks.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse)
                .ToList();
        }

        List<string>? typeCodes = null;
        if (!string.IsNullOrWhiteSpace(types))
        {
            typeCodes = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        var collection = await _featureService.GetMapFeaturesAsync(
            minX, minY, maxX, maxY, networkIds, typeCodes, User, limit, cancellationToken);

        return Ok(collection);
    }
}
