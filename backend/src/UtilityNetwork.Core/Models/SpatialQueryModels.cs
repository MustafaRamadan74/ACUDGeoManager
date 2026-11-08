using System.Text.Json;

namespace UtilityNetwork.Core.Models;

public sealed record QueryWithinRequest(
    Guid? NetworkId,
    IReadOnlyList<string>? FeatureTypes,
    string? Status,
    JsonElement Polygon,
    int Limit = 500);

public sealed record NearestQueryFilter(
    double X,
    double Y,
    Guid? NetworkId = null,
    string? FeatureType = null,
    string? Status = null,
    double? MaxDistanceM = null,
    int Limit = 10);

public sealed record RichWorkspaceSummaryDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string Domain,
    int TotalFeatures,
    double TotalLengthM,
    double TotalAreaSqm,
    IReadOnlyDictionary<string, int> CountsByType,
    IReadOnlyDictionary<string, int> CountsByStatus,
    IReadOnlyDictionary<string, double> LengthsByTypeM,
    IReadOnlyDictionary<string, double> AreasByTypeSqm,
    IReadOnlyList<TypeStatusCountDto> Details);
