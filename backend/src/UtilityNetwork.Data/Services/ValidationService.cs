using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Core.Validation;

#pragma warning disable CA1848, CA1873

namespace UtilityNetwork.Data.Services;

public sealed class ValidationService : IValidationService
{
    private readonly IValidationRepository _validationRepository;
    private readonly IEnumerable<IValidationRule> _rules;
    private readonly string _connectionString;
    private readonly ILogger<ValidationService> _logger;
    private readonly IWorkspaceNotificationService? _notificationService;

    public ValidationService(
        IValidationRepository validationRepository,
        IEnumerable<IValidationRule> rules,
        IOptions<DatabaseOptions> dbOptions,
        ILogger<ValidationService> logger,
        IWorkspaceNotificationService? notificationService = null)
    {
        _validationRepository = validationRepository;
        _rules = rules;
        _connectionString = dbOptions.Value.DefaultConnection;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<ValidationRunDto> RunValidationAsync(Guid networkId, string startedBy, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting validation run for network {NetworkId} by {User}", networkId, startedBy);

        var run = await _validationRepository.CreateValidationRunAsync(networkId, startedBy, ct);
        var context = new ValidationContext(networkId, startedBy, _connectionString);

        var allIssues = new List<ValidationIssueDto>();
        var executedRules = new List<string>();

        foreach (var rule in _rules)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ruleIssues = await rule.EvaluateAsync(context, ct);
                executedRules.Add(rule.RuleCode);

                foreach (var issue in ruleIssues)
                {
                    allIssues.Add(issue with { RunId = run.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rule {RuleCode} failed during evaluation", rule.RuleCode);
            }
        }

        await _validationRepository.SaveValidationIssuesAsync(run.Id, allIssues, ct);

        sw.Stop();
        var summary = new ValidationSummaryDto(
            TotalIssues: allIssues.Count,
            ErrorsCount: allIssues.Count(i => i.Severity == ValidationSeverity.ERROR),
            WarningsCount: allIssues.Count(i => i.Severity == ValidationSeverity.WARNING),
            InfosCount: allIssues.Count(i => i.Severity == ValidationSeverity.INFO),
            DurationMs: sw.ElapsedMilliseconds,
            RulesExecuted: executedRules
        );

        await _validationRepository.CompleteValidationRunAsync(run.Id, summary, ct);

        if (_notificationService != null)
        {
            _ = _notificationService.NotifyValidationCompletedAsync(new ValidationCompletedEvent(
                run.Id,
                networkId,
                "COMPLETED",
                allIssues.Count,
                DateTimeOffset.UtcNow
            ), ct);
        }

        _logger.LogInformation("Validation run {RunId} finished in {Duration}ms with {Count} issues",
            run.Id, sw.ElapsedMilliseconds, allIssues.Count);

        return run with { FinishedAt = DateTime.UtcNow, Summary = summary };
    }

    public Task<IReadOnlyList<ValidationRunDto>> GetRunsAsync(Guid? networkId, int limit = 20, CancellationToken ct = default)
    {
        return _validationRepository.GetValidationRunsAsync(networkId, limit, ct);
    }

    public Task<ValidationRunDto?> GetRunByIdAsync(Guid runId, CancellationToken ct = default)
    {
        return _validationRepository.GetValidationRunByIdAsync(runId, ct);
    }

    public Task<ValidationIssuePageResult> QueryIssuesAsync(ValidationIssueQuery query, CancellationToken ct = default)
    {
        return _validationRepository.GetValidationIssuesAsync(query, ct);
    }

    public async Task<ValidationIssueDto> UpdateIssueStatusAsync(Guid issueId, IssueStatus status, string userName, CancellationToken ct = default)
    {
        var updated = await _validationRepository.UpdateIssueStatusAsync(issueId, status, userName, ct);
        if (updated == null)
        {
            throw new KeyNotFoundException($"Validation issue '{issueId}' not found");
        }

        return updated;
    }
}
