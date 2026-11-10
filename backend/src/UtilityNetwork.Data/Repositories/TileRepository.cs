using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class TileRepository : ITileRepository
{
    private readonly string _connectionString;

    public TileRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<byte[]?> GetVectorTileAsync(Guid? networkId, int z, int x, int y, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT fn_get_vector_tile(@networkId, @z, @x, @y);";

        var command = new CommandDefinition(
            sql,
            new { networkId, z, x, y },
            cancellationToken: ct,
            commandTimeout: 10
        );

        var tileBytes = await connection.QuerySingleOrDefaultAsync<byte[]?>(command);
        return tileBytes is { Length: > 0 } ? tileBytes : null;
    }

    public async Task RefreshMaterializedSummariesAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = "SELECT fn_refresh_materialized_summaries();";
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct, commandTimeout: 30));
    }
}

