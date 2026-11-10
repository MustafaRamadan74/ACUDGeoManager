using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class TraceRepository : ITraceRepository
{
    private readonly string _connectionString;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TraceRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<NetworkTraceResultDto> ExecuteTraceAsync(NetworkTraceRequest request, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT fn_network_trace(
                @NetworkId,
                @Mode,
                @StartFeatureId,
                @StartKind,
                @MaxDepth,
                @MaxEdges
            )::text;
            """;

        var command = new CommandDefinition(
            sql,
            new
            {
                NetworkId = request.NetworkId,
                Mode = request.Mode,
                StartFeatureId = request.StartFeatureId,
                StartKind = request.StartKind,
                MaxDepth = request.MaxDepth,
                MaxEdges = request.MaxEdges
            },
            cancellationToken: ct,
            commandTimeout: 30
        );

        var json = await connection.QuerySingleAsync<string>(command);

        var result = JsonSerializer.Deserialize<NetworkTraceResultDto>(json, JsonOptions);
        if (result is null)
        {
            throw new InvalidOperationException("Failed to deserialize network trace result from database.");
        }

        return result;
    }
}
