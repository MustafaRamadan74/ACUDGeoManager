using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/approvals")]
[Authorize]
public sealed class ApprovalsController : ControllerBase
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    /// <summary>
    /// Retrieves all changes waiting for review in the approval queue.
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Policy = "Approver")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingChangeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingQueueAsync(
        [FromQuery] Guid? networkId,
        CancellationToken ct = default)
    {
        var pending = await _approvalService.GetPendingQueueAsync(networkId, User, ct);
        return Ok(pending);
    }

    /// <summary>
    /// Retrieves a single pending change proposal by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Approver")]
    [ProducesResponseType(typeof(PendingChangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPendingChangeByIdAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var item = await _approvalService.GetPendingChangeByIdAsync(id, User, ct);
        if (item is null)
        {
            return NotFound(new { error = $"Pending change '{id}' not found." });
        }
        return Ok(item);
    }

    /// <summary>
    /// Approves and applies a pending feature modification into production tables.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Approver")]
    [ProducesResponseType(typeof(ApprovalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveChangeAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var result = await _approvalService.ApproveChangeAsync(id, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Rejects a pending feature modification with a mandatory explanation.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "Approver")]
    [ProducesResponseType(typeof(ApprovalResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectChangeAsync(
        [FromRoute] Guid id,
        [FromBody] RejectPendingChangeRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return BadRequest(new { error = "Rejection reason is required." });
        }

        var result = await _approvalService.RejectChangeAsync(id, request, User, ct);
        return Ok(result);
    }

    /// <summary>
    /// Configures whether modifications to a specific feature type require managerial approval.
    /// </summary>
    [HttpPatch("layers/{code}")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLayerApprovalRequirementAsync(
        [FromRoute] string code,
        [FromBody] UpdateLayerApprovalFlagRequest request,
        CancellationToken ct = default)
    {
        var updated = await _approvalService.UpdateLayerApprovalRequirementAsync(code, request.RequiresApproval, User, ct);
        if (!updated)
        {
            return NotFound(new { error = $"Feature type '{code}' not found." });
        }

        return Ok(new { featureTypeCode = code.ToUpperInvariant(), requiresApproval = request.RequiresApproval });
    }
}
