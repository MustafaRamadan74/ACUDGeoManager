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

public sealed record TypeStatusCountDto(
    string FeatureTypeCode,
    string Status,
    int Count);

public sealed record NetworkSummaryDto(
    Guid NetworkId,
    string NetworkName,
    string NetworkDomain,
    int TotalFeatures,
    IReadOnlyDictionary<string, int> CountsByType,
    IReadOnlyDictionary<string, int> CountsByStatus,
    IReadOnlyList<TypeStatusCountDto> Details);
