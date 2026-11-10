using System.Security.Authentication;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

#pragma warning disable CA1848, CA1873

namespace UtilityNetwork.Data.Services;

public sealed class TraceService : ITraceService
{
    private readonly ITraceRepository _traceRepo;
    private readonly ILogger<TraceService> _logger;

    public TraceService(
        ITraceRepository traceRepo,
        ILogger<TraceService> logger)
    {
        _traceRepo = traceRepo;
        _logger = logger;
    }

    public async Task<NetworkTraceResultDto> ExecuteTraceAsync(NetworkTraceRequest request, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, request.NetworkId);

        _logger.LogInformation("Executing {Mode} network trace on network {NetworkId} from {StartKind}:{StartFeatureId} for user {User}",
            request.Mode, request.NetworkId, request.StartKind, request.StartFeatureId, user.Identity?.Name);

        var result = await _traceRepo.ExecuteTraceAsync(request, ct);

        _logger.LogInformation("{Mode} trace completed on network {NetworkId}: {EdgeCount} edges ({TotalLengthM}m), {ValvesCount} valves to close, in {ExecutionMs}ms",
            result.Mode, result.NetworkId, result.Summary.EdgeCount, result.Summary.TotalLengthM, result.Summary.ValvesToCloseCount, result.Summary.ExecutionMs);

        return result;
    }

    private static void EnsureNetworkAccess(ClaimsPrincipal user, Guid networkId)
    {
        if (user.IsInRole("ADMIN")) return;

        var networkClaims = user.FindAll("network_id").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!networkClaims.Contains(networkId.ToString()))
        {
            throw new AuthenticationException($"Access denied to network '{networkId}'. The current user does not have assigned permissions.");
        }
    }
}

