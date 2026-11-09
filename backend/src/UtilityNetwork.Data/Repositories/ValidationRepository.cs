using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class ValidationRepository : IValidationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly string _connectionString;

    public ValidationRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    public async Task<ValidationRunDto> CreateValidationRunAsync(Guid networkId, string startedBy, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        const string sql = @"
            INSERT INTO validation_runs (id, network_id, started_by, started_at, summary)
            VALUES (@runId, @networkId, @startedBy, now(), '{}'::jsonb)
            RETURNING id, network_id, started_by, started_at, finished_at, summary::text;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync<dynamic>(new CommandDefinition(sql, new { runId, networkId, startedBy }, cancellationToken: ct));

        var summary = new ValidationSummaryDto(0, 0, 0, 0, 0, Array.Empty<string>());
        return new ValidationRunDto(
            (Guid)row.id,
            (Guid)row.network_id,
            (string)row.started_by,
            (DateTime)row.started_at,
            (DateTime?)row.finished_at,
            summary
        );
    }

    public async Task CompleteValidationRunAsync(Guid runId, ValidationSummaryDto summary, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE validation_runs
            SET finished_at = now(),
                summary = @summaryJson::jsonb
            WHERE id = @runId;";

        var summaryJson = JsonSerializer.Serialize(summary);
        await using var conn = await CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { runId, summaryJson }, cancellationToken: ct));
    }

    public async Task SaveValidationIssuesAsync(Guid runId, IEnumerable<ValidationIssueDto> issues, CancellationToken ct = default)
    {
        var issueList = issues.ToList();
        if (issueList.Count == 0) return;

        await using var conn = await CreateOpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string sql = @"
            INSERT INTO validation_issues (
                id, run_id, rule_code, severity, feature_table, feature_id, message, geom, status
            ) VALUES (
                @Id, @RunId, @RuleCode, @Severity::validation_severity, @FeatureTable, @FeatureId, @Message,
                CASE WHEN @GeometryGeoJson IS NOT NULL THEN ST_SetSRID(ST_GeomFromGeoJSON(@GeometryGeoJson), 4326) ELSE NULL END,
                @Status::issue_status
            );";

        foreach (var issue in issueList)
        {
            var p = new
            {
                issue.Id,
                RunId = runId,
                issue.RuleCode,
                Severity = issue.Severity.ToString(),
                issue.FeatureTable,
                issue.FeatureId,
                issue.Message,
                issue.GeometryGeoJson,
                Status = issue.Status.ToString()
            };
            await conn.ExecuteAsync(new CommandDefinition(sql, p, tx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ValidationRunDto>> GetValidationRunsAsync(Guid? networkId, int limit = 20, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, network_id, started_by, started_at, finished_at, summary::text
            FROM validation_runs
            WHERE (@networkId IS NULL OR network_id = @networkId)
            ORDER BY started_at DESC
            LIMIT @limit;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<dynamic>(new CommandDefinition(sql, new { networkId, limit }, cancellationToken: ct));

        var result = new List<ValidationRunDto>();
        foreach (var r in rows)
        {
            ValidationSummaryDto summary;
            try
            {
                string sText = r.summary ?? "{}";
                summary = JsonSerializer.Deserialize<ValidationSummaryDto>(sText, JsonOptions)
                          ?? new ValidationSummaryDto(0, 0, 0, 0, 0, Array.Empty<string>());
            }
            catch
            {
                summary = new ValidationSummaryDto(0, 0, 0, 0, 0, Array.Empty<string>());
            }

            result.Add(new ValidationRunDto(
                (Guid)r.id,
                (Guid)r.network_id,
                (string)r.started_by,
                (DateTime)r.started_at,
                (DateTime?)r.finished_at,
                summary
            ));
        }

        return result;
    }

    public async Task<ValidationRunDto?> GetValidationRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT id, network_id, started_by, started_at, finished_at, summary::text
            FROM validation_runs
            WHERE id = @runId;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var r = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { runId }, cancellationToken: ct));
        if (r == null) return null;

        ValidationSummaryDto summary;
        try
        {
            string sText = r.summary ?? "{}";
            summary = JsonSerializer.Deserialize<ValidationSummaryDto>(sText, JsonOptions)
                      ?? new ValidationSummaryDto(0, 0, 0, 0, 0, Array.Empty<string>());
        }
        catch
        {
            summary = new ValidationSummaryDto(0, 0, 0, 0, 0, Array.Empty<string>());
        }

        return new ValidationRunDto(
            (Guid)r.id,
            (Guid)r.network_id,
            (string)r.started_by,
            (DateTime)r.started_at,
            (DateTime?)r.finished_at,
            summary
        );
    }

    public async Task<ValidationIssuePageResult> GetValidationIssuesAsync(ValidationIssueQuery query, CancellationToken ct = default)
    {
        var offset = (Math.Max(query.Page, 1) - 1) * query.PageSize;

        var countSql = @"
            SELECT COUNT(*)
            FROM validation_issues vi
            JOIN validation_runs vr ON vi.run_id = vr.id
            WHERE (@RunId IS NULL OR vi.run_id = @RunId)
              AND (@NetworkId IS NULL OR vr.network_id = @NetworkId)
              AND (@Status IS NULL OR vi.status = @Status::issue_status)
              AND (@Severity IS NULL OR vi.severity = @Severity::validation_severity)
              AND (@RuleCode IS NULL OR vi.rule_code = @RuleCode)
              AND (@FeatureTable IS NULL OR vi.feature_table = @FeatureTable);";

        var dataSql = @"
            SELECT 
                vi.id, vi.run_id, vi.rule_code, vi.severity::text, vi.feature_table, vi.feature_id,
                vi.message, ST_AsGeoJSON(vi.geom) AS geojson, vi.status::text,
                vi.acknowledged_by, vi.acknowledged_at, vi.resolved_by, vi.resolved_at,
                COALESCE(p.name, l.name, poly.name, vi.feature_id::text) AS asset_name
            FROM validation_issues vi
            JOIN validation_runs vr ON vi.run_id = vr.id
            LEFT JOIN points p ON vi.feature_table = 'points' AND vi.feature_id = p.id
            LEFT JOIN lines l ON vi.feature_table = 'lines' AND vi.feature_id = l.id
            LEFT JOIN polygons poly ON vi.feature_table = 'polygons' AND vi.feature_id = poly.id
            WHERE (@RunId IS NULL OR vi.run_id = @RunId)
              AND (@NetworkId IS NULL OR vr.network_id = @NetworkId)
              AND (@Status IS NULL OR vi.status = @Status::issue_status)
              AND (@Severity IS NULL OR vi.severity = @Severity::validation_severity)
              AND (@RuleCode IS NULL OR vi.rule_code = @RuleCode)
              AND (@FeatureTable IS NULL OR vi.feature_table = @FeatureTable)
            ORDER BY 
                CASE vi.severity WHEN 'ERROR'::validation_severity THEN 1 WHEN 'WARNING'::validation_severity THEN 2 ELSE 3 END,
                vi.id ASC
            OFFSET @offset LIMIT @limit;";

        var p = new
        {
            query.RunId,
            query.NetworkId,
            Status = query.Status?.ToString(),
            Severity = query.Severity?.ToString(),
            query.RuleCode,
            query.FeatureTable,
            offset,
            limit = query.PageSize
        };

        await using var conn = await CreateOpenConnectionAsync(ct);
        var total = await conn.ExecuteScalarAsync<int>(new CommandDefinition(countSql, p, cancellationToken: ct));
        var rows = await conn.QueryAsync<dynamic>(new CommandDefinition(dataSql, p, cancellationToken: ct));

        var items = new List<ValidationIssueDto>();
        foreach (var r in rows)
        {
            if (!Enum.TryParse<ValidationSeverity>((string)r.severity, out var sev))
            {
                sev = ValidationSeverity.ERROR;
            }

            if (!Enum.TryParse<IssueStatus>((string)r.status, out var st))
            {
                st = IssueStatus.OPEN;
            }

            items.Add(new ValidationIssueDto(
                (Guid)r.id,
                (Guid)r.run_id,
                (string)r.rule_code,
                sev,
                (string)r.feature_table,
                (Guid)r.feature_id,
                (string)r.asset_name,
                (string)r.message,
                (string?)r.geojson,
                st,
                (string?)r.acknowledged_by,
                (DateTime?)r.acknowledged_at,
                (string?)r.resolved_by,
                (DateTime?)r.resolved_at
            ));
        }

        return new ValidationIssuePageResult(items, total, query.Page, query.PageSize);
    }

    public async Task<ValidationIssueDto?> GetValidationIssueByIdAsync(Guid issueId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT 
                vi.id, vi.run_id, vi.rule_code, vi.severity::text, vi.feature_table, vi.feature_id,
                vi.message, ST_AsGeoJSON(vi.geom) AS geojson, vi.status::text,
                vi.acknowledged_by, vi.acknowledged_at, vi.resolved_by, vi.resolved_at,
                COALESCE(p.name, l.name, poly.name, vi.feature_id::text) AS asset_name
            FROM validation_issues vi
            LEFT JOIN points p ON vi.feature_table = 'points' AND vi.feature_id = p.id
            LEFT JOIN lines l ON vi.feature_table = 'lines' AND vi.feature_id = l.id
            LEFT JOIN polygons poly ON vi.feature_table = 'polygons' AND vi.feature_id = poly.id
            WHERE vi.id = @issueId;";

        await using var conn = await CreateOpenConnectionAsync(ct);
        var r = await conn.QuerySingleOrDefaultAsync<dynamic>(new CommandDefinition(sql, new { issueId }, cancellationToken: ct));
        if (r == null) return null;

        if (!Enum.TryParse<ValidationSeverity>((string)r.severity, out var sev))
        {
            sev = ValidationSeverity.ERROR;
        }

        if (!Enum.TryParse<IssueStatus>((string)r.status, out var st))
        {
            st = IssueStatus.OPEN;
        }

        return new ValidationIssueDto(
            (Guid)r.id,
            (Guid)r.run_id,
            (string)r.rule_code,
            sev,
            (string)r.feature_table,
            (Guid)r.feature_id,
            (string)r.asset_name,
            (string)r.message,
            (string?)r.geojson,
            st,
            (string?)r.acknowledged_by,
            (DateTime?)r.acknowledged_at,
            (string?)r.resolved_by,
            (DateTime?)r.resolved_at
        );
    }

    public async Task<ValidationIssueDto?> UpdateIssueStatusAsync(Guid issueId, IssueStatus status, string userName, CancellationToken ct = default)
    {
        string sql = status switch
        {
            IssueStatus.ACKNOWLEDGED => @"
                UPDATE validation_issues
                SET status = 'ACKNOWLEDGED'::issue_status,
                    acknowledged_by = @userName,
                    acknowledged_at = now()
                WHERE id = @issueId;",
            IssueStatus.RESOLVED => @"
                UPDATE validation_issues
                SET status = 'RESOLVED'::issue_status,
                    resolved_by = @userName,
                    resolved_at = now()
                WHERE id = @issueId;",
            _ => @"
                UPDATE validation_issues
                SET status = 'OPEN'::issue_status,
                    acknowledged_by = NULL,
                    acknowledged_at = NULL,
                    resolved_by = NULL,
                    resolved_at = NULL
                WHERE id = @issueId;"
        };

        await using var conn = await CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { issueId, userName }, cancellationToken: ct));
        if (affected == 0) return null;

        return await GetValidationIssueByIdAsync(issueId, ct);
    }
}
