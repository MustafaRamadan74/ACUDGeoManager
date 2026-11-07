using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IHistoryRepository
{
    Task<PagedResult<AuditLogEntry>> GetHistoryAsync(
        HistoryFilter filter,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
