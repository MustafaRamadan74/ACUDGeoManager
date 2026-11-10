using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IEnterpriseRepository
{
    // Attachments
    Task<IReadOnlyList<FeatureAttachmentDto>> GetAttachmentsByFeatureAsync(Guid featureId, CancellationToken ct = default);
    Task<FeatureAttachmentDto?> GetAttachmentByIdAsync(Guid id, CancellationToken ct = default);
    Task<FeatureAttachmentDto> AddAttachmentAsync(FeatureAttachmentDto attachment, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(Guid id, CancellationToken ct = default);

    // Relationships
    Task<IReadOnlyList<FeatureRelationshipDto>> GetRelationshipsByFeatureAsync(Guid featureId, CancellationToken ct = default);
    Task<FeatureRelationshipDto> AddRelationshipAsync(FeatureRelationshipDto relationship, CancellationToken ct = default);
    Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken ct = default);

    // Locks
    Task<FeatureLockDto?> GetLockAsync(Guid featureId, Guid? currentUserId = null, CancellationToken ct = default);
    Task<(bool Success, FeatureLockDto LockInfo)> TryAcquireLockAsync(Guid featureId, string featureTable, Guid userId, string username, int ttlMinutes, string? clientSessionId, CancellationToken ct = default);
    Task<bool> ReleaseLockAsync(Guid featureId, Guid userId, bool isAdmin = false, CancellationToken ct = default);
    Task<bool> RefreshLockAsync(Guid featureId, Guid userId, int ttlMinutes, CancellationToken ct = default);
    Task ClearExpiredLocksAsync(CancellationToken ct = default);

    // Data Quality
    Task<IReadOnlyList<LayerDataQualityDto>> GetDataQualityReportAsync(Guid? networkId, CancellationToken ct = default);
}

