using System.Text.Json;
using Dapper;
using Npgsql;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Data.Services;
using UtilityNetwork.Seed;

namespace UtilityNetwork.Tests;

public sealed class SeedGeneratorTests
{
    private const string ConnectionString = "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    private readonly FeatureValidationService _validator = new();

    [Fact]
    public void SmallSize_GeneratesExpectedFeatureCounts()
    {
        var dataset = SeedDataGenerator.Generate("small");

        Assert.Equal(219, dataset.Points.Count);
        Assert.Equal(66, dataset.Lines.Count);
        Assert.Equal(83, dataset.Polygons.Count);
        Assert.Equal(368, dataset.Points.Count + dataset.Lines.Count + dataset.Polygons.Count);
        Assert.Equal(10, dataset.PlantedErrorsDescription.Count);
    }

    [Fact]
    public void MediumSize_GeneratesScaledDataset()
    {
        var dataset = SeedDataGenerator.Generate("medium");

        Assert.True(dataset.Points.Count > 1000, $"Expected >1000 points, got {dataset.Points.Count}");
        Assert.True(dataset.Lines.Count > 300, $"Expected >300 lines, got {dataset.Lines.Count}");
        Assert.True(dataset.Polygons.Count > 600, $"Expected >600 polygons, got {dataset.Polygons.Count}");
        Assert.True(dataset.Points.Count + dataset.Lines.Count + dataset.Polygons.Count > 2000);
        Assert.Equal(10, dataset.PlantedErrorsDescription.Count);
    }

    [Fact]
    public void PlantedDefects_AreAllPresentAndIdentified()
    {
        var dataset = SeedDataGenerator.Generate("small");

        var pointExtIds = dataset.Points.Select(p => p.ExternalId).ToHashSet();
        var lineExtIds = dataset.Lines.Select(l => l.ExternalId).ToHashSet();
        var polyExtIds = dataset.Polygons.Select(p => p.ExternalId).ToHashSet();

        // 1. Near duplicate buildings
        Assert.Contains("BLD-ERR-01A", polyExtIds);
        Assert.Contains("BLD-ERR-01B", polyExtIds);

        // 2. Overlapping poles
        Assert.Contains("POLE-ERR-02A", pointExtIds);
        Assert.Contains("POLE-ERR-02B", pointExtIds);

        // 3. Ultra-short road
        Assert.Contains("ROAD-ERR-03", lineExtIds);

        // 4. Building overlapping road
        Assert.Contains("BLD-ERR-04", polyExtIds);
        Assert.Contains("ROAD-ERR-04", lineExtIds);

        // 5. Valve off-pipe
        Assert.Contains("VALVE-ERR-05", pointExtIds);

        // 6. Dangling pipe
        Assert.Contains("PIPE-ERR-06", lineExtIds);

        // 7. Duplicate hydrants
        Assert.Contains("HYD-ERR-07A", pointExtIds);
        Assert.Contains("HYD-ERR-07B", pointExtIds);

        // 8. Pipe with UNKNOWN direction
        var unknownPipe = dataset.Lines.FirstOrDefault(l => l.ExternalId == "PIPE-ERR-08");
        Assert.NotNull(unknownPipe);
        Assert.Equal("UNKNOWN", unknownPipe.FlowDirection);

        // 9. Pipe with reversed direction
        var revPipe = dataset.Lines.FirstOrDefault(l => l.ExternalId == "PIPE-ERR-09");
        Assert.NotNull(revPipe);
        Assert.Equal("AGAINST_DIGITIZED", revPipe.FlowDirection);

        // 10. Segment with no source
        Assert.Contains("PIPE-ERR-10", lineExtIds);
        Assert.Contains("HYD-ERR-10", pointExtIds);
    }

    [Fact]
    public async Task AllSeededFeatures_PassPhase5GeometryAndSchemaValidation()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var schemas = (await conn.QueryAsync<(string Code, string SchemaJson)>(
            "SELECT code AS \"Code\", attribute_schema::text AS \"SchemaJson\" FROM feature_types WHERE is_active = true;"))
            .ToDictionary(x => x.Code, x => x.SchemaJson);

        var dataset = SeedDataGenerator.Generate("small");

