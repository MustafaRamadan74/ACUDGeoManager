using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Route("[controller]")]
[Authorize]
public sealed class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;
    private readonly IUndoRedoService _undoRedoService;

    public HistoryController(
        IHistoryService historyService,
        IUndoRedoService undoRedoService)
    {
        _historyService = historyService;
        _undoRedoService = undoRedoService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] bool? onlyMe,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? featureId,
        [FromQuery] string? tableName,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole("ADMIN");

        var filter = new HistoryFilter(
            OnlyMe: onlyMe,
            UserId: userId,
            FeatureId: featureId,
            TableName: tableName,
            FromDate: fromDate,
            ToDate: toDate,
            Page: page,
            PageSize: pageSize);

        var result = await _historyService.GetHistoryAsync(filter, currentUserId, isAdmin, cancellationToken);
        return Ok(result);
    }

    [HttpGet("stack")]
    [ProducesResponseType(typeof(UndoRedoStackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStackAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var stack = await _undoRedoService.GetUserStackAsync(currentUserId.Value, cancellationToken);
        return Ok(stack);
    }

    [HttpPost("undo")]
    [ProducesResponseType(typeof(UndoResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UndoAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var username = GetCurrentUsername();
        var role = GetCurrentUserRole();

        try
        {
            var result = await _undoRedoService.UndoLatestAsync(currentUserId.Value, username, role, cancellationToken);
            return Ok(result);
        }
        catch (UndoRedoConflictException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                message = ex.Message,
                conflicts = ex.Conflicts
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("redo")]
    [ProducesResponseType(typeof(RedoResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RedoAsync(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
        {
            return Unauthorized();
        }

        var username = GetCurrentUsername();
        var role = GetCurrentUserRole();

        try
        {
            var result = await _undoRedoService.RedoLatestAsync(currentUserId.Value, username, role, cancellationToken);
            return Ok(result);
        }
        catch (UndoRedoConflictException ex)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                message = ex.Message,
                conflicts = ex.Conflicts
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out var guid) ? guid : null;
    }

    private string GetCurrentUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value
               ?? User.FindFirst("unique_name")?.Value
               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? User.Identity?.Name
               ?? "system";
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value
               ?? User.FindFirst("role")?.Value
               ?? "VIEWER";
    }
}
