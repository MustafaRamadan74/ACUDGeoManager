using System.Text.Json;

namespace UtilityNetwork.Core.Models;

public sealed record NetworkDto(
    Guid Id,
    string Code,
    string Name,
    string Domain,
    bool IsDirected,
    double SnapToleranceM,
    DateTime CreatedAt);

public sealed record FeatureTypeDto(
    string Code,
    string NetworkDomain,
    string GeometryKind,
    string DisplayName,
    string TopologyRole,
    JsonElement AttributeSchema,
    JsonElement Style,
    bool IsActive);
