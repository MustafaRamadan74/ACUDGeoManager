namespace UtilityNetwork.Core.Models;

/// <summary>
/// Status of a proposed change in the approval queue.
/// </summary>
public enum ReviewStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// DTO representing a proposed feature modification awaiting approval.
/// </summary>
public sealed record PendingChangeDto(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string GeometryKind,
    Guid? FeatureId,
    string Operation,
    string Name,
    string Status,
    string? GeometryGeoJson,
    string AttributesJson,
    bool? IsOpen,
    string? FlowDirection,
    int? DiameterMm,
    string? Material,
    int? TargetVersion,
    Guid ProposerUserId,
    string ProposerUsername,
    DateTime ProposedAt,
    string ReviewStatus,
    Guid? ReviewerUserId,
    string? ReviewerUsername,
    DateTime? ReviewedAt,
    string? RejectionReason
);

/// <summary>
/// Request payload to reject a pending change.
/// </summary>
public sealed class RejectPendingChangeRequest
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Result returned when a feature edit is submitted to the approval queue.
/// </summary>
public sealed record PendingChangeSubmittedDto(
    Guid PendingChangeId,
    bool IsPendingApproval,
    string Operation,
    string FeatureTypeCode,
    string Message
);

/// <summary>
/// Result returned when an approver reviews a pending change.
/// </summary>
public sealed record ApprovalResultDto(
    Guid PendingChangeId,
    string ReviewStatus,
    Guid? AppliedFeatureId,
    string Message
);

/// <summary>
/// Request to update a feature type's approval requirement flag.
/// </summary>
public sealed class UpdateLayerApprovalFlagRequest
{
    public bool RequiresApproval { get; set; }
}
