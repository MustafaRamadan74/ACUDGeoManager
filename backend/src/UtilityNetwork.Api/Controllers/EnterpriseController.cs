using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/enterprise")]
[Authorize]
public sealed class EnterpriseController : ControllerBase
{
    private readonly IEnterpriseService _enterpriseService;

    public EnterpriseController(IEnterpriseService enterpriseService)
    {
        _enterpriseService = enterpriseService;
    }

    private Guid GetUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(idStr, out var id))
        {
            return id;
        }
        throw new UnauthorizedAccessException("User identifier is missing or invalid.");
    }

    private string GetUsername()
    {
        return User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "system";
    }

    // ==========================================
    // 1. ATTACHMENTS
    // ==========================================

    [HttpGet("features/{featureId:guid}/attachments")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureAttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttachmentsAsync([FromRoute] Guid featureId, CancellationToken ct = default)
    {
        var attachments = await _enterpriseService.GetAttachmentsAsync(featureId, ct);
        return Ok(attachments);
    }

    [HttpPost("features/{featureId:guid}/attachments")]
    [Authorize(Policy = "Editor")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(FeatureAttachmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAttachmentAsync(
        [FromRoute] Guid featureId,
        [FromForm] IFormFile file,
        [FromForm] string featureTable,
        [FromForm] string? description,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { detail = "A non-empty file must be provided." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var uploaded = await _enterpriseService.UploadAttachmentAsync(
                featureId,
                featureTable,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                description,
                GetUsername(),
                ct
            );

            return StatusCode(StatusCodes.Status201Created, uploaded);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("attachments/{id:guid}/download")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAttachmentAsync([FromRoute] Guid id, CancellationToken ct = default)
    {
        try
        {
            var (stream, contentType, fileName) = await _enterpriseService.DownloadAttachmentAsync(id, ct);
            return File(stream, contentType, fileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { detail = "Attachment not found." });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { detail = "Physical file not found on disk." });
        }
    }

    [HttpDelete("attachments/{id:guid}")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAttachmentAsync([FromRoute] Guid id, CancellationToken ct = default)
    {
        var success = await _enterpriseService.DeleteAttachmentAsync(id, ct);
        if (!success)
        {
            return NotFound(new { detail = "Attachment not found." });
        }
        return NoContent();
    }

    // ==========================================
    // 2. RELATIONSHIPS
    // ==========================================

    [HttpGet("features/{featureId:guid}/relationships")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureRelationshipDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRelationshipsAsync([FromRoute] Guid featureId, CancellationToken ct = default)
    {
        var relationships = await _enterpriseService.GetRelationshipsAsync(featureId, ct);
        return Ok(relationships);
    }

    [HttpPost("relationships")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(FeatureRelationshipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRelationshipAsync(
        [FromBody] CreateRelationshipRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var created = await _enterpriseService.CreateRelationshipAsync(request, GetUsername(), ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpDelete("relationships/{id:guid}")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRelationshipAsync([FromRoute] Guid id, CancellationToken ct = default)
    {
        var success = await _enterpriseService.DeleteRelationshipAsync(id, ct);
        if (!success)
        {
            return NotFound(new { detail = "Relationship not found." });
        }
        return NoContent();
    }

    // ==========================================
    // 3. EDIT LOCKING
    // ==========================================

    [HttpGet("features/{featureId:guid}/lock")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(FeatureLockDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLockStatusAsync([FromRoute] Guid featureId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var lockInfo = await _enterpriseService.GetLockStatusAsync(featureId, userId, ct);
        if (lockInfo == null)
        {
            return Ok(new { isLocked = false });
        }
        return Ok(lockInfo);
    }

    [HttpPost("features/{featureId:guid}/lock")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(typeof(FeatureLockDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcquireLockAsync(
        [FromRoute] Guid featureId,
        [FromBody] AcquireLockRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var userId = GetUserId();
            var username = GetUsername();
            var lockInfo = await _enterpriseService.AcquireLockAsync(
                featureId,
                request.FeatureTable,
                userId,
                username,
                request.TtlMinutes ?? 5,
                request.ClientSessionId,
                ct
            );
            return Ok(lockInfo);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpDelete("features/{featureId:guid}/lock")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReleaseLockAsync([FromRoute] Guid featureId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var isAdmin = User.IsInRole("ADMIN");
        await _enterpriseService.ReleaseLockAsync(featureId, userId, isAdmin, ct);
        return NoContent();
    }

    [HttpPost("features/{featureId:guid}/lock/refresh")]
    [Authorize(Policy = "Editor")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefreshLockAsync(
        [FromRoute] Guid featureId,
        [FromBody] AcquireLockRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var refreshed = await _enterpriseService.RefreshLockAsync(featureId, userId, request.TtlMinutes ?? 5, ct);
        if (!refreshed)
        {
            return NotFound(new { detail = "Active lock not found or owned by another user." });
        }
        return Ok(new { success = true });
    }

    // ==========================================
    // 4. DATA QUALITY DASHBOARD
    // ==========================================

    [HttpGet("data-quality")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(DataQualityDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDataQualityDashboardAsync(
        [FromQuery] Guid? networkId = null,
        CancellationToken ct = default)
    {
        var dashboard = await _enterpriseService.GetDataQualityDashboardAsync(networkId, ct);
        return Ok(dashboard);
    }
}
