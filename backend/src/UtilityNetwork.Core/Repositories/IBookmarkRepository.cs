using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IBookmarkRepository
{
    Task<IReadOnlyList<BookmarkDto>> GetUserBookmarksAsync(Guid userId, CancellationToken ct = default);
    Task<BookmarkDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<BookmarkDto> CreateAsync(Guid userId, CreateBookmarkRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}

