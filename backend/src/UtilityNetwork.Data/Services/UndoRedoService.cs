using Microsoft.Extensions.Logging;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class UndoRedoService : IUndoRedoService
{
    private readonly IUndoRedoRepository _repository;

    public UndoRedoService(IUndoRedoRepository repository)
    {
        _repository = repository;
    }

    public Task<UndoRedoStackDto> GetUserStackAsync(Guid userId, CancellationToken ct = default)
    {
        return _repository.GetUserStackAsync(userId, ct);
    }

    public Task<UndoResultDto> UndoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default)
    {
        return _repository.UndoLatestAsync(userId, username, role, ct);
    }

    public Task<RedoResultDto> RedoLatestAsync(Guid userId, string username, string role, CancellationToken ct = default)
    {
        return _repository.RedoLatestAsync(userId, username, role, ct);
    }
}
