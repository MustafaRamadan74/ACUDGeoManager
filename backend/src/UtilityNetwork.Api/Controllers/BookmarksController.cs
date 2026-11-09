using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Api.Controllers;

[ApiController]
[Route("api/v1/bookmarks")]
[Authorize]
public sealed class BookmarksController : ControllerBase
{
    private readonly IBookmarkService _bookmarkService;

    public BookmarksController(IBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    /// <summary>
    /// Retrieves all spatial bookmarks saved by the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookmarkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookmarksAsync(CancellationToken ct = default)
    {
        var bookmarks = await _bookmarkService.GetBookmarksAsync(User, ct);
        return Ok(bookmarks);
    }

    /// <summary>
    /// Saves a new spatial bookmark for the authenticated user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(BookmarkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBookmarkAsync(
        [FromBody] CreateBookmarkRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var created = await _bookmarkService.CreateBookmarkAsync(User, request, ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Deletes a saved spatial bookmark for the authenticated user.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBookmarkAsync(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var deleted = await _bookmarkService.DeleteBookmarkAsync(User, id, ct);
        if (!deleted)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = $"Bookmark with ID '{id}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }
}

