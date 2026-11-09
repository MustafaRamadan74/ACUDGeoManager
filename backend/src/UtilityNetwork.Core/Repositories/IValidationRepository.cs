using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IValidationRepository
{
    Task<ValidationRunDto> CreateValidationRunAsync(Guid networkId, string startedBy, CancellationToken ct = default);
    Task CompleteValidationRunAsync(Guid runId, ValidationSummaryDto summary, CancellationToken ct = default);
    Task SaveValidationIssuesAsync(Guid runId, IEnumerable<ValidationIssueDto> issues, CancellationToken ct = default);
    Task<IReadOnlyList<ValidationRunDto>> GetValidationRunsAsync(Guid? networkId, int limit = 20, CancellationToken ct = default);
    Task<ValidationRunDto?> GetValidationRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<ValidationIssuePageResult> GetValidationIssuesAsync(ValidationIssueQuery query, CancellationToken ct = default);
    Task<ValidationIssueDto?> GetValidationIssueByIdAsync(Guid issueId, CancellationToken ct = default);
    Task<ValidationIssueDto?> UpdateIssueStatusAsync(Guid issueId, IssueStatus status, string userName, CancellationToken ct = default);
}
