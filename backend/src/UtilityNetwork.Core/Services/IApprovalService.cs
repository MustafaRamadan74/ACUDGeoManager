using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IApprovalService
{
    Task<IReadOnlyList<PendingChangeDto>> GetPendingQueueAsync(Guid? networkId, ClaimsPrincipal approver, CancellationToken ct = default);
    Task<PendingChangeDto?> GetPendingChangeByIdAsync(Guid id, ClaimsPrincipal approver, CancellationToken ct = default);
    Task<ApprovalResultDto> ApproveChangeAsync(Guid id, ClaimsPrincipal approver, CancellationToken ct = default);
    Task<ApprovalResultDto> RejectChangeAsync(Guid id, RejectPendingChangeRequest request, ClaimsPrincipal approver, CancellationToken ct = default);
    Task<bool> UpdateLayerApprovalRequirementAsync(string featureTypeCode, bool requiresApproval, ClaimsPrincipal admin, CancellationToken ct = default);
}
