using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class PointsController : ControllerBase
{
    private readonly IFeatureService _featureService;
    private readonly IValidator<CreatePointRequest> _createValidator;
    private readonly IValidator<UpdatePointRequest> _updateValidator;

    public PointsController(
        IFeatureService featureService,
        IValidator<CreatePointRequest> createValidator,
        IValidator<UpdatePointRequest> updateValidator)
    {
        _featureService = featureService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(KeysetPagedResult<PointFeatureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPointsAsync(
        [FromQuery] Guid? networkId,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] double? minX,
        [FromQuery] double? minY,
        [FromQuery] double? maxX,
        [FromQuery] double? maxY,
        [FromQuery] Guid? afterId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new FeatureQueryFilter(
            NetworkId: networkId,
            FeatureTypeCode: type,
            Status: status,
            MinX: minX,
            MinY: minY,
            MaxX: maxX,
            MaxY: maxY,
            AfterId: afterId,
            Limit: limit);

        var result = await _featureService.GetPointsAsync(filter, User, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(PointFeatureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var point = await _featureService.GetPointByIdAsync(id, User, cancellationToken);
        return point != null ? Ok(point) : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(PointFeatureDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreatePointRequest request,
        CancellationToken cancellationToken)
    {
        var val = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!val.IsValid)
        {
            throw new ValidationException(val.Errors);
        }

        var created = await _featureService.CreatePointAsync(request, User, cancellationToken);
        return CreatedAtAction("GetById", new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(PointFeatureDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdatePointRequest request,
        CancellationToken cancellationToken)
    {
        var val = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!val.IsValid)
        {
            throw new ValidationException(val.Errors);
        }

        var updated = await _featureService.UpdatePointAsync(id, request, User, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        [FromQuery] int version,
        CancellationToken cancellationToken)
    {
        await _featureService.DeletePointAsync(id, version, User, cancellationToken);
        return NoContent();
    }
}
