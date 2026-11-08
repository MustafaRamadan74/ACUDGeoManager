using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly IHistoryRepository _historyRepository;

    public HistoryService(IHistoryRepository historyRepository)
    {
        _historyRepository = historyRepository;
    }

    public Task<PagedResult<AuditLogEntry>> GetHistoryAsync(
        HistoryFilter filter,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        return _historyRepository.GetHistoryAsync(filter, currentUserId, isAdmin, ct);
    }
}
