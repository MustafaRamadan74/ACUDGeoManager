namespace UtilityNetwork.Core.Models;

public sealed record UserPresenceDto(
    string ConnectionId,
    Guid UserId,
    string Username,
    string Role,
    Guid? ActiveWorkspaceId,
    DateTimeOffset ConnectedAt
);

public sealed record FeatureChangeEvent(
    string Action, // "CREATED", "UPDATED", "DELETED"
    Guid WorkspaceId,
    string FeatureTable,
    Guid FeatureId,
    string FeatureTypeCode,
    string? FeatureName,
    string ChangedBy,
    DateTimeOffset ChangedAt
);

public sealed record FeatureEditingEvent(
    Guid WorkspaceId,
    string FeatureTable,
    Guid FeatureId,
    Guid UserId,
    string Username,
    bool IsEditing,
    DateTimeOffset Timestamp
);

public sealed record ValidationCompletedEvent(
    Guid RunId,
    Guid? WorkspaceId,
    string Status,
    long TotalIssuesCount,
    DateTimeOffset CompletedAt
);
