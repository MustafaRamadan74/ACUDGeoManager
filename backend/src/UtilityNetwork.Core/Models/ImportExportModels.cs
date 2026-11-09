using System.ComponentModel.DataAnnotations;

namespace UtilityNetwork.Core.Models;

public enum ExportFormat
{
    GeoJson,
    Csv
}

public sealed record ExportFilterRequest(
    Guid? NetworkId = null,
    string? FeatureTypeCode = null,
    string? Bbox = null,
    int Srid = 4326
);

public sealed record ImportPreviewRequest(
    Guid NetworkId,
    string? FeatureTypeCode = null,
    int Srid = 4326,
    string? Content = null
);

public sealed record ImportRowErrorDto(
    int RowIndex,
    string Field,
    string Error
);

public sealed record ImportPreviewRowDto(
    int RowIndex,
    string Name,
    string FeatureTypeCode,
    string GeometryKind,
    string GeometrySummary,
    string Status,
    bool IsValid,
    bool IsDuplicate,
    string? DuplicateReason = null,
    string? Attributes = null
);

public sealed record ImportPreviewResultDto(
    Guid PreviewToken,
    int TotalFeatures,
    int ValidCount,
    int InvalidCount,
    int DuplicateCount,
    IReadOnlyList<ImportRowErrorDto> Errors,
    IReadOnlyList<ImportPreviewRowDto> SampleRows
);

public sealed record ImportCommitRequest(
    Guid PreviewToken,
    Guid NetworkId,
    bool SkipDuplicates = true
);

public sealed record ImportCommitResultDto(
    Guid ChangeSetId,
    int ImportedCount,
    int SkippedCount,
    IReadOnlyList<Guid> FeatureIds
);

public sealed class ParsedImportFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int RowIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeatureTypeCode { get; set; } = string.Empty;
    public string GeometryKind { get; set; } = string.Empty;
    public string Status { get; set; } = "ACTIVE";
    public string? ExternalId { get; set; }
    public string? AttributesJson { get; set; }
    public string WktOrGeoJson { get; set; } = string.Empty;
    public bool? IsOpen { get; set; }
    public string? FlowDirection { get; set; }
    public int? DiameterMm { get; set; }
    public string? Material { get; set; }
    public bool IsValid { get; set; }
    public bool IsDuplicate { get; set; }
    public string? DuplicateReason { get; set; }
    public List<ImportRowErrorDto> Errors { get; set; } = new();
}

