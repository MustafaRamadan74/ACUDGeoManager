using System.Text.RegularExpressions;
using FluentValidation;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Core.Validators;

public sealed partial class CreatePointRequestValidator : AbstractValidator<CreatePointRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{1,31}$")]
    private static partial Regex CodeRegex();

    public CreatePointRequestValidator()
    {
        RuleFor(x => x.NetworkId).NotEmpty().WithMessage("Network ID is required.");
        RuleFor(x => x.FeatureTypeCode)
            .NotEmpty().WithMessage("Feature type code is required.")
            .Matches(CodeRegex()).WithMessage("Feature type code format is invalid.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Matches(NameRegex()).WithMessage("Name must be 1-120 characters and contain valid letters, numbers, spaces, or allowed symbols.");
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s)).WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
        RuleFor(x => x.X).InclusiveBetween(-180, 180).WithMessage("Longitude (X) must be between -180 and 180.");
        RuleFor(x => x.Y).InclusiveBetween(-90, 90).WithMessage("Latitude (Y) must be between -90 and 90.");
    }
}

public sealed partial class UpdatePointRequestValidator : AbstractValidator<UpdatePointRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    public UpdatePointRequestValidator()
    {
        RuleFor(x => x.Version).GreaterThanOrEqualTo(1).WithMessage("Version must be at least 1 for concurrency check.");
        RuleFor(x => x.Name)
            .Matches(NameRegex()).When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Name contains invalid characters or exceeds 120 characters.");
        RuleFor(x => x.Status)
            .Must(s => s == null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
    }
}

public sealed partial class CreateLineRequestValidator : AbstractValidator<CreateLineRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    private static readonly HashSet<string> AllowedFlowDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "WITH_DIGITIZED", "AGAINST_DIGITIZED", "BIDIRECTIONAL", "UNKNOWN"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{1,31}$")]
    private static partial Regex CodeRegex();

    public CreateLineRequestValidator()
    {
        RuleFor(x => x.NetworkId).NotEmpty().WithMessage("Network ID is required.");
        RuleFor(x => x.FeatureTypeCode)
            .NotEmpty().WithMessage("Feature type code is required.")
            .Matches(CodeRegex()).WithMessage("Feature type code format is invalid.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Matches(NameRegex()).WithMessage("Name must be 1-120 characters and contain valid letters, numbers, spaces, or allowed symbols.");
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s)).WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
        RuleFor(x => x.Coordinates)
            .NotNull().Must(c => c != null && c.Count >= 2).WithMessage("Line must have at least 2 coordinate points.");
        RuleFor(x => x.FlowDirection)
            .NotEmpty().WithMessage("Flow direction is required.")
            .Must(f => AllowedFlowDirections.Contains(f)).WithMessage("Flow direction must be: WITH_DIGITIZED, AGAINST_DIGITIZED, BIDIRECTIONAL, UNKNOWN.");
        RuleFor(x => x.DiameterMm)
            .GreaterThan(0).When(x => x.DiameterMm.HasValue).WithMessage("Diameter must be positive.")
            .NotNull().When(x => string.Equals(x.FeatureTypeCode, "PIPE", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Diameter is required for PIPE feature type.");
    }
}

public sealed partial class UpdateLineRequestValidator : AbstractValidator<UpdateLineRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    private static readonly HashSet<string> AllowedFlowDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "WITH_DIGITIZED", "AGAINST_DIGITIZED", "BIDIRECTIONAL", "UNKNOWN"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    public UpdateLineRequestValidator()
    {
        RuleFor(x => x.Version).GreaterThanOrEqualTo(1).WithMessage("Version must be at least 1.");
        RuleFor(x => x.Name)
            .Matches(NameRegex()).When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Name contains invalid characters or exceeds 120 characters.");
        RuleFor(x => x.Status)
            .Must(s => s == null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
        RuleFor(x => x.FlowDirection)
            .Must(f => f == null || AllowedFlowDirections.Contains(f))
            .WithMessage("Invalid flow direction.");
        RuleFor(x => x.DiameterMm)
            .GreaterThan(0).When(x => x.DiameterMm.HasValue).WithMessage("Diameter must be positive.");
    }
}

public sealed partial class CreatePolygonRequestValidator : AbstractValidator<CreatePolygonRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{1,31}$")]
    private static partial Regex CodeRegex();

    public CreatePolygonRequestValidator()
    {
        RuleFor(x => x.NetworkId).NotEmpty().WithMessage("Network ID is required.");
        RuleFor(x => x.FeatureTypeCode)
            .NotEmpty().WithMessage("Feature type code is required.")
            .Matches(CodeRegex()).WithMessage("Feature type code format is invalid.");
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .Matches(NameRegex()).WithMessage("Name must be 1-120 characters and contain valid letters, numbers, spaces, or allowed symbols.");
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => AllowedStatuses.Contains(s)).WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
        RuleFor(x => x.RingCoordinates)
            .NotNull().Must(r => r != null && r.Count > 0).WithMessage("Polygon must have at least one ring.");
    }
}

public sealed partial class UpdatePolygonRequestValidator : AbstractValidator<UpdatePolygonRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE", "INACTIVE", "ABANDONED", "PLANNED", "DAMAGED"
    };

    [GeneratedRegex(@"^[\p{L}\p{N} _\-./()]{1,120}$")]
    private static partial Regex NameRegex();

    public UpdatePolygonRequestValidator()
    {
        RuleFor(x => x.Version).GreaterThanOrEqualTo(1).WithMessage("Version must be at least 1.");
        RuleFor(x => x.Name)
            .Matches(NameRegex()).When(x => !string.IsNullOrWhiteSpace(x.Name))
            .WithMessage("Name contains invalid characters or exceeds 120 characters.");
        RuleFor(x => x.Status)
            .Must(s => s == null || AllowedStatuses.Contains(s))
            .WithMessage("Status must be one of: ACTIVE, INACTIVE, ABANDONED, PLANNED, DAMAGED.");
    }
}
