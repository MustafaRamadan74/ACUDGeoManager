using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Generates a Workspace Summary Report in PDF or CSV format.
    /// </summary>
    [HttpPost("workspace-summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateWorkspaceSummaryReportAsync([FromBody] WorkspaceSummaryReportRequest request, CancellationToken ct = default)
    {
        var result = await _reportService.GenerateWorkspaceSummaryReportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Generates a Topology & Spatial Validation Report in PDF or CSV format.
    /// </summary>
    [HttpPost("validation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateValidationReportAsync([FromBody] ValidationReportRequest request, CancellationToken ct = default)
    {
        var result = await _reportService.GenerateValidationReportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Generates an Audit History Report for a specific feature in PDF or CSV format.
    /// </summary>
    [HttpPost("feature-history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateFeatureHistoryReportAsync([FromBody] FeatureHistoryReportRequest request, CancellationToken ct = default)
    {
        var result = await _reportService.GenerateFeatureHistoryReportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Generates a User Activity and productivity report in PDF or CSV format.
    /// </summary>
    [HttpPost("user-activity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateUserActivityReportAsync([FromBody] UserActivityReportRequest request, CancellationToken ct = default)
    {
        var result = await _reportService.GenerateUserActivityReportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Exports results from any Analysis Widget (Buffer, Measure, Spatial Join, Stats) to PDF or CSV.
    /// </summary>
    [HttpPost("analysis-widget")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAnalysisWidgetReportAsync([FromBody] AnalysisWidgetExportRequest request, CancellationToken ct = default)
    {
        var result = await _reportService.GenerateAnalysisWidgetReportAsync(request, ct);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
