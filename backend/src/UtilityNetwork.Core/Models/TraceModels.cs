using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace UtilityNetwork.Core.Models;

public record NetworkTraceRequest(
    Guid NetworkId,
    string Mode, // "UPSTREAM" | "DOWNSTREAM" | "ISOLATION"
    Guid StartFeatureId,
    string StartKind, // "LINE" | "POINT"
    int MaxDepth = 150,
    int MaxEdges = 5000
);

public record TraceSummaryDto(
    [property: JsonPropertyName("totalLengthM")] double TotalLengthM,
    [property: JsonPropertyName("edgeCount")] int EdgeCount,
    [property: JsonPropertyName("nodeCount")] int NodeCount,
    [property: JsonPropertyName("valvesToCloseCount")] int ValvesToCloseCount,
    [property: JsonPropertyName("affectedZonesCount")] int AffectedZonesCount,
    [property: JsonPropertyName("executionMs")] double ExecutionMs
);

public record TraceEdgeDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("line_id")] Guid LineId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("diameter_mm")] int? DiameterMm,
    [property: JsonPropertyName("material")] string? Material,
    [property: JsonPropertyName("direction")] string? Direction,
    [property: JsonPropertyName("length_m")] double LengthM,
    [property: JsonPropertyName("geojson")] JsonNode? Geojson
);

public record TraceNodeDto(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("node_role")] string? NodeRole,
    [property: JsonPropertyName("elevation_m")] double? ElevationM,
    [property: JsonPropertyName("geojson")] JsonNode? Geojson
);

public record TraceValveDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("valve_type")] string? ValveType,
    [property: JsonPropertyName("is_open")] bool IsOpen,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("coordinates")] double[] Coordinates,
    [property: JsonPropertyName("geojson")] JsonNode? Geojson
);

public record TraceAffectedPolygonDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("feature_type_code")] string FeatureTypeCode,
    [property: JsonPropertyName("geojson")] JsonNode? Geojson
);

public record NetworkTraceResultDto(
    [property: JsonPropertyName("networkId")] Guid NetworkId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("startFeatureId")] Guid StartFeatureId,
    [property: JsonPropertyName("startKind")] string StartKind,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("summary")] TraceSummaryDto Summary,
    [property: JsonPropertyName("edges")] IReadOnlyList<TraceEdgeDto> Edges,
    [property: JsonPropertyName("nodes")] IReadOnlyList<TraceNodeDto> Nodes,
    [property: JsonPropertyName("valvesToClose")] IReadOnlyList<TraceValveDto> ValvesToClose,
    [property: JsonPropertyName("affectedPolygons")] IReadOnlyList<TraceAffectedPolygonDto> AffectedPolygons
);

public record TraceReportRequest(
    NetworkTraceResultDto TraceResult,
    string NetworkName,
    string Format = "PDF" // "PDF" | "CSV"
);
