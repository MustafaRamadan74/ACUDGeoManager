using System.Collections.Concurrent;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class MemoryImportPreviewStore : IImportPreviewStore
{
    private sealed record StoreEntry(
        IReadOnlyList<ParsedImportFeature> Features,
        int SourceSrid,
        DateTime ExpiresAtUtc
    );

    private readonly ConcurrentDictionary<Guid, StoreEntry> _cache = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    public void Store(Guid previewToken, IReadOnlyList<ParsedImportFeature> features, int sourceSrid)
    {
        CleanupExpired();
        _cache[previewToken] = new StoreEntry(features, sourceSrid, DateTime.UtcNow.Add(DefaultTtl));
    }

    public (IReadOnlyList<ParsedImportFeature> Features, int SourceSrid)? Retrieve(Guid previewToken)
    {
        CleanupExpired();
        if (_cache.TryGetValue(previewToken, out var entry))
        {
            if (entry.ExpiresAtUtc >= DateTime.UtcNow)
            {
                return (entry.Features, entry.SourceSrid);
            }
            _cache.TryRemove(previewToken, out _);
        }
        return null;
    }

    public void Remove(Guid previewToken)
    {
        _cache.TryRemove(previewToken, out _);
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _cache)
        {
            if (pair.Value.ExpiresAtUtc < now)
            {
                _cache.TryRemove(pair.Key, out _);
            }
        }
    }
}
