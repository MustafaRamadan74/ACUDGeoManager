using System.Data.Common;

namespace UtilityNetwork.Core.Data;

public sealed record UserContext(
    Guid? UserId,
    string Username,
    string Role,
    string? RequestId)
{
    public static readonly UserContext System = new(null, "system", "SYSTEM", null);
}

public interface IUnitOfWork : IAsyncDisposable
{
    Guid ChangeSetId { get; }
    DbConnection Connection { get; }
    DbTransaction Transaction { get; }
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface IUnitOfWorkFactory
{
    Task<IUnitOfWork> BeginWriteAsync(
        UserContext userContext,
        string description = "",
        string reason = "NORMAL",
        CancellationToken ct = default);
}
