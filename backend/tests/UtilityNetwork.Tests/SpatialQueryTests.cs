using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class SpatialQueryTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private readonly WebApplicationFactory<Program> _factory;

    public SpatialQueryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<string> GetAdminTokenAsync(HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        var body = await res.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        return body.AccessToken;
    }

    [Fact]
    public async Task QueryNearest_ReturnsAscendingDistancesInMeters()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/query/nearest?x=31.748&y=30.015&limit=5");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);

        var features = doc.RootElement.GetProperty("features").EnumerateArray().ToList();
        Assert.NotEmpty(features);
        Assert.True(features.Count <= 5);

        double lastDistance = -1;
        foreach (var f in features)
        {
            var props = f.GetProperty("properties");
            Assert.True(props.TryGetProperty("distanceMeters", out var distElem) || props.TryGetProperty("distance_meters", out distElem));
            var dist = distElem.GetDouble();
            Assert.True(dist >= 0, "Distance in metres must be non-negative.");
            Assert.True(dist >= lastDistance, "Nearest results must be sorted ascending by exact distance.");
            lastDistance = dist;
        }
    }

    [Fact]
    public async Task QueryWithin_WithPolygon_ReturnsIntersectingFeatures()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        // Bounding polygon covering NAC central core
        var polygonJson = JsonDocument.Parse(@"{
            ""type"": ""Polygon"",
            ""coordinates"": [
                [
                    [31.70, 30.00],
                    [31.80, 30.00],
                    [31.80, 30.05],
                    [31.70, 30.05],
                    [31.70, 30.00]
                ]
            ]
        }").RootElement;

        var queryBody = new QueryWithinRequest(
            NetworkId: _waterNetworkId,
            FeatureTypes: null,
            Status: null,
            Polygon: polygonJson,
            Limit: 50);

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/query/within")
        {
            Content = JsonContent.Create(queryBody)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var doc = await res.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(doc);

        var features = doc.RootElement.GetProperty("features").EnumerateArray().ToList();
        Assert.NotEmpty(features);
    }

    [Fact]
    public async Task RichWorkspaceSummary_ReturnsCountsLengthsAndAreas()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/workspaces/{_waterNetworkId}/summary");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var summary = await res.Content.ReadFromJsonAsync<RichWorkspaceSummaryDto>();
        Assert.NotNull(summary);

        Assert.Equal(_waterNetworkId, summary.WorkspaceId);
        Assert.True(summary.TotalFeatures > 0, "Total features must be greater than zero.");
        Assert.True(summary.TotalLengthM > 0, "Total line length in metres must be greater than zero.");
        Assert.NotEmpty(summary.CountsByType);
        Assert.NotEmpty(summary.CountsByStatus);
        Assert.Contains("PIPE", summary.CountsByType.Keys);
        Assert.Contains("VALVE", summary.CountsByType.Keys);
        Assert.True(summary.LengthsByTypeM.ContainsKey("PIPE"), "PIPE length in metres must be calculated.");
    }
}
