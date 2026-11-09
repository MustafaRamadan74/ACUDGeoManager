using System.Text.Json.Serialization;

namespace UtilityNetwork.Core.Models;

/// <summary>
/// Request for computing a geodesic buffer around a geometry or feature.
/// </summary>
public sealed class BufferAnalysisRequest
{
    public string? GeometryGeoJson { get; set; }
    public Guid? FeatureId { get; set; }
    public double DistanceMeters { get; set; } = 50.0;
    public Guid? NetworkId { get; set; }
    public IReadOnlyList<string>? FeatureTypeCodes { get; set; }
}

/// <summary>
/// Summary item for features intersecting a buffer or zone.
/// </summary>
public sealed record IntersectingFeatureItem(
    Guid Id,
    string FeatureTypeCode,
    string Name,
    string GeometryKind,
    string GeometryGeoJson,
    string Status,
    double? LengthMeters,
    double? AreaSqM
);

/// <summary>
/// Result of a geodesic buffer analysis.
/// </summary>
public sealed record BufferAnalysisResultDto(
    string BufferPolygonGeoJson,
    double DistanceMeters,
    int IntersectingFeaturesCount,
    double TotalPipeLengthMeters,
    double TotalAreaSqM,
    IReadOnlyList<IntersectingFeatureItem> IntersectingFeatures
);

/// <summary>
/// Request for computing multi-ring service areas around a geographic point.
/// </summary>
public sealed class ServiceAreaRequest
{
    public double CenterLon { get; set; }
    public double CenterLat { get; set; }
    public IReadOnlyList<double> RadiiMeters { get; set; } = new List<double> { 50, 100, 250 };
    public Guid? NetworkId { get; set; }
}

/// <summary>
/// Single ring summary in a service area calculation.
/// </summary>
public sealed record ServiceAreaRingDto(
    double RadiusMeters,
    string PolygonGeoJson,
    int FeaturesCount,
    int ValvesCount,
    double PipeLengthMeters
);

/// <summary>
/// Result of multi-ring service area analysis.
/// </summary>
public sealed record ServiceAreaResultDto(
    double CenterLon,
    double CenterLat,
    IReadOnlyList<ServiceAreaRingDto> Rings
);

/// <summary>
/// Request for spatial join count (e.g. assets inside administrative or service zones).
/// </summary>
public sealed class SpatialJoinRequest
{
    public string PolygonTypeCode { get; set; } = "URBAN_DISTRICT";
    public string TargetTypeCode { get; set; } = "WATER_VALVE";
    public Guid? NetworkId { get; set; }
}

/// <summary>
/// Summary item representing one polygon feature and its contained assets count.
/// </summary>
public sealed record SpatialJoinItemDto(
    Guid PolygonId,
    string PolygonName,
    int ContainedCount,
    double TotalContainedLengthMeters,
    string GeometryGeoJson
);

/// <summary>
/// Result of a spatial join analysis.
/// </summary>
public sealed record SpatialJoinResultDto(
    string PolygonTypeCode,
    string TargetTypeCode,
    int TotalPolygons,
    int TotalContainedCount,
    IReadOnlyList<SpatialJoinItemDto> Items
);

/// <summary>
/// Aggregated statistics breakdown by feature type.
/// </summary>
public sealed record TypeStatsDto(
    string FeatureTypeCode,
    string DisplayName,
    string GeometryKind,
    int Count,
    double TotalLengthMeters,
    double TotalAreaSqM
);

/// <summary>
/// Aggregated statistics breakdown by status.
/// </summary>
public sealed record StatusStatsDto(
    string Status,
    int Count
);

/// <summary>
/// Aggregated statistics breakdown for pipes by diameter.
/// </summary>
public sealed record PipeDiameterStatsDto(
    int DiameterMm,
    int Count,
    double TotalLengthMeters
);

/// <summary>
/// Aggregated statistics breakdown for valves by state (open/closed).
/// </summary>
public sealed record ValveStateStatsDto(
    string State,
    int Count
);

/// <summary>
/// Comprehensive network statistics for charting and reporting.
/// </summary>
public sealed record NetworkStatisticsDto(
    Guid NetworkId,
    int TotalFeatures,
    double TotalPipeLengthMeters,
    double TotalAreaSqM,
    IReadOnlyList<TypeStatsDto> ByType,
    IReadOnlyList<StatusStatsDto> ByStatus,
    IReadOnlyList<PipeDiameterStatsDto> PipesByDiameter,
    IReadOnlyList<ValveStateStatsDto> ValvesByState
);
