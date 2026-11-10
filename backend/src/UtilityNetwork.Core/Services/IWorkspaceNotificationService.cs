using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IWorkspaceNotificationService
{
    Task NotifyFeatureChangedAsync(FeatureChangeEvent ev, CancellationToken ct = default);
    Task NotifyFeatureEditingAsync(FeatureEditingEvent ev, CancellationToken ct = default);
    Task NotifyValidationCompletedAsync(ValidationCompletedEvent ev, CancellationToken ct = default);
}
