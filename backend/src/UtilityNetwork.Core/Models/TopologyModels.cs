namespace UtilityNetwork.Core.Models;

/// <summary>
/// Status and metrics for network topology state.
/// </summary>
public sealed record TopologyStatusDto(
    Guid NetworkId,
    bool IsDirty,
    DateTime? LastRebuiltAt,
    int NodeCount,
    int EdgeCount,
    int DirtyFeatureCount
);

/// <summary>
/// Geometric and topological node connecting utility network line endpoints.
/// </summary>
public sealed record TopologyNodeDto(
    long Id,
    Guid NetworkId,
    double X,
    double Y,
    string NodeRole,
    double? ElevationM,
    int AttachedFeatureCount
);

/// <summary>
/// Topological edge corresponding to a network line asset with effective flow direction.
/// </summary>
public sealed record TopologyEdgeDto(
    long Id,
    Guid NetworkId,
    Guid LineFeatureId,
    long FromNodeId,
    long ToNodeId,
    string FlowDirection,
    double LengthM
);

/// <summary>
/// Attached point asset feature associated with a topology node.
/// </summary>
public sealed record TopologyNodeFeatureDto(
    long NodeId,
    Guid PointId,
    string FeatureTypeCode,
    string AssetName,
    double DistanceM
);

/// <summary>
/// Result of a topology rebuild operation.
/// </summary>
public sealed record RebuildTopologyResultDto(
    Guid NetworkId,
    bool IsDirty,
    int NodeCount,
    int EdgeCount,
    DateTime RebuiltAt,
    long DurationMs,
    string Message
);

