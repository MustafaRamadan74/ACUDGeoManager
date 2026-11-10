using System.Collections.Concurrent;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Api.Services;

public interface IPresenceTracker
{
    Task UserConnectedAsync(string connectionId, Guid userId, string username, string role, Guid? workspaceId);
    Task<UserPresenceDto?> UserDisconnectedAsync(string connectionId);
    Task SwitchWorkspaceAsync(string connectionId, Guid workspaceId);
    IReadOnlyList<UserPresenceDto> GetUsersInWorkspace(Guid workspaceId);
    IReadOnlyList<UserPresenceDto> GetAllOnlineUsers();
}

public sealed class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, UserPresenceDto> _connections = new();

    public Task UserConnectedAsync(string connectionId, Guid userId, string username, string role, Guid? workspaceId)
    {
        var presence = new UserPresenceDto(connectionId, userId, username, role, workspaceId, DateTimeOffset.UtcNow);
        _connections[connectionId] = presence;
        return Task.CompletedTask;
    }

    public Task<UserPresenceDto?> UserDisconnectedAsync(string connectionId)
    {
        _connections.TryRemove(connectionId, out var removed);
        return Task.FromResult(removed);
    }

    public Task SwitchWorkspaceAsync(string connectionId, Guid workspaceId)
    {
        _connections.AddOrUpdate(
            connectionId,
            id => new UserPresenceDto(id, Guid.Empty, "unknown", "VIEWER", workspaceId, DateTimeOffset.UtcNow),
            (id, existing) => existing with { ActiveWorkspaceId = workspaceId }
        );
        return Task.CompletedTask;
    }

    public IReadOnlyList<UserPresenceDto> GetUsersInWorkspace(Guid workspaceId)
    {
        return _connections.Values
            .Where(p => p.ActiveWorkspaceId == workspaceId)
            .GroupBy(p => p.UserId) // Group in case the same user has multiple tabs/connections
            .Select(g => g.First())
            .ToList();
    }

    public IReadOnlyList<UserPresenceDto> GetAllOnlineUsers()
    {
        return _connections.Values
            .GroupBy(p => p.UserId)
            .Select(g => g.First())
            .ToList();
    }
}

