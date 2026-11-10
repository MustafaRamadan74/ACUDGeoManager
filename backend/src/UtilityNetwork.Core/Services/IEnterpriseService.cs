using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IEnterpriseService
{
    // Attachments
    Task<IReadOnlyList<FeatureAttachmentDto>> GetAttachmentsAsync(Guid featureId, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType, string FileName)> DownloadAttachmentAsync(Guid attachmentId, CancellationToken ct = default);
    Task<FeatureAttachmentDto> UploadAttachmentAsync(
        Guid featureId,
        string featureTable,
        Stream fileStream,
        string fileName,
        string contentType,
        long fileSizeBytes,
        string? description,
        string uploadedBy,
        CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken ct = default);

    // Relationships
    Task<IReadOnlyList<FeatureRelationshipDto>> GetRelationshipsAsync(Guid featureId, CancellationToken ct = default);
    Task<FeatureRelationshipDto> CreateRelationshipAsync(CreateRelationshipRequest request, string createdBy, CancellationToken ct = default);
    Task<bool> DeleteRelationshipAsync(Guid relationshipId, CancellationToken ct = default);

    // Locks
    Task<FeatureLockDto?> GetLockStatusAsync(Guid featureId, Guid? currentUserId = null, CancellationToken ct = default);
    Task<FeatureLockDto> AcquireLockAsync(Guid featureId, string featureTable, Guid userId, string username, int ttlMinutes, string? clientSessionId, CancellationToken ct = default);
    Task<bool> ReleaseLockAsync(Guid featureId, Guid userId, bool isAdmin = false, CancellationToken ct = default);
    Task<bool> RefreshLockAsync(Guid featureId, Guid userId, int ttlMinutes, CancellationToken ct = default);

    // Data Quality
    Task<DataQualityDashboardDto> GetDataQualityDashboardAsync(Guid? networkId = null, CancellationToken ct = default);
}
