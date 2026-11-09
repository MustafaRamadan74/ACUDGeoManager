using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IUndoRedoService
{
    Task<UndoRedoStackDto> GetUserStackAsync(Guid userId, CancellationToken ct = default);
    Task<UndoResultDto> UndoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default);
    Task<RedoResultDto> RedoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default);
}

