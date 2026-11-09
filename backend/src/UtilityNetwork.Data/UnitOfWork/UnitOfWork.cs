using System.Data.Common;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Data;

namespace UtilityNetwork.Data.UnitOfWork;

public sealed class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly string _connectionString;

    public UnitOfWorkFactory(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<IUnitOfWork> BeginWriteAsync(
        UserContext userContext,
        string description = "",
        string reason = "NORMAL",
        CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(ct);

        var changeSetId = Guid.NewGuid();

        // 1. If reason is NORMAL, clear that user's redo stack per specification
        if (string.Equals(reason, "NORMAL", StringComparison.OrdinalIgnoreCase) && userContext.UserId.HasValue)
        {
            const string clearRedoSql = @"
                DELETE FROM audit_log 
                WHERE change_set_id IN (
                    SELECT id FROM change_sets 
                    WHERE user_id = @userId AND status = 'UNDONE'::change_set_status
                );
                DELETE FROM change_sets 
                WHERE user_id = @userId AND status = 'UNDONE'::change_set_status;";

            await connection.ExecuteAsync(new CommandDefinition(
                clearRedoSql,
                new { userId = userContext.UserId.Value },
                transaction: transaction,
                cancellationToken: ct));
        }

        // 2. Create change_set entry
        const string insertChangeSetSql = @"
            INSERT INTO change_sets (id, user_id, description, status)
            VALUES (@changeSetId, @userId, @description, 'APPLIED'::change_set_status);";

        await connection.ExecuteAsync(new CommandDefinition(
            insertChangeSetSql,
            new { changeSetId, userId = userContext.UserId, description },
            transaction: transaction,
            cancellationToken: ct));

        // 2. Set transaction-local session settings for audit triggers
        const string setConfigSql = @"
            SELECT set_config('app.change_set_id', @changeSetId, true),
                   set_config('app.user_id', @userId, true),
                   set_config('app.username', @username, true),
                   set_config('app.role', @role, true),
                   set_config('app.request_id', @requestId, true),
                   set_config('app.reason', @reason, true);";

        await connection.ExecuteAsync(new CommandDefinition(
            setConfigSql,
            new
            {
                changeSetId = changeSetId.ToString(),
                userId = userContext.UserId?.ToString() ?? "",
                username = userContext.Username,
                role = userContext.Role,
                requestId = userContext.RequestId ?? "",
                reason
            },
            transaction: transaction,
            cancellationToken: ct));

        return new UnitOfWork(changeSetId, connection, transaction);
    }
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private bool _committed;
    private bool _disposed;

    public Guid ChangeSetId { get; }
    public DbConnection Connection => _connection;
    public DbTransaction Transaction => _transaction;

    public UnitOfWork(Guid changeSetId, NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ChangeSetId = changeSetId;
        _connection = connection;
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_committed || _disposed) return;
        await _transaction.CommitAsync(ct);
        _committed = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_committed || _disposed) return;
        await _transaction.RollbackAsync(ct);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        if (!_committed)
        {
            try
            {
                await _transaction.RollbackAsync();
            }
            catch
            {
                // Suppress rollback errors on dispose
            }
        }

        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
        _disposed = true;
    }
}
