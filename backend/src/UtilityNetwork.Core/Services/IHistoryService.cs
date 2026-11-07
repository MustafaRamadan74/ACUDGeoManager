using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IHistoryService
{
    Task<PagedResult<AuditLogEntry>> GetHistoryAsync(
        HistoryFilter filter,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default);
}
