namespace UtilityNetwork.Seed;

public sealed record SeedPointRecord(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    bool? IsOpen,
    string AttributesJson,
    double X,
    double Y,
    string CreatedBy,
    DateTime CreatedAt,
    string? ExternalId,
    string Source
);

public sealed record SeedLineRecord(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    string FlowDirection,
    int? DiameterMm,
    string? Material,
    string AttributesJson,
    IReadOnlyList<double[]> Coordinates,
    string CreatedBy,
    DateTime CreatedAt,
    string? ExternalId,
    string Source
);

public sealed record SeedPolygonRecord(
    Guid Id,
    Guid NetworkId,
    string FeatureTypeCode,
    string Name,
    string Status,
    string AttributesJson,
    IReadOnlyList<IReadOnlyList<double[]>> Rings,
    string CreatedBy,
    DateTime CreatedAt,
    string? ExternalId,
    string Source
);

public sealed record SeedDataset(
    IReadOnlyList<SeedPointRecord> Points,
    IReadOnlyList<SeedLineRecord> Lines,
    IReadOnlyList<SeedPolygonRecord> Polygons,
    IReadOnlyList<string> PlantedErrorsDescription
);
