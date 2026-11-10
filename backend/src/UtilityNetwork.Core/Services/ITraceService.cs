using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface ITraceService
{
    Task<NetworkTraceResultDto> ExecuteTraceAsync(NetworkTraceRequest request, ClaimsPrincipal user, CancellationToken ct = default);
}

