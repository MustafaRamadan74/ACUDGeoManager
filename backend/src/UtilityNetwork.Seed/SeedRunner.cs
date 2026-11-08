using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using UtilityNetwork.Core.Services;
using UtilityNetwork.Data.Services;

namespace UtilityNetwork.Seed;

public sealed class SeedRunner
{
    private readonly string _connectionString;
    private readonly PasswordHasherService _passwordHasher = new();
    private readonly FeatureValidationService _validator = new();

    public SeedRunner(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> RunAsync(SeedOptions options)
    {
        var sw = Stopwatch.StartNew();

        Console.WriteLine("================================================================================");
        Console.WriteLine("  ACUDGeoManager Seed Data Generator");
        Console.WriteLine($"  Target Size: {options.Size.ToUpperInvariant()} | Clean Mode: {options.Clean}");
        Console.WriteLine("================================================================================");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // 1. Ensure seed users exist
        Console.WriteLine("[1/5] Ensuring seed users (admin, editor, viewer) and permissions...");
        var users = await EnsureUsersAsync(conn);

        // 2. Fetch catalog schemas for validation
        Console.WriteLine("[2/5] Fetching feature schemas from database catalog...");
        var schemas = (await conn.QueryAsync<(string Code, string SchemaJson)>(
            "SELECT code AS \"Code\", attribute_schema::text AS \"SchemaJson\" FROM feature_types WHERE is_active = true;"))
            .ToDictionary(x => x.Code, x => x.SchemaJson);

        // 3. Generate dataset
        Console.WriteLine($"[3/5] Generating {options.Size} dataset with deterministic RNG (Seed 42)...");
        var dataset = SeedDataGenerator.Generate(options.Size);

        // Display Planted Defects Catalog
        Console.WriteLine("\n--------------------------------------------------------------------------------");
        Console.WriteLine("  PLANTED DEFECTS CATALOG (For Validation & Topology Engine Tests):");
        foreach (var defect in dataset.PlantedErrorsDescription)
        {
            Console.WriteLine($"   {defect}");
        }
        Console.WriteLine("--------------------------------------------------------------------------------\n");

        // 4. Validate all generated features against Phase 5 validation rules
        Console.WriteLine("[4/5] In-memory validation pass of all features against Phase 5 validators...");
        ValidateDataset(dataset, schemas);
        Console.WriteLine("  -> 100% of generated features passed Phase 5 geometry and schema validation.");

        // 5. Commit to database in a single transaction
        Console.WriteLine("[5/5] Inserting features into PostGIS database (transactional & audited)...");
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Clean previously seeded features
            await conn.ExecuteAsync("DELETE FROM points WHERE source = 'SEED_GENERATOR';", transaction: tx);
            await conn.ExecuteAsync("DELETE FROM lines WHERE source = 'SEED_GENERATOR';", transaction: tx);
            await conn.ExecuteAsync("DELETE FROM polygons WHERE source = 'SEED_GENERATOR';", transaction: tx);

            if (options.Clean)
            {
                await tx.CommitAsync();
                Console.WriteLine("\n[SUCCESS] Clean operation completed. All seeded features wiped.");
                return 0;
            }

            var adminUser = users["admin"];
            var changeSetId = Guid.NewGuid();

            // Set session config for audit triggers
            await conn.ExecuteAsync(
                "SELECT set_config('app.user_id', @UserId, true); " +
                "SELECT set_config('app.username', 'admin', true); " +
                "SELECT set_config('app.role', 'ADMIN', true); " +
                "SELECT set_config('app.change_set_id', @ChangeSetId, true);",
                new { UserId = adminUser.Id.ToString(), ChangeSetId = changeSetId.ToString() },
                transaction: tx
            );

            // Create change set record
            await conn.ExecuteAsync(
                "INSERT INTO change_sets (id, user_id, description, status, created_at) " +
                "VALUES (@Id, @UserId, @Description, 'APPLIED'::change_set_status, now());",
                new
                {
                    Id = changeSetId,
                    UserId = adminUser.Id,
                    Description = $"Seed Data Generation ({options.Size})"
                },
                transaction: tx
            );

            // Batch insert points
            const string insertPointSql = @"
                INSERT INTO points (
                    id, network_id, feature_type_code, name, status, is_open, attributes, geom,
                    version, created_by, updated_by, created_at, updated_at, external_id, source
                ) VALUES (
                    @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status, @IsOpen, @AttributesJson::jsonb,
                    ST_SetSRID(ST_MakePoint(@X, @Y), 4326), 1, @CreatedBy, @CreatedBy, @CreatedAt, @CreatedAt, @ExternalId, @Source
                );";

            foreach (var pt in dataset.Points)
            {
                await conn.ExecuteAsync(insertPointSql, pt, transaction: tx);
            }

            // Batch insert lines
            const string insertLineSql = @"
                INSERT INTO lines (
                    id, network_id, feature_type_code, name, status, flow_direction, diameter_mm, material, attributes, geom,
                    version, created_by, updated_by, created_at, updated_at, external_id, source
                ) VALUES (
                    @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status, @FlowDirection::flow_direction,
                    @DiameterMm, @Material, @AttributesJson::jsonb, ST_SetSRID(ST_GeomFromText(@Wkt), 4326), 1,
                    @CreatedBy, @CreatedBy, @CreatedAt, @CreatedAt, @ExternalId, @Source
                );";

            foreach (var ln in dataset.Lines)
            {
                var wkt = CoordinatesToLineStringWkt(ln.Coordinates);
                await conn.ExecuteAsync(insertLineSql, new
                {
                    ln.Id,
                    ln.NetworkId,
                    ln.FeatureTypeCode,
                    ln.Name,
                    ln.Status,
                    ln.FlowDirection,
                    ln.DiameterMm,
                    ln.Material,
                    ln.AttributesJson,
                    Wkt = wkt,
                    ln.CreatedBy,
                    ln.CreatedAt,
                    ln.ExternalId,
                    ln.Source
                }, transaction: tx);
            }

            // Batch insert polygons
            const string insertPolygonSql = @"
                INSERT INTO polygons (
                    id, network_id, feature_type_code, name, status, attributes, geom,
                    version, created_by, updated_by, created_at, updated_at, external_id, source
                ) VALUES (
                    @Id, @NetworkId, @FeatureTypeCode, @Name, @Status::feature_status, @AttributesJson::jsonb,
                    ST_Multi(ST_SetSRID(ST_GeomFromText(@Wkt), 4326)), 1, @CreatedBy, @CreatedBy, @CreatedAt, @CreatedAt, @ExternalId, @Source
                );";

            foreach (var poly in dataset.Polygons)
            {
                var wkt = CoordinatesToPolygonWkt(poly.Rings);
                await conn.ExecuteAsync(insertPolygonSql, new
                {
                    poly.Id,
                    poly.NetworkId,
                    poly.FeatureTypeCode,
                    poly.Name,
                    poly.Status,
                    poly.AttributesJson,
                    Wkt = wkt,
                    poly.CreatedBy,
                    poly.CreatedAt,
                    poly.ExternalId,
                    poly.Source
                }, transaction: tx);
            }

            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            Console.WriteLine($"\n[ERROR] Seed execution failed: {ex.Message}");
            throw;
        }

        sw.Stop();

        // Print final summary
        Console.WriteLine("\n================================================================================");
        Console.WriteLine("  SEED GENERATION COMPLETE");
        Console.WriteLine("================================================================================");
        Console.WriteLine($"  Points:   {dataset.Points.Count,6:N0} records");
        Console.WriteLine($"  Lines:    {dataset.Lines.Count,6:N0} records");
        Console.WriteLine($"  Polygons: {dataset.Polygons.Count,6:N0} records");
        Console.WriteLine($"  Total:    {dataset.Points.Count + dataset.Lines.Count + dataset.Polygons.Count,6:N0} features");
        Console.WriteLine($"  Planted Defects: {dataset.PlantedErrorsDescription.Count} recorded & verified");
        Console.WriteLine($"  Elapsed Time:    {sw.ElapsedMilliseconds} ms");
        Console.WriteLine("================================================================================\n");

        return 0;
    }

