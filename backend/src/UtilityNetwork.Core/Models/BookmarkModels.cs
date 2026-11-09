using System.ComponentModel.DataAnnotations;

namespace UtilityNetwork.Core.Models;

public sealed record BookmarkDto(
    Guid Id,
    Guid UserId,
    string Name,
    double CenterLon,
    double CenterLat,
    int Zoom,
    string? Bbox,
    DateTime CreatedAt
);

public sealed record CreateBookmarkRequest(
    [Required]
    [StringLength(120, MinimumLength = 1)]
    string Name,

    [Range(-180.0, 180.0)]
    double CenterLon,

    [Range(-90.0, 90.0)]
    double CenterLat,

    [Range(1, 24)]
    int Zoom = 15,

    string? Bbox = null
);

