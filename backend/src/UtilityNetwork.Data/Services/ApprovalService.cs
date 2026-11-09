using System.Security.Claims;
using System.Text.Json;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Entities;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Data.Services;

public sealed class ApprovalService : IApprovalService
{
    private readonly IPendingChangeRepository _pendingRepo;
    private readonly ICatalogRepository _catalogRepo;
    private readonly IFeatureRepository _featureRepo;
    private readonly IUnitOfWorkFactory _uowFactory;

    public ApprovalService(
        IPendingChangeRepository pendingRepo,
        ICatalogRepository catalogRepo,
        IFeatureRepository featureRepo,
        IUnitOfWorkFactory uowFactory)
    {
        _pendingRepo = pendingRepo;
        _catalogRepo = catalogRepo;
        _featureRepo = featureRepo;
        _uowFactory = uowFactory;
    }

    public async Task<IReadOnlyList<PendingChangeDto>> GetPendingQueueAsync(Guid? networkId, ClaimsPrincipal approver, CancellationToken ct = default)
    {
        EnsureApproverAccess(approver);
        return await _pendingRepo.GetPendingQueueAsync(networkId, ct);
    }

    public async Task<PendingChangeDto?> GetPendingChangeByIdAsync(Guid id, ClaimsPrincipal approver, CancellationToken ct = default)
    {
        EnsureApproverAccess(approver);
        return await _pendingRepo.GetByIdAsync(id, ct);
    }

