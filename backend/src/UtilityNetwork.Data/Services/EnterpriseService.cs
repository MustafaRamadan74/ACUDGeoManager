using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class EnterpriseService : IEnterpriseService
{
    private readonly IEnterpriseRepository _repository;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".dwg", ".dxf", ".csv", ".docx", ".xlsx"
    };

    private static readonly HashSet<string> AllowedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "points", "lines", "polygons"
    };

    private static readonly HashSet<string> AllowedRelationshipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTAINS", "SERVES", "ATTACHED_TO", "FEEDS", "ASSOCIATED_WITH"
    };

    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    public EnterpriseService(IEnterpriseRepository repository)
    {
        _repository = repository;
    }

    // Attachments
    public async Task<IReadOnlyList<FeatureAttachmentDto>> GetAttachmentsAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _repository.GetAttachmentsByFeatureAsync(featureId, ct);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId, ct);
        if (attachment is null)
        {
            throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        }

        if (!File.Exists(attachment.FilePath))
        {
            throw new FileNotFoundException($"Attachment file does not exist on disk: {attachment.FilePath}");
        }

        var stream = new FileStream(attachment.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return (stream, attachment.ContentType, attachment.FileName);
    }

    public async Task<FeatureAttachmentDto> UploadAttachmentAsync(
        Guid featureId,
        string featureTable,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string? description,
        string uploadedBy,
        CancellationToken ct = default)
    {
        var normalizedTable = featureTable.Trim().ToLowerInvariant();
        if (!AllowedTables.Contains(normalizedTable))
        {
            throw new ArgumentException($"Invalid feature table '{featureTable}'. Must be points, lines, or polygons.");
        }

        if (fileSizeBytes <= 0 || fileSizeBytes > MaxFileSizeBytes)
        {
            throw new ArgumentException($"File size must be between 1 byte and {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException($"File extension '{ext}' is not permitted. Allowed: {string.Join(", ", AllowedExtensions)}");
        }

        var baseUploadDir = Path.Combine(AppContext.BaseDirectory, "uploads", "attachments", featureId.ToString());
        Directory.CreateDirectory(baseUploadDir);

        var sanitizedFileName = Path.GetFileName(fileName);
        var uniqueFileName = $"{Guid.NewGuid():N}_{sanitizedFileName}";
        var targetFilePath = Path.Combine(baseUploadDir, uniqueFileName);

        await using (var targetStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(targetStream, ct);
        }

        var attachment = new FeatureAttachmentDto(
            Guid.NewGuid(),
            featureId,
            normalizedTable,
            sanitizedFileName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            fileSizeBytes,
            targetFilePath,
            description,
            uploadedBy,
            DateTimeOffset.UtcNow
        );

        return await _repository.AddAttachmentAsync(attachment, ct);
    }

    public async Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId, ct);
        if (attachment is null)
        {
            return false;
        }

        var deleted = await _repository.DeleteAttachmentAsync(attachmentId, ct);
        if (deleted && File.Exists(attachment.FilePath))
        {
            try
            {
                File.Delete(attachment.FilePath);
            }
            catch
            {
                // File deletion failure shouldn't fail DB record removal
            }
        }

        return deleted;
    }

    // Relationships
    public async Task<IReadOnlyList<FeatureRelationshipDto>> GetRelationshipsAsync(Guid featureId, CancellationToken ct = default)
    {
        return await _repository.GetRelationshipsByFeatureAsync(featureId, ct);
    }

    public async Task<FeatureRelationshipDto> CreateRelationshipAsync(CreateRelationshipRequest request, string createdBy, CancellationToken ct = default)
    {
        var srcTable = request.SourceTable.Trim().ToLowerInvariant();
        var tgtTable = request.TargetTable.Trim().ToLowerInvariant();

        if (!AllowedTables.Contains(srcTable) || !AllowedTables.Contains(tgtTable))
        {
            throw new ArgumentException("Source and Target tables must be one of: points, lines, polygons.");
        }

        if (request.SourceFeatureId == request.TargetFeatureId && srcTable == tgtTable)
        {
            throw new ArgumentException("A feature cannot create a relationship with itself.");
        }

        var relType = request.RelationshipType.Trim().ToUpperInvariant();
        if (!AllowedRelationshipTypes.Contains(relType))
        {
            throw new ArgumentException($"Invalid relationship type '{request.RelationshipType}'. Allowed: {string.Join(", ", AllowedRelationshipTypes)}");
        }

        var relationship = new FeatureRelationshipDto(
            Guid.NewGuid(),
            request.SourceFeatureId,
            srcTable,
            request.TargetFeatureId,
            tgtTable,
            relType,
            request.Description?.Trim(),
            createdBy,
            DateTimeOffset.UtcNow
        );

        return await _repository.AddRelationshipAsync(relationship, ct);
    }

    public async Task<bool> DeleteRelationshipAsync(Guid relationshipId, CancellationToken ct = default)
    {
        return await _repository.DeleteRelationshipAsync(relationshipId, ct);
    }

    // Locks
    public async Task<FeatureLockDto?> GetLockStatusAsync(Guid featureId, Guid? currentUserId = null, CancellationToken ct = default)
    {
        return await _repository.GetLockAsync(featureId, currentUserId, ct);
    }

    public async Task<FeatureLockDto> AcquireLockAsync(
        Guid featureId,
        string featureTable,
        Guid userId,
        string username,
        int ttlMinutes,
        string? clientSessionId,
        CancellationToken ct = default)
    {
        var table = featureTable.Trim().ToLowerInvariant();
        if (!AllowedTables.Contains(table))
        {
            throw new ArgumentException($"Invalid feature table '{featureTable}'.");
        }

        var boundedTtl = Math.Clamp(ttlMinutes <= 0 ? 5 : ttlMinutes, 1, 30);
        var (success, lockInfo) = await _repository.TryAcquireLockAsync(featureId, table, userId, username, boundedTtl, clientSessionId, ct);

        if (!success)
        {
            throw new InvalidOperationException($"Feature '{featureId}' is currently locked by user '{lockInfo.LockedByUsername}' until {lockInfo.ExpiresAt:u}.");
        }

        return lockInfo;
    }

    public async Task<bool> ReleaseLockAsync(Guid featureId, Guid userId, bool isAdmin = false, CancellationToken ct = default)
    {
        return await _repository.ReleaseLockAsync(featureId, userId, isAdmin, ct);
    }

    public async Task<bool> RefreshLockAsync(Guid featureId, Guid userId, int ttlMinutes, CancellationToken ct = default)
    {
        var boundedTtl = Math.Clamp(ttlMinutes <= 0 ? 5 : ttlMinutes, 1, 30);
        return await _repository.RefreshLockAsync(featureId, userId, boundedTtl, ct);
    }

    // Data Quality Dashboard
    public async Task<DataQualityDashboardDto> GetDataQualityDashboardAsync(Guid? networkId = null, CancellationToken ct = default)
    {
        var layers = await _repository.GetDataQualityReportAsync(networkId, ct);

        long totalFeatures = layers.Sum(l => l.TotalCount);
        long totalValid = layers.Sum(l => l.ValidGeomCount);
        long totalIssues = layers.Sum(l => l.OpenIssuesCount);
        long totalAttachments = layers.Sum(l => l.AttachmentsCount);
        long totalRelationships = layers.Sum(l => l.RelationshipsCount);

        decimal overallCompleteness = totalFeatures > 0
            ? Math.Round((decimal)totalValid / totalFeatures * 100m, 1)
            : 100m;

        return new DataQualityDashboardDto(
            networkId,
            totalFeatures,
            totalValid,
            totalIssues,
            overallCompleteness,
            totalAttachments,
            totalRelationships,
            layers
        );
    }
}

