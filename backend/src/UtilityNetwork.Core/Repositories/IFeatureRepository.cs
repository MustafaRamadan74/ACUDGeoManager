using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface IFeatureRepository
{
    // Points
    Task<KeysetPagedResult<PointFeatureDto>> GetPointsAsync(FeatureQueryFilter filter, CancellationToken ct = default);
    Task<PointFeatureDto?> GetPointByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreatePointAsync(CreatePointRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<PointFeatureDto?> UpdatePointAsync(Guid id, UpdatePointRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<bool> DeletePointAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default);

    // Lines
    Task<KeysetPagedResult<LineFeatureDto>> GetLinesAsync(FeatureQueryFilter filter, CancellationToken ct = default);
    Task<LineFeatureDto?> GetLineByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateLineAsync(CreateLineRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<LineFeatureDto?> UpdateLineAsync(Guid id, UpdateLineRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<bool> DeleteLineAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default);

    // Polygons
    Task<KeysetPagedResult<PolygonFeatureDto>> GetPolygonsAsync(FeatureQueryFilter filter, CancellationToken ct = default);
    Task<PolygonFeatureDto?> GetPolygonByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreatePolygonAsync(CreatePolygonRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<PolygonFeatureDto?> UpdatePolygonAsync(Guid id, UpdatePolygonRequest req, IUnitOfWork uow, CancellationToken ct = default);
    Task<bool> DeletePolygonAsync(Guid id, int version, IUnitOfWork uow, CancellationToken ct = default);

    // Map FeatureCollection
    Task<GeoJsonFeatureCollection> GetMapFeaturesAsync(
        double minX,
        double minY,
        double maxX,
        double maxY,
        IReadOnlyList<Guid>? networkIds,
        IReadOnlyList<string>? typeCodes,
        int limit = 1000,
        CancellationToken ct = default);
}
