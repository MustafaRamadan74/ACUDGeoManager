using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new HealthCheckResponse(
            Status: report.Status.ToString(),
            TotalDurationMs: report.TotalDuration.TotalMilliseconds,
            Entries: report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new HealthEntryResponse(
                    Status: kvp.Value.Status.ToString(),
                    Description: kvp.Value.Description ?? string.Empty,
                    DurationMs: kvp.Value.Duration.TotalMilliseconds
                )
            )
        );

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}

public sealed record HealthCheckResponse(
    string Status,
    double TotalDurationMs,
    IReadOnlyDictionary<string, HealthEntryResponse> Entries);

public sealed record HealthEntryResponse(
    string Status,
    string Description,
    double DurationMs);

