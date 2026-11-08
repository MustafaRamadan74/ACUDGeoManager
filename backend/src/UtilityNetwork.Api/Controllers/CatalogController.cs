using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Policy = "Viewer")]
public sealed class CatalogController : ControllerBase
{
    private readonly ICatalogRepository _catalogRepository;

    public CatalogController(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    [HttpGet("networks")]
    [ProducesResponseType(typeof(IReadOnlyList<NetworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNetworksAsync(CancellationToken cancellationToken)
    {
        var networks = await _catalogRepository.GetNetworksAsync(cancellationToken);
        return Ok(networks);
    }

    [HttpGet("networks/{id:guid}/summary")]
    [ProducesResponseType(typeof(NetworkSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNetworkSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var summary = await _catalogRepository.GetNetworkSummaryAsync(id, cancellationToken);
        return summary is not null ? Ok(summary) : NotFound();
    }

    [HttpGet("feature-types")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatureTypesAsync(CancellationToken cancellationToken)
    {
        var types = await _catalogRepository.GetFeatureTypesAsync(cancellationToken);
        return Ok(types);
    }
}
