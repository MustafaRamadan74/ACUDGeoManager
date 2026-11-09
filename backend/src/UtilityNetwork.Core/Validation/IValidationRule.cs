using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Validation;

public interface IValidationRule
{
    string RuleCode { get; }
    string DisplayName { get; }
    ValidationSeverity DefaultSeverity { get; }
    Task<IReadOnlyList<ValidationIssueDto>> EvaluateAsync(ValidationContext context, CancellationToken ct = default);
}
