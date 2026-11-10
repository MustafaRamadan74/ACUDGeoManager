using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface ITraceRepository
{
    Task<NetworkTraceResultDto> ExecuteTraceAsync(NetworkTraceRequest request, CancellationToken ct = default);
}

