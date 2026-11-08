using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Repositories;

public interface ISpatialQueryRepository
{
    Task<GeoJsonFeatureCollection> QueryWithinAsync(QueryWithinRequest request, CancellationToken ct = default);
    Task<GeoJsonFeatureCollection> QueryNearestAsync(NearestQueryFilter filter, CancellationToken ct = default);
}

