using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class HistoryRepository : IHistoryRepository
{
    private static readonly HashSet<string> AllowedTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "points", "lines", "polygons"
    };

    private readonly string _connectionString;

    public HistoryRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    public async Task<PagedResult<AuditLogEntry>> GetHistoryAsync(
        HistoryFilter filter,
        Guid currentUserId,
        bool isAdmin,
        CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        // Build WHERE clauses using parameterized conditions only
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // Security rule: Non-admins can only see their own history
        if (!isAdmin || filter.OnlyMe == true)
        {
            conditions.Add("user_id = @CurrentUserId");
            parameters.Add("CurrentUserId", currentUserId);
        }
        else if (filter.UserId.HasValue)
        {
            conditions.Add("user_id = @TargetUserId");
            parameters.Add("TargetUserId", filter.UserId.Value);
        }

        if (filter.FeatureId.HasValue)
        {
            conditions.Add("record_id = @FeatureId");
            parameters.Add("FeatureId", filter.FeatureId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.TableName))
        {
            var normalizedTable = filter.TableName.Trim().ToLowerInvariant();
            if (AllowedTableNames.Contains(normalizedTable))
            {
                conditions.Add("table_name = @TableName");
                parameters.Add("TableName", normalizedTable);
            }
        }

        if (filter.FromDate.HasValue)
        {
            conditions.Add("changed_at >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            conditions.Add("changed_at <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var countSql = $"SELECT count(*) FROM audit_log {whereClause};";
        var querySql = $@"
            SELECT id AS Id, 
                   change_set_id AS ChangeSetId, 
                   table_name AS TableName, 
                   record_id AS RecordId, 
                   action::text AS Action, 
                   old_data::text AS OldData, 
                   new_data::text AS NewData, 
                   user_id AS UserId, 
                   username AS Username, 
                   role AS Role, 
                   changed_at AS ChangedAt, 
                   reason::text AS Reason, 
                   request_id AS RequestId
            FROM audit_log
            {whereClause}
            ORDER BY changed_at DESC, id DESC
            LIMIT @Limit OFFSET @Offset;";

        parameters.Add("Limit", pageSize);
        parameters.Add("Offset", offset);

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        var totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, parameters, cancellationToken: ct));
        var items = await conn.QueryAsync<AuditLogEntry>(new CommandDefinition(querySql, parameters, cancellationToken: ct));

        return new PagedResult<AuditLogEntry>(items.ToList(), totalCount, page, pageSize);
    }
}
