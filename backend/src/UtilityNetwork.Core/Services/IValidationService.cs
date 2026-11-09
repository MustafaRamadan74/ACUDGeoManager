using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IValidationService
{
    Task<ValidationRunDto> RunValidationAsync(Guid networkId, string startedBy, CancellationToken ct = default);
    Task<IReadOnlyList<ValidationRunDto>> GetRunsAsync(Guid? networkId, int limit = 20, CancellationToken ct = default);
    Task<ValidationRunDto?> GetRunByIdAsync(Guid runId, CancellationToken ct = default);
    Task<ValidationIssuePageResult> QueryIssuesAsync(ValidationIssueQuery query, CancellationToken ct = default);
    Task<ValidationIssueDto> UpdateIssueStatusAsync(Guid issueId, IssueStatus status, string userName, CancellationToken ct = default);
}
