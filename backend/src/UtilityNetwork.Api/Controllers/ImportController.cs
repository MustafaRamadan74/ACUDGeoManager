using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/import")]
[Authorize]
public sealed class ImportController : ControllerBase
{
    private readonly IImportService _importService;

    public ImportController(IImportService importService)
    {
        _importService = importService;
    }

    /// <summary>
    /// Step 1: Previews and validates an import file (GeoJSON or CSV) without persisting changes.
    /// Supports dry run validation, OGC compliance check, duplicate detection, and SRID transformation.
    /// </summary>
    [HttpPost("preview")]
    [Consumes("multipart/form-data", "application/json")]
    [ProducesResponseType(typeof(ImportPreviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewAsync(
        [FromForm] IFormFile? file,
        [FromForm] Guid? formNetworkId,
        [FromForm] string? formFeatureTypeCode,
        [FromForm] int? formSrid,
        [FromBody] ImportPreviewRequest? jsonRequest,
        CancellationToken ct = default)
    {
        string? content = null;
        Guid networkId;
        string? featureTypeCode = null;
        int srid = 4326;

        if (file != null && file.Length > 0)
        {
            if (file.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "File Too Large",
                    Detail = "Uploaded file exceeds the maximum 10 MB limit.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            content = await reader.ReadToEndAsync(ct);

            if (!formNetworkId.HasValue || formNetworkId.Value == Guid.Empty)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Validation Error",
                    Detail = "NetworkId is required.",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            networkId = formNetworkId.Value;
            featureTypeCode = formFeatureTypeCode;
            srid = formSrid ?? 4326;
        }
        else if (jsonRequest != null)
        {
            content = jsonRequest.Content;
            networkId = jsonRequest.NetworkId;
            featureTypeCode = jsonRequest.FeatureTypeCode;
            srid = jsonRequest.Srid;
        }
        else
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "A file or JSON content payload must be provided.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var req = new ImportPreviewRequest(networkId, featureTypeCode, srid, content);
            var result = await _importService.PreviewImportAsync(req, User, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import Validation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import Operation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Step 2: Commits a validated preview batch into the database inside a single change set.
    /// Allows atomic single-step undo of the whole import batch.
    /// </summary>
    [HttpPost("commit")]
    [Authorize(Roles = "ADMIN,EDITOR")]
    [ProducesResponseType(typeof(ImportCommitResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CommitAsync(
        [FromBody] ImportCommitRequest request,
        CancellationToken ct = default)
    {
        if (request.PreviewToken == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = "PreviewToken cannot be empty.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var result = await _importService.CommitImportAsync(request, User, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Import Commit Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
}

