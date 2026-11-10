using Microsoft.AspNetCore.SignalR;
using UtilityNetwork.Api.Hubs;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Services;

public sealed class WorkspaceNotificationService : IWorkspaceNotificationService
{
    private readonly IHubContext<WorkspaceHub> _hubContext;

    public WorkspaceNotificationService(IHubContext<WorkspaceHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyFeatureChangedAsync(FeatureChangeEvent ev, CancellationToken ct = default)
    {
        try
        {
            var group = WorkspaceHub.GetWorkspaceGroup(ev.WorkspaceId);
            await _hubContext.Clients.Group(group).SendAsync("FeatureChanged", ev, ct);
        }
        catch
        {
            // Non-blocking: background notification failure does not disrupt the caller
        }
    }

    public async Task NotifyFeatureEditingAsync(FeatureEditingEvent ev, CancellationToken ct = default)
    {
        try
        {
            var group = WorkspaceHub.GetWorkspaceGroup(ev.WorkspaceId);
            await _hubContext.Clients.Group(group).SendAsync("FeatureEditingChanged", ev, ct);
        }
        catch
        {
            // Non-blocking
        }
    }

    public async Task NotifyValidationCompletedAsync(ValidationCompletedEvent ev, CancellationToken ct = default)
    {
        try
        {
            if (ev.WorkspaceId.HasValue)
            {
                var group = WorkspaceHub.GetWorkspaceGroup(ev.WorkspaceId.Value);
                await _hubContext.Clients.Group(group).SendAsync("ValidationCompleted", ev, ct);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("ValidationCompleted", ev, ct);
            }
        }
        catch
        {
            // Non-blocking
        }
    }
}
