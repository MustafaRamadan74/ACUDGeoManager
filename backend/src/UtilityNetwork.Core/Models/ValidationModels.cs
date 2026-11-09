using System.Text.Json;
using System.Text.Json.Serialization;

namespace UtilityNetwork.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ValidationSeverity
{
    ERROR,
    WARNING,
    INFO
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IssueStatus
{
    OPEN,
    ACKNOWLEDGED,
    RESOLVED
}

public sealed record ValidationSummaryDto(
    int TotalIssues,
    int ErrorsCount,
    int WarningsCount,
    int InfosCount,
    long DurationMs,
    IReadOnlyList<string> RulesExecuted);

public sealed record ValidationRunDto(
    Guid Id,
    Guid NetworkId,
    string StartedBy,
    DateTime StartedAt,
    DateTime? FinishedAt,
    ValidationSummaryDto Summary);

public sealed record ValidationIssueDto(
    Guid Id,
    Guid RunId,
    string RuleCode,
    ValidationSeverity Severity,
    string FeatureTable,
    Guid FeatureId,
    string AssetName,
    string Message,
    string? GeometryGeoJson,
    IssueStatus Status,
    string? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    string? ResolvedBy,
    DateTime? ResolvedAt);

public sealed record ValidationIssueQuery(
    Guid? RunId = null,
    Guid? NetworkId = null,
    IssueStatus? Status = null,
    ValidationSeverity? Severity = null,
    string? RuleCode = null,
    string? FeatureTable = null,
    int Page = 1,
    int PageSize = 50);

public sealed record ValidationIssuePageResult(
    IReadOnlyList<ValidationIssueDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UpdateIssueStatusRequest(
    IssueStatus Status);

public sealed record ValidationContext(
    Guid NetworkId,
    string StartedBy,
    string ConnectionString);
