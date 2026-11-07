namespace UtilityNetwork.Core.Services;

public interface IAdminBootstrapService
{
    Task<bool> EnsureAdminCreatedAsync(CancellationToken ct = default);
}
