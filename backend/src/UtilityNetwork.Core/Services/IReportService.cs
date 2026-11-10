using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

/// <summary>
/// Service generating professional PDF and CSV reports for workspaces, validations, audit histories, and analyses.
/// </summary>
public interface IReportService
{
    Task<GeneratedReportResult> GenerateWorkspaceSummaryReportAsync(WorkspaceSummaryReportRequest request, CancellationToken ct = default);
    Task<GeneratedReportResult> GenerateValidationReportAsync(ValidationReportRequest request, CancellationToken ct = default);
    Task<GeneratedReportResult> GenerateFeatureHistoryReportAsync(FeatureHistoryReportRequest request, CancellationToken ct = default);
    Task<GeneratedReportResult> GenerateUserActivityReportAsync(UserActivityReportRequest request, CancellationToken ct = default);
    Task<GeneratedReportResult> GenerateAnalysisWidgetReportAsync(AnalysisWidgetExportRequest request, CancellationToken ct = default);
    Task<GeneratedReportResult> GenerateTraceReportAsync(TraceReportRequest request, CancellationToken ct = default);
}
