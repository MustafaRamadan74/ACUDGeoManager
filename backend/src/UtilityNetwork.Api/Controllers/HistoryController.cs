using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public sealed class HistoryController : ControllerBase
{
    private readonly IHistoryService _historyService;

    public HistoryController(IHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntry>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] bool? onlyMe,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? featureId,
        [FromQuery] string? tableName,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdClaim, out var currentUserId))
        {
            return Unauthorized();
        }

        var isAdmin = User.IsInRole("ADMIN");

        var filter = new HistoryFilter(
            OnlyMe: onlyMe,
            UserId: userId,
            FeatureId: featureId,
            TableName: tableName,
            FromDate: fromDate,
            ToDate: toDate,
            Page: page,
            PageSize: pageSize);

        var result = await _historyService.GetHistoryAsync(filter, currentUserId, isAdmin, cancellationToken);
        return Ok(result);
    }
}
