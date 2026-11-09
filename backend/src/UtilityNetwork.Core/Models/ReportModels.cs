namespace UtilityNetwork.Core.Models;

/// <summary>
/// Supported export formats for analysis and system reports.
/// </summary>
public enum ReportFormat
{
    Pdf,
    Csv
}

/// <summary>
/// Request parameters for generating a Workspace Summary Report.
/// </summary>
public sealed class WorkspaceSummaryReportRequest
{
    public Guid NetworkId { get; set; }
    public string? MapSnapshotBase64 { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
}

/// <summary>
/// Request parameters for generating a Topology / Validation Report.
/// </summary>
public sealed class ValidationReportRequest
{
    public Guid? NetworkId { get; set; }
    public string? Severity { get; set; }
    public string? MapSnapshotBase64 { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
}

/// <summary>
/// Request parameters for generating a Feature Audit History Report.
/// </summary>
public sealed class FeatureHistoryReportRequest
{
    public Guid FeatureId { get; set; }
    public string? MapSnapshotBase64 { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
}

/// <summary>
/// Request parameters for generating a User Activity Report.
/// </summary>
public sealed class UserActivityReportRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
}

/// <summary>
/// Request parameters for exporting generic Analysis Widget results (Buffer, Measure, Spatial Join, Stats).
/// </summary>
public sealed class AnalysisWidgetExportRequest
{
    public string Title { get; set; } = "Analysis Report";
    public string AnalysisType { get; set; } = "BUFFER";
    public string? Description { get; set; }
    public string? SummaryJson { get; set; }
    public IReadOnlyList<string> Headers { get; set; } = new List<string>();
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = new List<IReadOnlyList<string>>();
    public string? MapSnapshotBase64 { get; set; }
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;
}

/// <summary>
/// Generated file result containing raw bytes, MIME type, and recommended filename.
/// </summary>
public sealed record GeneratedReportResult(
    byte[] Content,
    string ContentType,
    string FileName
);