        // Validate all points
        foreach (var pt in dataset.Points)
        {
            _validator.ValidatePointGeometry(pt.X, pt.Y);
            if (schemas.TryGetValue(pt.FeatureTypeCode, out var schema))
            {
                using var doc = JsonDocument.Parse(pt.AttributesJson);
                _validator.ValidateAttributes(schema, doc.RootElement, pt.FeatureTypeCode);
            }
        }

        // Validate all lines
        foreach (var ln in dataset.Lines)
        {
            _validator.ValidateLineGeometry(ln.Coordinates);
            if (schemas.TryGetValue(ln.FeatureTypeCode, out var schema))
            {
                using var doc = JsonDocument.Parse(ln.AttributesJson);
                _validator.ValidateAttributes(schema, doc.RootElement, ln.FeatureTypeCode);
            }
        }

        // Validate all polygons
        foreach (var poly in dataset.Polygons)
        {
            _validator.ValidatePolygonGeometry(poly.Rings);
            if (schemas.TryGetValue(poly.FeatureTypeCode, out var schema))
            {
                using var doc = JsonDocument.Parse(poly.AttributesJson);
                _validator.ValidateAttributes(schema, doc.RootElement, poly.FeatureTypeCode);
            }
        }
    }

    [Fact]
    public async Task SeedRunner_IsIdempotent_DoesNotDuplicateData()
    {
        var runner = new SeedRunner(ConnectionString);
        var options = new SeedOptions { Size = "small", Clean = false };

        // Run seed 1
        var code1 = await runner.RunAsync(options);
        Assert.Equal(0, code1);

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var count1 = await conn.ExecuteScalarAsync<int>(
            "SELECT (SELECT count(*) FROM points WHERE source = 'SEED_GENERATOR') + " +
            "(SELECT count(*) FROM lines WHERE source = 'SEED_GENERATOR') + " +
            "(SELECT count(*) FROM polygons WHERE source = 'SEED_GENERATOR');");

        Assert.Equal(368, count1);

        // Run seed 2 (should be idempotent)
        var code2 = await runner.RunAsync(options);
        Assert.Equal(0, code2);

        var count2 = await conn.ExecuteScalarAsync<int>(
            "SELECT (SELECT count(*) FROM points WHERE source = 'SEED_GENERATOR') + " +
            "(SELECT count(*) FROM lines WHERE source = 'SEED_GENERATOR') + " +
            "(SELECT count(*) FROM polygons WHERE source = 'SEED_GENERATOR');");

        Assert.Equal(368, count2);
    }

    [Fact]
    public async Task SeedUsers_ExistAndCanAuthenticate()
    {
        var runner = new SeedRunner(ConnectionString);
        await runner.RunAsync(new SeedOptions { Size = "small" });

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var users = (await conn.QueryAsync<(string Username, string Role, string Hash)>(
            "SELECT username AS \"Username\", role::text AS \"Role\", password_hash AS \"Hash\" " +
            "FROM users WHERE username IN ('admin', 'editor', 'viewer');"))
            .ToDictionary(u => u.Username, u => u);

        Assert.True(users.ContainsKey("admin"));
        Assert.True(users.ContainsKey("editor"));
        Assert.True(users.ContainsKey("viewer"));

        Assert.Equal("ADMIN", users["admin"].Role);
        Assert.Equal("EDITOR", users["editor"].Role);
        Assert.Equal("VIEWER", users["viewer"].Role);

        var hasher = new PasswordHasherService();
        Assert.True(hasher.VerifyPassword(users["editor"].Hash, "Editor@Passw0rd123!"));
        Assert.True(hasher.VerifyPassword(users["viewer"].Hash, "Viewer@Passw0rd123!"));

        // Check network access for both networks
        foreach (var u in users.Values)
        {
            var accessCount = await conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM user_network_access WHERE user_id = " +
                "(SELECT id FROM users WHERE username = @username);",
                new { username = u.Username });

            Assert.True(accessCount >= 2, $"Expected at least 2 network accesses for {u.Username}, got {accessCount}");
        }
    }

    [Fact]
    public async Task AuditLog_And_ChangeSets_CaptureSeedOperations()
    {
        var runner = new SeedRunner(ConnectionString);
        await runner.RunAsync(new SeedOptions { Size = "small" });

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var changeSetCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM change_sets WHERE description LIKE 'Seed Data Generation%';");
        Assert.True(changeSetCount >= 1);

        var auditCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM audit_log WHERE username = 'admin' AND action = 'INSERT';");
        Assert.True(auditCount >= 368);
    }
}
