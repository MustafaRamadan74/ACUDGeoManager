using System.Text.Json;
using NetTopologySuite.Geometries;

namespace UtilityNetwork.Core.Services;

public interface IFeatureValidationService
{
    void ValidateAttributes(string schemaJson, JsonElement attributes, string featureTypeCode);
    void ValidatePointGeometry(double x, double y);
    void ValidateLineGeometry(IReadOnlyList<double[]> coordinates);
    void ValidatePolygonGeometry(IReadOnlyList<IReadOnlyList<double[]>> rings);
}
