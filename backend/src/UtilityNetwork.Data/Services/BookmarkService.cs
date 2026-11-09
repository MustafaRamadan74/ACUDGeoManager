using System.Security.Claims;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class BookmarkService : IBookmarkService
{
    private readonly IBookmarkRepository _repository;

    public BookmarkService(IBookmarkRepository repository)
    {
        _repository = repository;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var idStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(idStr, out var id))
        {
            return id;
        }
        throw new UnauthorizedAccessException("User identification claim is missing or invalid.");
    }

    public async Task<IReadOnlyList<BookmarkDto>> GetBookmarksAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        return await _repository.GetUserBookmarksAsync(userId, ct);
    }

    public async Task<BookmarkDto> CreateBookmarkAsync(ClaimsPrincipal user, CreateBookmarkRequest request, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Bookmark name cannot be empty.");
        }

        return await _repository.CreateAsync(userId, request, ct);
    }

    public async Task<bool> DeleteBookmarkAsync(ClaimsPrincipal user, Guid id, CancellationToken ct = default)
    {
        var userId = GetUserId(user);
        return await _repository.DeleteAsync(id, userId, ct);
    }
}
