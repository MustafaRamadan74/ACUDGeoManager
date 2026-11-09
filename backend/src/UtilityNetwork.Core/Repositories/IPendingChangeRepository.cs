using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IPendingChangeRepository
{
    Task<Guid> CreatePendingChangeAsync(
        Guid networkId,
        string featureTypeCode,
        string geometryKind,
        Guid? featureId,
        string operation,
        string name,
        string status,
        string? geometryGeoJson,
        string attributesJson,
        bool? isOpen,
        string? flowDirection,
        int? diameterMm,
        string? material,
        int? targetVersion,
        Guid proposerUserId,
        CancellationToken ct = default);

    Task<PendingChangeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PendingChangeDto>> GetPendingQueueAsync(Guid? networkId = null, CancellationToken ct = default);
    Task<bool> MarkApprovedAsync(Guid id, Guid reviewerUserId, CancellationToken ct = default);
    Task<bool> MarkRejectedAsync(Guid id, Guid reviewerUserId, string reason, CancellationToken ct = default);
}
