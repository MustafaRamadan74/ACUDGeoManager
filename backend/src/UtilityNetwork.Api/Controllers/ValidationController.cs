using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ValidationController : ControllerBase
{
    private readonly IValidationService _validationService;

    public ValidationController(IValidationService validationService)
    {
        _validationService = validationService;
    }

    [HttpPost("networks/{networkId:guid}/validation/run")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(ValidationRunDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunNetworkValidationAsync(
        Guid networkId,
        CancellationToken cancellationToken)
    {
        var userName = GetCurrentUserName();
        var run = await _validationService.RunValidationAsync(networkId, userName, cancellationToken);
        return Ok(run);
    }

    [HttpGet("networks/{networkId:guid}/validation/runs")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<ValidationRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNetworkRunsAsync(
        Guid networkId,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var runs = await _validationService.GetRunsAsync(networkId, limit, cancellationToken);
        return Ok(runs);
    }

    [HttpGet("validation/runs")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<ValidationRunDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllRunsAsync(
        [FromQuery] Guid? networkId = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var runs = await _validationService.GetRunsAsync(networkId, limit, cancellationToken);
        return Ok(runs);
    }

    [HttpGet("validation/runs/{runId:guid}")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(ValidationRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRunByIdAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _validationService.GetRunByIdAsync(runId, cancellationToken);
        return run != null ? Ok(run) : NotFound();
    }

    [HttpGet("validation/issues")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(ValidationIssuePageResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryIssuesAsync(
        [FromQuery] Guid? runId,
        [FromQuery] Guid? networkId,
        [FromQuery] IssueStatus? status,
        [FromQuery] ValidationSeverity? severity,
        [FromQuery] string? ruleCode,
        [FromQuery] string? featureTable,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new ValidationIssueQuery(
            RunId: runId,
            NetworkId: networkId,
            Status: status,
            Severity: severity,
            RuleCode: ruleCode,
            FeatureTable: featureTable,
            Page: page,
            PageSize: pageSize
        );

        var result = await _validationService.QueryIssuesAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("validation/issues/{id:guid}/status")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(ValidationIssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIssueStatusAsync(
        Guid id,
        [FromBody] UpdateIssueStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userName = GetCurrentUserName();
        var updated = await _validationService.UpdateIssueStatusAsync(id, request.Status, userName, cancellationToken);
        return Ok(updated);
    }

    private string GetCurrentUserName()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name
            ?? "system";
    }
}
