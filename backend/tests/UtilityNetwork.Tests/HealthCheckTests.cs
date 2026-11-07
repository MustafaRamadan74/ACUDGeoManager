using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UtilityNetwork.Api.Controllers;
using UtilityNetwork.Core.Configuration;

namespace UtilityNetwork.Tests;

public sealed class HealthCheckTests
{
    [Fact]
    public void DatabaseOptions_ShouldHaveCorrectSectionName()
    {
        Assert.Equal("ConnectionStrings", DatabaseOptions.SectionName);
    }

    [Fact]
    public async Task HealthController_WhenHealthy_ReturnsOkObjectResult()
    {
        // Arrange
        var healthService = new MockHealthCheckService(HealthStatus.Healthy);
        var controller = new HealthController(healthService);

        // Act
        var result = await controller.GetHealthAsync(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HealthCheckResponse>(okResult.Value);
        Assert.Equal("Healthy", response.Status);
    }

    [Fact]
    public async Task HealthController_WhenUnhealthy_ReturnsServiceUnavailable()
    {
        // Arrange
        var healthService = new MockHealthCheckService(HealthStatus.Unhealthy);
        var controller = new HealthController(healthService);

        // Act
        var result = await controller.GetHealthAsync(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }
}

internal sealed class MockHealthCheckService : HealthCheckService
{
    private readonly HealthStatus _status;

    public MockHealthCheckService(HealthStatus status)
    {
        _status = status;
    }

    public override Task<HealthReport> CheckHealthAsync(
        Func<HealthCheckRegistration, bool>? predicate,
        CancellationToken cancellationToken = default)
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["test-db"] = new(
                _status,
                description: "Test check",
                duration: TimeSpan.FromMilliseconds(5),
                exception: null,
                data: null)
        };

        var report = new HealthReport(entries, _status, TimeSpan.FromMilliseconds(10));
        return Task.FromResult(report);
    }
}