    public async Task<ApprovalResultDto> ApproveChangeAsync(Guid id, ClaimsPrincipal approver, CancellationToken ct = default)
    {
        EnsureApproverAccess(approver);
        var approverId = GetUserId(approver);
        var approverUsername = approver.Identity?.Name ?? "approver";
        var approverRole = approver.FindFirstValue(ClaimTypes.Role) ?? "APPROVER";

        var pc = await _pendingRepo.GetByIdAsync(id, ct);
        if (pc == null)
        {
            throw new KeyNotFoundException($"Pending change with ID '{id}' was not found.");
        }

        if (pc.ReviewStatus != "PENDING")
        {
            throw new InvalidOperationException($"Pending change '{id}' has already been reviewed ({pc.ReviewStatus}).");
        }

        // 1. Optimistic Concurrency Check for UPDATE and DELETE
        if (pc.Operation is "UPDATE" or "DELETE")
        {
            if (!pc.FeatureId.HasValue || !pc.TargetVersion.HasValue)
            {
                throw new InvalidOperationException("Pending modification is missing target Feature ID or Version.");
            }

            int currentVersion = -1;
            if (pc.GeometryKind == "POINT")
            {
                var current = await _featureRepo.GetPointByIdAsync(pc.FeatureId.Value, ct);
                if (current == null) throw new KeyNotFoundException($"Target point '{pc.FeatureId.Value}' no longer exists.");
                currentVersion = current.Version;
            }
            else if (pc.GeometryKind == "LINE")
            {
                var current = await _featureRepo.GetLineByIdAsync(pc.FeatureId.Value, ct);
                if (current == null) throw new KeyNotFoundException($"Target line '{pc.FeatureId.Value}' no longer exists.");
                currentVersion = current.Version;
            }
            else if (pc.GeometryKind == "POLYGON")
            {
                var current = await _featureRepo.GetPolygonByIdAsync(pc.FeatureId.Value, ct);
                if (current == null) throw new KeyNotFoundException($"Target polygon '{pc.FeatureId.Value}' no longer exists.");
                currentVersion = current.Version;
            }

            if (currentVersion != pc.TargetVersion.Value)
            {
                throw new ConcurrencyConflictException(
                    $"Target feature version changed (Current: {currentVersion}, Proposed: {pc.TargetVersion.Value}). " +
                    "The proposal was rejected due to a concurrent modification. The proposer must review and resubmit.");
            }
        }

        // 2. Apply change through Unit of Work
        var userContext = new UserContext(
            UserId: approverId,
            Username: approverUsername,
            Role: approverRole,
            RequestId: null
        );

        var description = $"Approved {pc.Operation} on {pc.FeatureTypeCode} proposed by {pc.ProposerUsername}";
        await using var uow = await _uowFactory.BeginWriteAsync(userContext, description: description, ct: ct);

        Guid appliedFeatureId = pc.FeatureId ?? Guid.Empty;

        if (pc.Operation == "INSERT")
        {
            if (pc.GeometryKind == "POINT")
            {
                var coords = ExtractPointCoordinates(pc.GeometryGeoJson);
                var req = new CreatePointRequest(
                    NetworkId: pc.NetworkId,
                    FeatureTypeCode: pc.FeatureTypeCode,
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseJsonAttributes(pc.AttributesJson),
                    X: coords.x,
                    Y: coords.y,
                    IsOpen: pc.IsOpen
                );
                appliedFeatureId = await _featureRepo.CreatePointAsync(req, uow, ct);
            }
            else if (pc.GeometryKind == "LINE")
            {
                var coords = ExtractLineCoordinates(pc.GeometryGeoJson);
                var req = new CreateLineRequest(
                    NetworkId: pc.NetworkId,
                    FeatureTypeCode: pc.FeatureTypeCode,
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseJsonAttributes(pc.AttributesJson),
                    Coordinates: coords,
                    FlowDirection: pc.FlowDirection ?? "WITH_DIGITIZED",
                    DiameterMm: pc.DiameterMm,
                    Material: pc.Material
                );
                appliedFeatureId = await _featureRepo.CreateLineAsync(req, uow, ct);
            }
            else if (pc.GeometryKind == "POLYGON")
            {
                var rings = ExtractPolygonCoordinates(pc.GeometryGeoJson);
                var req = new CreatePolygonRequest(
                    NetworkId: pc.NetworkId,
                    FeatureTypeCode: pc.FeatureTypeCode,
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseJsonAttributes(pc.AttributesJson),
                    RingCoordinates: rings
                );
                appliedFeatureId = await _featureRepo.CreatePolygonAsync(req, uow, ct);
            }
        }
        else if (pc.Operation == "UPDATE")
        {
            if (pc.GeometryKind == "POINT")
            {
                (double x, double y)? coords = !string.IsNullOrWhiteSpace(pc.GeometryGeoJson) ? ExtractPointCoordinates(pc.GeometryGeoJson) : null;
                var req = new UpdatePointRequest(
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseOptionalJsonAttributes(pc.AttributesJson),
                    X: coords?.x,
                    Y: coords?.y,
                    IsOpen: pc.IsOpen,
                    Version: pc.TargetVersion!.Value
                );
                var updated = await _featureRepo.UpdatePointAsync(pc.FeatureId!.Value, req, uow, ct);
                appliedFeatureId = updated?.Id ?? pc.FeatureId.Value;
            }
            else if (pc.GeometryKind == "LINE")
            {
                IReadOnlyList<double[]>? coords = !string.IsNullOrWhiteSpace(pc.GeometryGeoJson) ? ExtractLineCoordinates(pc.GeometryGeoJson) : null;
                var req = new UpdateLineRequest(
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseOptionalJsonAttributes(pc.AttributesJson),
                    Coordinates: coords,
                    FlowDirection: pc.FlowDirection,
                    DiameterMm: pc.DiameterMm,
                    Material: pc.Material,
                    Version: pc.TargetVersion!.Value
                );
                var updated = await _featureRepo.UpdateLineAsync(pc.FeatureId!.Value, req, uow, ct);
                appliedFeatureId = updated?.Id ?? pc.FeatureId.Value;
            }
            else if (pc.GeometryKind == "POLYGON")
            {
                IReadOnlyList<IReadOnlyList<double[]>>? rings = !string.IsNullOrWhiteSpace(pc.GeometryGeoJson) ? ExtractPolygonCoordinates(pc.GeometryGeoJson) : null;
                var req = new UpdatePolygonRequest(
                    Name: pc.Name,
                    Status: pc.Status,
                    Attributes: ParseOptionalJsonAttributes(pc.AttributesJson),
                    RingCoordinates: rings,
                    Version: pc.TargetVersion!.Value
                );
                var updated = await _featureRepo.UpdatePolygonAsync(pc.FeatureId!.Value, req, uow, ct);
                appliedFeatureId = updated?.Id ?? pc.FeatureId.Value;
            }
        }
        else if (pc.Operation == "DELETE")
        {
            if (pc.GeometryKind == "POINT")
            {
                await _featureRepo.DeletePointAsync(pc.FeatureId!.Value, pc.TargetVersion!.Value, uow, ct);
            }
            else if (pc.GeometryKind == "LINE")
            {
                await _featureRepo.DeleteLineAsync(pc.FeatureId!.Value, pc.TargetVersion!.Value, uow, ct);
            }
            else if (pc.GeometryKind == "POLYGON")
            {
                await _featureRepo.DeletePolygonAsync(pc.FeatureId!.Value, pc.TargetVersion!.Value, uow, ct);
            }
        }

        await _pendingRepo.MarkApprovedAsync(id, approverId, ct);
        await uow.CommitAsync(ct);

        return new ApprovalResultDto(
            id,
            "APPROVED",
            appliedFeatureId,
            $"Proposed {pc.Operation} on {pc.FeatureTypeCode} was approved by {approverUsername} and committed."
        );
    }

