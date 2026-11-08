using System.Security.Authentication;
using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Exceptions;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class FeatureService : IFeatureService
{
    private readonly IFeatureRepository _featureRepository;
    private readonly ICatalogRepository _catalogRepository;
    private readonly IFeatureValidationService _validationService;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IPermissionService _permissionService;

    public FeatureService(
        IFeatureRepository featureRepository,
        ICatalogRepository catalogRepository,
        IFeatureValidationService validationService,
        IUnitOfWorkFactory uowFactory,
        IPermissionService permissionService)
    {
        _featureRepository = featureRepository;
        _catalogRepository = catalogRepository;
        _validationService = validationService;
        _uowFactory = uowFactory;
        _permissionService = permissionService;
    }

    private static UserContext ExtractUserContext(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        var userId = Guid.TryParse(idClaim, out var id) ? id : (Guid?)null;
        var username = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("unique_name") ?? "system";
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "VIEWER";

        return new UserContext(userId, username, role, null);
    }

    private static void EnsureNetworkAccess(ClaimsPrincipal user, Guid networkId)
    {
        if (user.IsInRole("ADMIN")) return;

        var networkClaims = user.FindAll("network_id").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!networkClaims.Contains(networkId.ToString()))
        {
            throw new AuthenticationException($"Access denied to network '{networkId}'. The current user does not have assigned permissions.");
        }
    }

    // =========================================================================
    // POINTS
    // =========================================================================

    public Task<KeysetPagedResult<PointFeatureDto>> GetPointsAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (filter.NetworkId.HasValue)
        {
            EnsureNetworkAccess(user, filter.NetworkId.Value);
        }

        return _featureRepository.GetPointsAsync(filter, ct);
    }

    public async Task<PointFeatureDto?> GetPointByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var point = await _featureRepository.GetPointByIdAsync(id, ct);
        if (point != null)
        {
            EnsureNetworkAccess(user, point.NetworkId);
            await _permissionService.EnsureLayerPermissionAsync(user, point.NetworkId, point.FeatureTypeCode, LayerAction.Read, ct);
        }
        return point;
    }

    public async Task<PointFeatureDto> CreatePointAsync(CreatePointRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, req.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, req.NetworkId, req.FeatureTypeCode, LayerAction.Create, ct);

        var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(req.FeatureTypeCode, ct);
        if (ft is null)
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' does not exist.")]);
        }
        if (!string.Equals(ft.GeometryKind, "POINT", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' requires geometry kind '{ft.GeometryKind}', not POINT.")]);
        }

        _validationService.ValidatePointGeometry(req.X, req.Y);
        _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes, req.FeatureTypeCode);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Create point: {req.Name}", ct: ct);

        var id = await _featureRepository.CreatePointAsync(req, uow, ct);
        await uow.CommitAsync(ct);

        var created = await _featureRepository.GetPointByIdAsync(id, ct);
        return created!;
    }

    public async Task<PointFeatureDto> UpdatePointAsync(Guid id, UpdatePointRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetPointByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Point with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Update, ct);

        if (req.X.HasValue && req.Y.HasValue)
        {
            _validationService.ValidatePointGeometry(req.X.Value, req.Y.Value);
        }

        if (req.Attributes.HasValue)
        {
            var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(existing.FeatureTypeCode, ct);
            if (ft != null)
            {
                _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes.Value, existing.FeatureTypeCode);
            }
        }

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Update point: {id}", ct: ct);

        var updated = await _featureRepository.UpdatePointAsync(id, req, uow, ct);
        if (updated is null)
        {
            throw new KeyNotFoundException($"Point with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
        return updated;
    }

    public async Task DeletePointAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetPointByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Point with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Delete, ct);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Delete point: {id}", ct: ct);

        var deleted = await _featureRepository.DeletePointAsync(id, version, uow, ct);
        if (!deleted)
        {
            throw new KeyNotFoundException($"Point with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
    }

    // =========================================================================
    // LINES
    // =========================================================================

    public Task<KeysetPagedResult<LineFeatureDto>> GetLinesAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (filter.NetworkId.HasValue)
        {
            EnsureNetworkAccess(user, filter.NetworkId.Value);
        }

        return _featureRepository.GetLinesAsync(filter, ct);
    }

    public async Task<LineFeatureDto?> GetLineByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var line = await _featureRepository.GetLineByIdAsync(id, ct);
        if (line != null)
        {
            EnsureNetworkAccess(user, line.NetworkId);
            await _permissionService.EnsureLayerPermissionAsync(user, line.NetworkId, line.FeatureTypeCode, LayerAction.Read, ct);
        }
        return line;
    }

    public async Task<LineFeatureDto> CreateLineAsync(CreateLineRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, req.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, req.NetworkId, req.FeatureTypeCode, LayerAction.Create, ct);

        var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(req.FeatureTypeCode, ct);
        if (ft is null)
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' does not exist.")]);
        }
        if (!string.Equals(ft.GeometryKind, "LINE", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' requires geometry kind '{ft.GeometryKind}', not LINE.")]);
        }

        _validationService.ValidateLineGeometry(req.Coordinates);
        _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes, req.FeatureTypeCode);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Create line: {req.Name}", ct: ct);

        var id = await _featureRepository.CreateLineAsync(req, uow, ct);
        await uow.CommitAsync(ct);

        var created = await _featureRepository.GetLineByIdAsync(id, ct);
        return created!;
    }

    public async Task<LineFeatureDto> UpdateLineAsync(Guid id, UpdateLineRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetLineByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Line with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Update, ct);

        if (req.Coordinates != null)
        {
            _validationService.ValidateLineGeometry(req.Coordinates);
        }

        if (req.Attributes.HasValue)
        {
            var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(existing.FeatureTypeCode, ct);
            if (ft != null)
            {
                _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes.Value, existing.FeatureTypeCode);
            }
        }

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Update line: {id}", ct: ct);

        var updated = await _featureRepository.UpdateLineAsync(id, req, uow, ct);
        if (updated is null)
        {
            throw new KeyNotFoundException($"Line with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
        return updated;
    }

    public async Task DeleteLineAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetLineByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Line with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Delete, ct);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Delete line: {id}", ct: ct);

        var deleted = await _featureRepository.DeleteLineAsync(id, version, uow, ct);
        if (!deleted)
        {
            throw new KeyNotFoundException($"Line with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
    }

    // =========================================================================
    // POLYGONS
    // =========================================================================

    public Task<KeysetPagedResult<PolygonFeatureDto>> GetPolygonsAsync(FeatureQueryFilter filter, ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (filter.NetworkId.HasValue)
        {
            EnsureNetworkAccess(user, filter.NetworkId.Value);
        }

        return _featureRepository.GetPolygonsAsync(filter, ct);
    }

    public async Task<PolygonFeatureDto?> GetPolygonByIdAsync(Guid id, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var poly = await _featureRepository.GetPolygonByIdAsync(id, ct);
        if (poly != null)
        {
            EnsureNetworkAccess(user, poly.NetworkId);
            await _permissionService.EnsureLayerPermissionAsync(user, poly.NetworkId, poly.FeatureTypeCode, LayerAction.Read, ct);
        }
        return poly;
    }

    public async Task<PolygonFeatureDto> CreatePolygonAsync(CreatePolygonRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        EnsureNetworkAccess(user, req.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, req.NetworkId, req.FeatureTypeCode, LayerAction.Create, ct);

        var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(req.FeatureTypeCode, ct);
        if (ft is null)
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' does not exist.")]);
        }
        if (!string.Equals(ft.GeometryKind, "POLYGON", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException([new ValidationFailure("featureTypeCode", $"Feature type '{req.FeatureTypeCode}' requires geometry kind '{ft.GeometryKind}', not POLYGON.")]);
        }

        _validationService.ValidatePolygonGeometry(req.RingCoordinates);
        _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes, req.FeatureTypeCode);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Create polygon: {req.Name}", ct: ct);

        var id = await _featureRepository.CreatePolygonAsync(req, uow, ct);
        await uow.CommitAsync(ct);

        var created = await _featureRepository.GetPolygonByIdAsync(id, ct);
        return created!;
    }

    public async Task<PolygonFeatureDto> UpdatePolygonAsync(Guid id, UpdatePolygonRequest req, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetPolygonByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Polygon with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Update, ct);

        if (req.RingCoordinates != null)
        {
            _validationService.ValidatePolygonGeometry(req.RingCoordinates);
        }

        if (req.Attributes.HasValue)
        {
            var ft = await _catalogRepository.GetFeatureTypeByCodeAsync(existing.FeatureTypeCode, ct);
            if (ft != null)
            {
                _validationService.ValidateAttributes(ft.AttributeSchema.GetRawText(), req.Attributes.Value, existing.FeatureTypeCode);
            }
        }

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Update polygon: {id}", ct: ct);

        var updated = await _featureRepository.UpdatePolygonAsync(id, req, uow, ct);
        if (updated is null)
        {
            throw new KeyNotFoundException($"Polygon with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
        return updated;
    }

    public async Task DeletePolygonAsync(Guid id, int version, ClaimsPrincipal user, CancellationToken ct = default)
    {
        var existing = await _featureRepository.GetPolygonByIdAsync(id, ct);
        if (existing is null)
        {
            throw new KeyNotFoundException($"Polygon with ID '{id}' was not found.");
        }
        EnsureNetworkAccess(user, existing.NetworkId);
        await _permissionService.EnsureLayerPermissionAsync(user, existing.NetworkId, existing.FeatureTypeCode, LayerAction.Delete, ct);

        var userContext = ExtractUserContext(user);
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: $"Delete polygon: {id}", ct: ct);

        var deleted = await _featureRepository.DeletePolygonAsync(id, version, uow, ct);
        if (!deleted)
        {
            throw new KeyNotFoundException($"Polygon with ID '{id}' was not found.");
        }

        await uow.CommitAsync(ct);
    }

    // =========================================================================
    // UNIFIED MAP
    // =========================================================================

    public async Task<GeoJsonFeatureCollection> GetMapFeaturesAsync(
        double minX,
        double minY,
        double maxX,
        double maxY,
        IReadOnlyList<Guid>? networkIds,
        IReadOnlyList<string>? typeCodes,
        ClaimsPrincipal user,
        int limit = 1000,
        CancellationToken ct = default)
    {
        if (networkIds != null && networkIds.Count > 0)
        {
            foreach (var nid in networkIds)
            {
                EnsureNetworkAccess(user, nid);
            }
        }

        // Filter out unreadable layers if user is not ADMIN
        if (!user.IsInRole("ADMIN"))
        {
            var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (Guid.TryParse(idClaim, out var uid))
            {
                var perms = await _permissionService.GetUserPermissionsAsync(uid, ct);
                var unreadable = perms.Where(p => !p.CanRead).Select(p => p.FeatureTypeCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (unreadable.Count > 0)
                {
                    if (typeCodes != null && typeCodes.Count > 0)
                    {
                        typeCodes = typeCodes.Where(tc => !unreadable.Contains(tc)).ToList();
                    }
                    else
                    {
                        var allTypes = await _catalogRepository.GetFeatureTypesAsync(ct);
                        typeCodes = allTypes.Select(t => t.Code).Where(tc => !unreadable.Contains(tc)).ToList();
                    }
                }
            }
        }

        return await _featureRepository.GetMapFeaturesAsync(minX, minY, maxX, maxY, networkIds, typeCodes, limit, ct);
    }
}
