using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Api.Services;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/collaboration")]
[Authorize]
public sealed class CollaborationController : ControllerBase
{
    private readonly IPresenceTracker _presenceTracker;

    public CollaborationController(IPresenceTracker presenceTracker)
    {
        _presenceTracker = presenceTracker;
    }

    /// <summary>
    /// Returns the list of users currently active in a specific workspace.
    /// </summary>
    [HttpGet("workspaces/{workspaceId:guid}/presence")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<UserPresenceDto>), StatusCodes.Status200OK)]
    public IActionResult GetWorkspacePresence([FromRoute] Guid workspaceId)
    {
        var users = _presenceTracker.GetUsersInWorkspace(workspaceId);
        return Ok(users);
    }

    /// <summary>
    /// Returns all currently online users across the platform.
    /// </summary>
    [HttpGet("presence/online")]
    [Authorize(Policy = "Viewer")]
    [ProducesResponseType(typeof(IReadOnlyList<UserPresenceDto>), StatusCodes.Status200OK)]
    public IActionResult GetAllOnlineUsers()
    {
        var users = _presenceTracker.GetAllOnlineUsers();
        return Ok(users);
    }
}

