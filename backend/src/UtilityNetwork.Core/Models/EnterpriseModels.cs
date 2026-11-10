namespace UtilityNetwork.Core.Models;

public sealed class FeatureAttachmentDto
{
    public Guid Id { get; set; }
    public Guid FeatureId { get; set; }
    public string FeatureTable { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }

    public FeatureAttachmentDto() { }

    public FeatureAttachmentDto(
        Guid id,
        Guid featureId,
        string featureTable,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string filePath,
        string? description,
        string uploadedBy,
        DateTimeOffset uploadedAt)
    {
        Id = id;
        FeatureId = featureId;
        FeatureTable = featureTable;
        FileName = fileName;
        ContentType = contentType;
        FileSizeBytes = fileSizeBytes;
        FilePath = filePath;
        Description = description;
        UploadedBy = uploadedBy;
        UploadedAt = uploadedAt;
    }
}

public sealed class FeatureRelationshipDto
{
    public Guid Id { get; set; }
    public Guid SourceFeatureId { get; set; }
    public string SourceTable { get; set; } = string.Empty;
    public Guid TargetFeatureId { get; set; }
    public string TargetTable { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string? SourceFeatureName { get; set; }
    public string? TargetFeatureName { get; set; }
    public string? SourceFeatureTypeCode { get; set; }
    public string? TargetFeatureTypeCode { get; set; }

    public FeatureRelationshipDto() { }

    public FeatureRelationshipDto(
        Guid id,
        Guid sourceFeatureId,
        string sourceTable,
        Guid targetFeatureId,
        string targetTable,
        string relationshipType,
        string? description,
        string createdBy,
        DateTimeOffset createdAt,
        string? sourceFeatureName = null,
        string? targetFeatureName = null,
        string? sourceFeatureTypeCode = null,
        string? targetFeatureTypeCode = null)
    {
        Id = id;
        SourceFeatureId = sourceFeatureId;
        SourceTable = sourceTable;
        TargetFeatureId = targetFeatureId;
        TargetTable = targetTable;
        RelationshipType = relationshipType;
        Description = description;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        SourceFeatureName = sourceFeatureName;
        TargetFeatureName = targetFeatureName;
        SourceFeatureTypeCode = sourceFeatureTypeCode;
        TargetFeatureTypeCode = targetFeatureTypeCode;
    }
}

public sealed record CreateRelationshipRequest(
    Guid SourceFeatureId,
    string SourceTable,
    Guid TargetFeatureId,
    string TargetTable,
    string RelationshipType,
    string? Description
);

public sealed record FeatureLockDto(
    Guid FeatureId,
    string FeatureTable,
    Guid LockedByUserId,
    string LockedByUsername,
    DateTimeOffset LockedAt,
    DateTimeOffset ExpiresAt,
    string? ClientSessionId,
    bool IsLocked,
    bool IsLockedByCurrentUser
);

public sealed record AcquireLockRequest(
    string FeatureTable,
    int? TtlMinutes = 5,
    string? ClientSessionId = null
);

public sealed class LayerDataQualityDto
{
    public string FeatureTypeCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GeometryKind { get; set; } = string.Empty;
    public long TotalCount { get; set; }
    public long ValidGeomCount { get; set; }
    public long OpenIssuesCount { get; set; }
    public decimal CompletenessPct { get; set; }
    public long AttachmentsCount { get; set; }
    public long RelationshipsCount { get; set; }

    public LayerDataQualityDto() { }
}

public sealed record DataQualityDashboardDto(
    Guid? NetworkId,
    long TotalFeatures,
    long TotalValidGeometries,
    long TotalOpenIssues,
    decimal OverallCompletenessPct,
    long TotalAttachments,
    long TotalRelationships,
    IReadOnlyList<LayerDataQualityDto> Layers
);