    public async Task<ApprovalResultDto> RejectChangeAsync(Guid id, RejectPendingChangeRequest request, ClaimsPrincipal approver, CancellationToken ct = default)
    {
        EnsureApproverAccess(approver);
        var approverId = GetUserId(approver);
        var approverUsername = approver.Identity?.Name ?? "approver";

        var pc = await _pendingRepo.GetByIdAsync(id, ct);
        if (pc == null)
        {
            throw new KeyNotFoundException($"Pending change with ID '{id}' was not found.");
        }

        if (pc.ReviewStatus != "PENDING")
        {
            throw new InvalidOperationException($"Pending change '{id}' has already been reviewed ({pc.ReviewStatus}).");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Rejected by approver." : request.Reason.Trim();
        await _pendingRepo.MarkRejectedAsync(id, approverId, reason, ct);

        return new ApprovalResultDto(
            id,
            "REJECTED",
            pc.FeatureId,
            $"Proposed {pc.Operation} on {pc.FeatureTypeCode} was rejected by {approverUsername}. Reason: {reason}"
        );
    }

    public async Task<bool> UpdateLayerApprovalRequirementAsync(string featureTypeCode, bool requiresApproval, ClaimsPrincipal admin, CancellationToken ct = default)
    {
        var role = admin.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only administrators can update layer approval requirements.");
        }

        return await _catalogRepo.UpdateFeatureTypeRequiresApprovalAsync(featureTypeCode, requiresApproval, ct);
    }

    #region Helpers

    private static void EnsureApproverAccess(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "APPROVER", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("User is not authorized to review pending approvals. Role must be APPROVER or ADMIN.");
        }
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static JsonElement ParseJsonAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static JsonElement? ParseOptionalJsonAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static (double x, double y) ExtractPointCoordinates(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return (0, 0);
        using var doc = JsonDocument.Parse(geoJson);
        var coords = doc.RootElement.GetProperty("coordinates");
        return (coords[0].GetDouble(), coords[1].GetDouble());
    }

    private static IReadOnlyList<double[]> ExtractLineCoordinates(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return Array.Empty<double[]>();
        using var doc = JsonDocument.Parse(geoJson);
        var coords = doc.RootElement.GetProperty("coordinates");
        var list = new List<double[]>();
        foreach (var c in coords.EnumerateArray())
        {
            list.Add(new[] { c[0].GetDouble(), c[1].GetDouble() });
        }
        return list;
    }

    private static IReadOnlyList<IReadOnlyList<double[]>> ExtractPolygonCoordinates(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return Array.Empty<IReadOnlyList<double[]>>();
        using var doc = JsonDocument.Parse(geoJson);
        var coords = doc.RootElement.GetProperty("coordinates");
        var rings = new List<IReadOnlyList<double[]>>();
        foreach (var ring in coords.EnumerateArray())
        {
            var ringList = new List<double[]>();
            foreach (var c in ring.EnumerateArray())
            {
                ringList.Add(new[] { c[0].GetDouble(), c[1].GetDouble() });
            }
            rings.Add(ringList);
        }
        return rings;
    }

    #endregion
}

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message) { }
}
