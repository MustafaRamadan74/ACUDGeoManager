using System.Security.Claims;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Services;

public interface IFeatureService
{
    // Points
    Task<KeysetPagedResult<PointFeatureDto>> GetPointsAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PointFeatureDto?> GetPointByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PointFeatureDto> CreatePointAsync(CreatePointRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PointFeatureDto> UpdatePointAsync(Guid id, UpdatePointRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task DeletePointAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default);

    // Lines
    Task<KeysetPagedResult<LineFeatureDto>> GetLinesAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default);
    Task<LineFeatureDto?> GetLineByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);
    Task<LineFeatureDto> CreateLineAsync(CreateLineRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task<LineFeatureDto> UpdateLineAsync(Guid id, UpdateLineRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task DeleteLineAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default);

    // Polygons
    Task<KeysetPagedResult<PolygonFeatureDto>> GetPolygonsAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PolygonFeatureDto?> GetPolygonByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PolygonFeatureDto> CreatePolygonAsync(CreatePolygonRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task<PolygonFeatureDto> UpdatePolygonAsync(Guid id, UpdatePolygonRequest req, ClaimsPrincipal user, CancellationToken ct = default);
    Task DeletePolygonAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default);

    // Unified Map
    Task<GeoJsonFeatureCollection> GetMapFeaturesAsync(
        double minX,
        double minY,
        double maxX,
        double maxY,
        IReadOnlyList<Guid>? networkIds,
        IReadOnlyList<string>? typeCodes,
        ClaimsPrincipal user,
        int limit = 1000,
        CancellationToken ct = default);
}
