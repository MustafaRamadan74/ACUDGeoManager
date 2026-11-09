using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IBookmarkService
{
    Task<IReadOnlyList<BookmarkDto>> GetBookmarksAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<BookmarkDto> CreateBookmarkAsync(ClaimsPrincipal user, CreateBookmarkRequest request, CancellationToken ct = default);
    Task<bool> DeleteBookmarkAsync(ClaimsPrincipal user, Guid id, CancellationToken ct = default);
}
