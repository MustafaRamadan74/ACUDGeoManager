namespace UtilityNetwork.Core.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public sealed record AuditLogEntry(
    long Id,
    Guid? ChangeSetId,
    string TableName,
    Guid RecordId,
    string Action,
    string? OldData,
    string? NewData,
    Guid? UserId,
    string Username,
    string Role,
    DateTime ChangedAt,
    string Reason,
    string? RequestId);

public sealed record HistoryFilter(
    bool? OnlyMe,
    Guid? UserId,
    Guid? FeatureId,
    string? TableName,
    DateTime? FromDate,
    DateTime? ToDate,
    int Page = 1,
    int PageSize = 50);
