using System.Text.Json;
using System.Text.Json.Serialization;

namespace UtilityNetwork.Core.Models;

public sealed record KeysetPagedResult<T>(
    IReadOnlyList<T> Items,
    Guid? NextCursor,
    bool HasMore,
    int TotalCount);

public sealed record FeatureQueryFilter(
    Guid? NetworkId = null,
    string? FeatureTypeCode = null,
    string? Status = null,
    double? MinX = null,
    double? MinY = null,
    double? MaxX = null,
    double? MaxY = null,
    Guid? AfterId = null,
    int Limit = 50);

// --- POINT ---
public sealed record PointFeatureDto(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    double X,
    double Y,
    bool? IsOpen,
    long? NodeId,
    int Version,
    string CreatedBy,
    string UpdatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreatePointRequest(
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    double X,
    double Y,
    bool? IsOpen);

public sealed record UpdatePointRequest(
    string? Name,
    string? Status,
    JsonElement? Attributes,
    double? X,
    double? Y,
    bool? IsOpen,
    int Version);

// --- LINE ---
public sealed record LineFeatureDto(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    IReadOnlyList<double[]> Coordinates,
    string FlowDirection,
    int? DiameterMm,
    string? Material,
    double LengthM,
    int Version,
    string CreatedBy,
    string UpdatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreateLineRequest(
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    IReadOnlyList<double[]> Coordinates,
    string FlowDirection,
    int? DiameterMm,
    string? Material);

public sealed record UpdateLineRequest(
    string? Name,
    string? Status,
    JsonElement? Attributes,
    IReadOnlyList<double[]>? Coordinates,
    string? FlowDirection,
    int? DiameterMm,
    string? Material,
    int Version);

// --- POLYGON ---
public sealed record PolygonFeatureDto(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double[]>>> Coordinates,
    double AreaSqm,
    int Version,
    string CreatedBy,
    string UpdatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CreatePolygonRequest(
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    JsonElement Attributes,
    IReadOnlyList<IReadOnlyList<double[]>> RingCoordinates);

public sealed record UpdatePolygonRequest(
    string? Name,
    string? Status,
    JsonElement? Attributes,
    IReadOnlyList<IReadOnlyList<double[]>>? RingCoordinates,
    int Version);

// --- MAP GEOJSON ---
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "RFC 7946 GeoJSON FeatureCollection")]
public sealed record GeoJsonFeatureCollection
{
    public const string CollectionType = "FeatureCollection";

    public string Type { get; init; } = CollectionType;
    public IReadOnlyList<GeoJsonFeature> Features { get; init; } = [];

    [JsonConstructor]
    public GeoJsonFeatureCollection(string type, IReadOnlyList<GeoJsonFeature> features)
    {
        Type = type;
        Features = features;
    }

    public GeoJsonFeatureCollection(IReadOnlyList<GeoJsonFeature> features) 
        : this(CollectionType, features) { }
}

public sealed record GeoJsonFeature
{
    public const string DefaultType = "Feature";

    public string Type { get; init; } = DefaultType;
    public Guid Id { get; init; }
    public GeoJsonGeometry Geometry { get; init; }
    public Dictionary<string, object?> Properties { get; init; }

    [JsonConstructor]
    public GeoJsonFeature(string type, Guid id, GeoJsonGeometry geometry, Dictionary<string, object?> properties)
    {
        Type = type;
        Id = id;
        Geometry = geometry;
        Properties = properties;
    }

    public GeoJsonFeature(Guid id, GeoJsonGeometry geometry, Dictionary<string, object?> properties) 
        : this(DefaultType, id, geometry, properties) { }
}

public sealed record GeoJsonGeometry(
    string Type,
    object Coordinates);
