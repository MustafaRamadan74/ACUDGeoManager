using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UtilityNetwork.Api.Services;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Api.Hubs;

[Authorize]
public sealed class WorkspaceHub : Hub
{
    private readonly IPresenceTracker _presenceTracker;

    public WorkspaceHub(IPresenceTracker presenceTracker)
    {
        _presenceTracker = presenceTracker;
    }

    private Guid GetUserId()
    {
        var idStr = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idStr, out var id) ? id : Guid.Empty;
    }

    private string GetUsername()
    {
        return Context.User?.Identity?.Name
            ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
            ?? Context.User?.FindFirst("unique_name")?.Value
            ?? "user";
    }

    private string GetRole()
    {
        return Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "VIEWER";
    }

    public static string GetWorkspaceGroup(Guid workspaceId) => $"ws_{workspaceId:N}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var username = GetUsername();
        var role = GetRole();

        await _presenceTracker.UserConnectedAsync(Context.ConnectionId, userId, username, role, null);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var removed = await _presenceTracker.UserDisconnectedAsync(Context.ConnectionId);
        if (removed != null && removed.ActiveWorkspaceId.HasValue)
        {
            var groupName = GetWorkspaceGroup(removed.ActiveWorkspaceId.Value);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
            await Clients.Group(groupName).SendAsync("UserLeft", removed);

            var currentUsers = _presenceTracker.GetUsersInWorkspace(removed.ActiveWorkspaceId.Value);
            await Clients.Group(groupName).SendAsync("PresenceUpdated", currentUsers);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinWorkspace(Guid workspaceId)
    {
        var userId = GetUserId();
        var username = GetUsername();
        var role = GetRole();

        await _presenceTracker.SwitchWorkspaceAsync(Context.ConnectionId, workspaceId);

        var groupName = GetWorkspaceGroup(workspaceId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var presence = new UserPresenceDto(Context.ConnectionId, userId, username, role, workspaceId, DateTimeOffset.UtcNow);
        await Clients.OthersInGroup(groupName).SendAsync("UserJoined", presence);

        var currentUsers = _presenceTracker.GetUsersInWorkspace(workspaceId);
        await Clients.Caller.SendAsync("PresenceUpdated", currentUsers);
        await Clients.OthersInGroup(groupName).SendAsync("PresenceUpdated", currentUsers);
    }

    public async Task LeaveWorkspace(Guid workspaceId)
    {
        var groupName = GetWorkspaceGroup(workspaceId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        var removed = await _presenceTracker.UserDisconnectedAsync(Context.ConnectionId);
        if (removed != null)
        {
            await Clients.OthersInGroup(groupName).SendAsync("UserLeft", removed);
            var currentUsers = _presenceTracker.GetUsersInWorkspace(workspaceId);
            await Clients.OthersInGroup(groupName).SendAsync("PresenceUpdated", currentUsers);
        }
    }

    public async Task NotifyEditingFeature(Guid workspaceId, string featureTable, Guid featureId, bool isEditing)
    {
        var groupName = GetWorkspaceGroup(workspaceId);
        var ev = new FeatureEditingEvent(
            workspaceId,
            featureTable,
            featureId,
            GetUserId(),
            GetUsername(),
            isEditing,
            DateTimeOffset.UtcNow
        );

        await Clients.OthersInGroup(groupName).SendAsync("FeatureEditingChanged", ev);
    }
}
