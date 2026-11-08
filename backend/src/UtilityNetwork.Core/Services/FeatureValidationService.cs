using System.Collections.Concurrent;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Json.Schema;
using NetTopologySuite.Geometries;

namespace UtilityNetwork.Core.Services;

public sealed class FeatureValidationService : IFeatureValidationService
{
    private static readonly ConcurrentDictionary<string, JsonSchema> SchemaCache = new();
    private static readonly GeometryFactory GeoFactory = new(new PrecisionModel(), 4326);

    public void ValidateAttributes(string schemaJson, JsonElement attributes, string featureTypeCode)
    {
        if (string.IsNullOrWhiteSpace(schemaJson) || schemaJson.Trim() == "{}")
        {
            return;
        }

        var schema = SchemaCache.GetOrAdd(schemaJson, text => JsonSchema.FromText(text));

        var result = schema.Evaluate(attributes, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List
        });

        if (!result.IsValid)
        {
            var failures = new List<ValidationFailure>();
            if (result.Details != null)
            {
                foreach (var detail in result.Details)
                {
                    if (detail.Errors != null && detail.Errors.Count > 0)
                    {
                        foreach (var (keyword, errorMsg) in detail.Errors)
                        {
                            var propPath = detail.InstanceLocation.ToString().TrimStart('/');
                            var fieldName = string.IsNullOrWhiteSpace(propPath) ? "attributes" : $"attributes.{propPath}";
                            failures.Add(new ValidationFailure(fieldName, $"Attribute error at '{propPath}': {errorMsg} ({keyword})"));
                        }
                    }
                }
            }

            if (failures.Count == 0 && result.Errors != null)
            {
                foreach (var (k, v) in result.Errors)
                {
                    failures.Add(new ValidationFailure("attributes", $"Attribute error: {v} ({k})"));
                }
            }

            if (failures.Count == 0)
            {
                failures.Add(new ValidationFailure("attributes", $"Feature attributes do not conform to schema defined for '{featureTypeCode}'."));
            }

            throw new ValidationException(failures);
        }
    }

    public void ValidatePointGeometry(double x, double y)
    {
        if (x is < -180.0 or > 180.0)
        {
            throw new ValidationException([new ValidationFailure("x", "Longitude (x) must be between -180 and 180 degrees.")]);
        }
        if (y is < -90.0 or > 90.0)
        {
            throw new ValidationException([new ValidationFailure("y", "Latitude (y) must be between -90 and 90 degrees.")]);
        }
    }

    public void ValidateLineGeometry(IReadOnlyList<double[]> coordinates)
    {
        if (coordinates is null || coordinates.Count < 2)
        {
            throw new ValidationException([new ValidationFailure("coordinates", "A line feature must have at least 2 coordinate points.")]);
        }

        if (coordinates.Count > 10000)
        {
            throw new ValidationException([new ValidationFailure("coordinates", "Line vertex count exceeds safety limit of 10,000 points.")]);
        }

        var ntsCoordinates = new Coordinate[coordinates.Count];
        for (var i = 0; i < coordinates.Count; i++)
        {
            var pt = coordinates[i];
            if (pt is null || pt.Length < 2)
            {
                throw new ValidationException([new ValidationFailure($"coordinates[{i}]", "Each vertex coordinate must contain [longitude, latitude].")]);
            }
            if (pt[0] is < -180.0 or > 180.0 || pt[1] is < -90.0 or > 90.0)
            {
                throw new ValidationException([new ValidationFailure($"coordinates[{i}]", "Vertex coordinates must be within valid WGS84 bounds.")]);
            }
            ntsCoordinates[i] = new Coordinate(pt[0], pt[1]);
        }

        var line = GeoFactory.CreateLineString(ntsCoordinates);
        if (!line.IsValid)
        {
            throw new ValidationException([new ValidationFailure("coordinates", "LineString geometry is mathematically invalid.")]);
        }
        if (line.Length <= 0)
        {
            throw new ValidationException([new ValidationFailure("coordinates", "LineString must have a non-zero length with distinct coordinates.")]);
        }
    }

    public void ValidatePolygonGeometry(IReadOnlyList<IReadOnlyList<double[]>> rings)
    {
        if (rings is null || rings.Count == 0)
        {
            throw new ValidationException([new ValidationFailure("ringCoordinates", "A polygon feature must contain at least one exterior ring.")]);
        }

        var outerRingCoords = rings[0];
        if (outerRingCoords is null || outerRingCoords.Count < 4)
        {
            throw new ValidationException([new ValidationFailure("ringCoordinates[0]", "The exterior ring must contain at least 4 coordinates (closed loop).")]);
        }

        var ntsOuterCoords = new Coordinate[outerRingCoords.Count];
        for (var i = 0; i < outerRingCoords.Count; i++)
        {
            var pt = outerRingCoords[i];
            if (pt is null || pt.Length < 2)
            {
                throw new ValidationException([new ValidationFailure($"ringCoordinates[0][{i}]", "Each vertex must contain [longitude, latitude].")]);
            }
            if (pt[0] is < -180.0 or > 180.0 || pt[1] is < -90.0 or > 90.0)
            {
                throw new ValidationException([new ValidationFailure($"ringCoordinates[0][{i}]", "Coordinates must be within WGS84 bounds.")]);
            }
            ntsOuterCoords[i] = new Coordinate(pt[0], pt[1]);
        }

        // Enforce closed ring (first == last)
        if (!ntsOuterCoords[0].Equals2D(ntsOuterCoords[^1]))
        {
            throw new ValidationException([new ValidationFailure("ringCoordinates[0]", "The polygon exterior ring must be closed (start coordinate must equal end coordinate).")]);
        }

        var linearRing = GeoFactory.CreateLinearRing(ntsOuterCoords);
        var poly = GeoFactory.CreatePolygon(linearRing);

        if (!poly.IsValid)
        {
            throw new ValidationException([new ValidationFailure("ringCoordinates", "Polygon geometry is self-intersecting or mathematically invalid.")]);
        }
    }
}
