using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class FeaturesCrudTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private readonly WebApplicationFactory<Program> _factory;

    public FeaturesCrudTests(WebApplicationFactory<Program> factory)
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
    public async Task Catalog_Endpoints_ReturnSeededNetworksAndFeatureTypes()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        using var netReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/networks");
        netReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var netRes = await client.SendAsync(netReq);
        Assert.Equal(HttpStatusCode.OK, netRes.StatusCode);
        var networks = await netRes.Content.ReadFromJsonAsync<IReadOnlyList<NetworkDto>>();
        Assert.NotNull(networks);
        Assert.Contains(networks, n => n.Code == "WATER_DISTRIBUTION");

        using var typeReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/feature-types");
        typeReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var typeRes = await client.SendAsync(typeReq);
        Assert.Equal(HttpStatusCode.OK, typeRes.StatusCode);
        var types = await typeRes.Content.ReadFromJsonAsync<IReadOnlyList<FeatureTypeDto>>();
        Assert.NotNull(types);
        Assert.Contains(types, t => t.Code == "VALVE");
        Assert.Contains(types, t => t.Code == "PIPE");
        Assert.Contains(types, t => t.Code == "SERVICE_ZONE");
    }

    [Fact]
    public async Task PointsCrud_FullLifecycle_AndOptimisticConcurrency409()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        // 1. CREATE Point
        var validAttributes = JsonDocument.Parse("{\"valve_type\":\"GATE\",\"depth_m\":1.8,\"turns_to_close\":10}").RootElement;
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Sector 3 Control Valve",
            Status: "ACTIVE",
            Attributes: validAttributes,
            X: 31.65,
            Y: 30.05,
            IsOpen: true);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/points");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createMsg.Content = JsonContent.Create(createReq);

        var createRes = await client.SendAsync(createMsg);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(created);
        Assert.Equal(1, created.Version);
        Assert.Equal("Sector 3 Control Valve", created.Name);

        // 2. UPDATE Point with valid version (1)
        var updateReq = new UpdatePointRequest(
            Name: "Sector 3 Control Valve - Main",
            Status: "ACTIVE",
            Attributes: null,
            X: null,
            Y: null,
            IsOpen: false,
            Version: 1);

        using var updateMsg = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/points/{created.Id}");
        updateMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        updateMsg.Content = JsonContent.Create(updateReq);

        var updateRes = await client.SendAsync(updateMsg);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        var updated = await updateRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(updated);
        Assert.Equal(2, updated.Version); // Trigger incremented version to 2
        Assert.Equal("Sector 3 Control Valve - Main", updated.Name);
        Assert.False(updated.IsOpen);

        // 3. CONCURRENT UPDATE with stale version (1 instead of 2) -> 409 Conflict
        var staleUpdateReq = new UpdatePointRequest(
            Name: "Stale Edit",
            Status: "ACTIVE",
            Attributes: null,
            X: null,
            Y: null,
            IsOpen: true,
            Version: 1);

        using var staleMsg = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/points/{created.Id}");
        staleMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        staleMsg.Content = JsonContent.Create(staleUpdateReq);

        var staleRes = await client.SendAsync(staleMsg);
        Assert.Equal(HttpStatusCode.Conflict, staleRes.StatusCode);
        var conflictDetails = await staleRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(conflictDetails);
        Assert.Equal(409, conflictDetails.Status);

        // 4. CONCURRENT DELETE with stale version (1 instead of 2) -> 409 Conflict
        using var staleDelMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/points/{created.Id}?version=1");
        staleDelMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var staleDelRes = await client.SendAsync(staleDelMsg);
        Assert.Equal(HttpStatusCode.Conflict, staleDelRes.StatusCode);

        // 5. DELETE with matching version (2) -> 204 NoContent
        using var delMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/points/{created.Id}?version=2");
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await client.SendAsync(delMsg);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 6. Verify GET returns 404
        using var getMsg = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/points/{created.Id}");
        getMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getRes = await client.SendAsync(getMsg);
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task LinesCrud_WithValidationAndGeoJson()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        var pipeAttributes = JsonDocument.Parse("{\"installation_year\":2025,\"pressure_bar\":8.0}").RootElement;
        var coords = new double[][]
        {
            [31.650, 30.050],
            [31.655, 30.055]
        };

        var createReq = new CreateLineRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "PIPE",
            Name: "Sector 3 Main Trunk",
            Status: "ACTIVE",
            Attributes: pipeAttributes,
            Coordinates: coords,
            FlowDirection: "WITH_DIGITIZED",
            DiameterMm: 400,
            Material: "DI");

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/lines");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        createMsg.Content = JsonContent.Create(createReq);

        var createRes = await client.SendAsync(createMsg);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<LineFeatureDto>();
        Assert.NotNull(created);
        Assert.True(created.LengthM > 0, "Line length in metres must be calculated and positive.");

        // Cleanup
        using var delMsg = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/lines/{created.Id}?version={created.Version}");
        delMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var delRes = await client.SendAsync(delMsg);
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    public async Task ValidationFailures_ReturnClearProblemDetails()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        // 1. Invalid JSON Schema: depth_m cannot be negative in VALVE schema
        var invalidAttributes = JsonDocument.Parse("{\"valve_type\":\"GATE\",\"depth_m\":-5.0}").RootElement;
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Bad Attributes Valve",
            Status: "ACTIVE",
            Attributes: invalidAttributes,
            X: 31.65,
            Y: 30.05,
            IsOpen: true);

        using var msg1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/points");
        msg1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg1.Content = JsonContent.Create(createReq);
        var res1 = await client.SendAsync(msg1);

        Assert.Equal(HttpStatusCode.BadRequest, res1.StatusCode);
        var prob1 = await res1.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(prob1);
        Assert.Equal(400, prob1.Status);
        Assert.NotNull(prob1.Extensions);
        Assert.True(prob1.Extensions.ContainsKey("errors"));

        // 2. Invalid Geometry Bounds: X (longitude) > 180
        var invalidCoordReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Out of Bounds Valve",
            Status: "ACTIVE",
            Attributes: JsonDocument.Parse("{}").RootElement,
            X: 200.0,
            Y: 30.0,
            IsOpen: true);

        using var msg2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/points");
        msg2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        msg2.Content = JsonContent.Create(invalidCoordReq);
        var res2 = await client.SendAsync(msg2);

        Assert.Equal(HttpStatusCode.BadRequest, res2.StatusCode);
    }

    [Fact]
    public async Task NetworkAuthorization_EditorInNetworkACannotTouchNetworkB()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var adminToken = await GetAdminTokenAsync(client);

        // 1. Create Network B in database
        var networkBId = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO networks (id, code, name, domain, is_directed, snap_tolerance_m)
            VALUES (@networkBId, 'SEWER_DIST', 'New Capital Sewer Network', 'SEWER', true, 0.5)
            ON CONFLICT (code) DO NOTHING;", new { networkBId });

        // 2. Create User EditorA with access ONLY to Water Network A
        var editorUsername = $"ed_{Guid.NewGuid():N}"[..16];
        var createEditorReq = new CreateUserRequest(
            editorUsername,
            $"{editorUsername}@acud.gov.eg",
            "EditorPass123!",
            "EDITOR",
            [_waterNetworkId]);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        createMsg.Content = JsonContent.Create(createEditorReq);
        var createEditorRes = await client.SendAsync(createMsg);
        Assert.Equal(HttpStatusCode.Created, createEditorRes.StatusCode);

        // 3. Login as EditorA
        var editorLoginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(editorUsername, "EditorPass123!"));
        var editorAuth = await editorLoginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(editorAuth);

        // 4. Attempt to create point in Network B -> must be REJECTED (Unauthorized/Forbidden)
        var createPointReq = new CreatePointRequest(
            NetworkId: networkBId,
            FeatureTypeCode: "VALVE",
            Name: "Cross Network Attempt Valve",
            Status: "ACTIVE",
            Attributes: JsonDocument.Parse("{}").RootElement,
            X: 31.65,
            Y: 30.05,
            IsOpen: true);

        using var crossMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/points");
        crossMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", editorAuth.AccessToken);
        crossMsg.Content = JsonContent.Create(createPointReq);
        var crossRes = await client.SendAsync(crossMsg);

        Assert.True(crossRes.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Security_SqlInjectionPayloads_HandledSafely()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        // Test SQL Injection payload in query parameters
        var injectionQuery = "/api/v1/points?type=VALVE'; DROP TABLE points; --&status=ACTIVE' OR '1'='1";
        using var queryMsg = new HttpRequestMessage(HttpMethod.Get, injectionQuery);
        queryMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var queryRes = await client.SendAsync(queryMsg);

        // API should return 200 with empty list or 400 without executing the injection!
        Assert.True(queryRes.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);

        // Verify points table still exists and is healthy
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM points;");
        Assert.True(count >= 0);
    }

    [Fact]
    public async Task MapFeatures_ReturnsUnifiedGeoJsonFeatureCollection()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);

        using var mapReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/map/features?bbox=30.0,28.0,33.0,32.0&limit=100");
        mapReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var mapRes = await client.SendAsync(mapReq);

        Assert.Equal(HttpStatusCode.OK, mapRes.StatusCode);
        var featureCollection = await mapRes.Content.ReadFromJsonAsync<GeoJsonFeatureCollection>();
        Assert.NotNull(featureCollection);
        Assert.Equal("FeatureCollection", featureCollection.Type);
        Assert.NotNull(featureCollection.Features);
    }
}