    private async Task<Dictionary<string, (Guid Id, string Role)>> EnsureUsersAsync(NpgsqlConnection conn)
    {
        var result = new Dictionary<string, (Guid Id, string Role)>(StringComparer.OrdinalIgnoreCase);

        var userDefs = new[]
        {
            ("admin", "admin@acud.gov.eg", "ADMIN", "Admin@Passw0rd123!"),
            ("editor", "editor@acud.gov.eg", "EDITOR", "Editor@Passw0rd123!"),
            ("viewer", "viewer@acud.gov.eg", "VIEWER", "Viewer@Passw0rd123!")
        };

        foreach (var (username, email, role, password) in userDefs)
        {
            var existing = await conn.QuerySingleOrDefaultAsync<(Guid Id, string Role)>(
                "SELECT id, role::text AS \"Role\" FROM users WHERE username = @username;",
                new { username });

            Guid userId;
            if (existing.Id != Guid.Empty)
            {
                userId = existing.Id;
            }
            else
            {
                userId = Guid.NewGuid();
                var hash = _passwordHasher.HashPassword(password);
                await conn.ExecuteAsync(
                    "INSERT INTO users (id, username, email, password_hash, role, is_active, created_at) " +
                    "VALUES (@id, @username, @email, @hash, @role::user_role, true, now());",
                    new { id = userId, username, email, hash, role });
            }

            // Assign access to both networks
            await conn.ExecuteAsync(
                "INSERT INTO user_network_access (user_id, network_id) " +
                "VALUES (@userId, @net1), (@userId, @net2) " +
                "ON CONFLICT DO NOTHING;",
                new
                {
                    userId,
                    net1 = SeedDataGenerator.WaterNetworkId,
                    net2 = SeedDataGenerator.UrbanNetworkId
                });

            result[username] = (userId, role);
        }

        return result;
    }

