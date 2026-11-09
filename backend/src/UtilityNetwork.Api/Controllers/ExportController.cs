using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/export")]
[Authorize]
public sealed class ExportController : ControllerBase
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Exports spatial features to GeoJSON format with optional coordinate transformation.
    /// </summary>
    /// <param name="networkId">Target workspace/network ID filter</param>
    /// <param name="featureTypeCode">Target layer/feature type code filter</param>
    /// <param name="bbox">Bounding box in minX,minY,maxX,maxY format</param>
    /// <param name="srid">Target coordinate system: 4326 (WGS84), 22992 (Egypt Red Belt), or 32636 (UTM 36N)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("geojson")]
    [Produces("application/geo+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportGeoJsonAsync(
        [FromQuery] Guid? networkId,
        [FromQuery] string? featureTypeCode,
        [FromQuery] string? bbox,
        [FromQuery] int srid = 4326,
        CancellationToken ct = default)
    {
        var request = new ExportFilterRequest(networkId, featureTypeCode, bbox, srid);
        var (bytes, contentType, fileName) = await _exportService.ExportGeoJsonAsync(request, User, ct);

        return File(bytes, contentType, fileName);
    }

    /// <summary>
    /// Exports spatial features to CSV format with formula injection protection and selectable SRID.
    /// </summary>
    /// <param name="networkId">Target workspace/network ID filter</param>
    /// <param name="featureTypeCode">Target layer/feature type code filter</param>
    /// <param name="bbox">Bounding box in minX,minY,maxX,maxY format</param>
    /// <param name="srid">Target coordinate system: 4326 (WGS84), 22992 (Egypt Red Belt), or 32636 (UTM 36N)</param>
    /// <param name="ct">Cancellation token</param>
    [HttpGet("csv")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportCsvAsync(
        [FromQuery] Guid? networkId,
        [FromQuery] string? featureTypeCode,
        [FromQuery] string? bbox,
        [FromQuery] int srid = 4326,
        CancellationToken ct = default)
    {
        var request = new ExportFilterRequest(networkId, featureTypeCode, bbox, srid);
        var (bytes, contentType, fileName) = await _exportService.ExportCsvAsync(request, User, ct);

        return File(bytes, contentType, fileName);
    }
}
