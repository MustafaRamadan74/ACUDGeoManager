using Dapper;
using Microsoft.Extensions.Options;
using Npgsql;
using UtilityNetwork.Core.Configuration;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;

namespace UtilityNetwork.Data.Repositories;

public sealed class BookmarkRepository : IBookmarkRepository
{
    private readonly string _connectionString;

    public BookmarkRepository(IOptions<DatabaseOptions> dbOptions)
    {
        _connectionString = dbOptions.Value.DefaultConnection;
    }

    private sealed class BookmarkRow
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public double CenterLon { get; set; }
        public double CenterLat { get; set; }
        public int Zoom { get; set; }
        public string? BboxJson { get; set; }
        public DateTime CreatedAt { get; set; }

        public BookmarkDto ToDto() => new(
            Id,
            UserId,
            Name,
            CenterLon,
            CenterLat,
            Zoom,
            BboxJson,
            CreatedAt
        );
    }

    public async Task<IReadOnlyList<BookmarkDto>> GetUserBookmarksAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, user_id AS UserId, name, center_lon AS CenterLon, center_lat AS CenterLat,
                   zoom, bbox::text AS BboxJson, created_at AS CreatedAt
            FROM bookmarks
            WHERE user_id = @userId
            ORDER BY created_at DESC;";

        var rows = await conn.QueryAsync<BookmarkRow>(new CommandDefinition(sql, new { userId }, cancellationToken: ct));
        return rows.Select(r => r.ToDto()).ToList();
    }

    public async Task<BookmarkDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, user_id AS UserId, name, center_lon AS CenterLon, center_lat AS CenterLat,
                   zoom, bbox::text AS BboxJson, created_at AS CreatedAt
            FROM bookmarks
            WHERE id = @id AND user_id = @userId;";

        var row = await conn.QuerySingleOrDefaultAsync<BookmarkRow>(new CommandDefinition(sql, new { id, userId }, cancellationToken: ct));
        return row?.ToDto();
    }

    public async Task<BookmarkDto> CreateAsync(Guid userId, CreateBookmarkRequest request, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            INSERT INTO bookmarks (id, user_id, name, center_lon, center_lat, zoom, bbox, created_at)
            VALUES (gen_random_uuid(), @userId, @Name, @CenterLon, @CenterLat, @Zoom, @Bbox::jsonb, now())
            RETURNING id, user_id AS UserId, name, center_lon AS CenterLon, center_lat AS CenterLat,
                      zoom, bbox::text AS BboxJson, created_at AS CreatedAt;";

        var row = await conn.QuerySingleAsync<BookmarkRow>(new CommandDefinition(sql, new
        {
            userId,
            request.Name,
            request.CenterLon,
            request.CenterLat,
            request.Zoom,
            Bbox = string.IsNullOrWhiteSpace(request.Bbox) ? null : request.Bbox
        }, cancellationToken: ct));

        return row.ToDto();
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "DELETE FROM bookmarks WHERE id = @id AND user_id = @userId;";
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { id, userId }, cancellationToken: ct));
        return affected > 0;
    }
}