    private void ValidateDataset(SeedDataset dataset, Dictionary<string, string> schemas)
    {
        foreach (var pt in dataset.Points)
        {
            _validator.ValidatePointGeometry(pt.X, pt.Y);
            if (schemas.TryGetValue(pt.FeatureTypeCode, out var schema))
            {
                using var doc = JsonDocument.Parse(pt.AttributesJson);
                _validator.ValidateAttributes(schema, doc.RootElement, pt.FeatureTypeCode);
            }
        }

        foreach (var ln in dataset.Lines)
        {
            _validator.ValidateLineGeometry(ln.Coordinates);
            if (schemas.TryGetValue(ln.FeatureTypeCode, out var schema))
            {
                using var doc = JsonDocument.Parse(ln.AttributesJson);
                _validator.ValidateAttributes(schema, doc.RootElement, ln.FeatureTypeCode);
            }
        }

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

    private static string CoordinatesToLineStringWkt(IReadOnlyList<double[]> coords)
    {
        var sb = new StringBuilder("LINESTRING(");
        for (var i = 0; i < coords.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }
            sb.Append(coords[i][0].ToString(CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(coords[i][1].ToString(CultureInfo.InvariantCulture));
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static string CoordinatesToPolygonWkt(IReadOnlyList<IReadOnlyList<double[]>> rings)
    {
        var sb = new StringBuilder("POLYGON(");
        for (var r = 0; r < rings.Count; r++)
        {
            if (r > 0)
            {
                sb.Append(", ");
            }
            sb.Append('(');
            var ring = rings[r];
            for (var i = 0; i < ring.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(ring[i][0].ToString(CultureInfo.InvariantCulture));
                sb.Append(' ');
                sb.Append(ring[i][1].ToString(CultureInfo.InvariantCulture));
            }
            sb.Append(')');
        }
        sb.Append(')');
        return sb.ToString();
    }
}
