using System.Globalization;
using Npgsql;
using UtilityNetwork.Core.Configuration;

namespace UtilityNetwork.Tests;

public sealed class SchemaValidationTests
{
    private const string ConnectionString = "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task Catalog_ShouldHaveSeededWaterNetworkAndFeatureTypes()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // 1. Verify network exists
        await using var cmdNet = conn.CreateCommand();
        cmdNet.CommandText = "SELECT code, domain, is_directed, snap_tolerance_m FROM networks WHERE code = 'WATER_DISTRIBUTION';";
        await using var readerNet = await cmdNet.ExecuteReaderAsync();
        Assert.True(await readerNet.ReadAsync(), "Water distribution network should be seeded.");
        Assert.Equal("WATER", readerNet.GetString(1));
        Assert.True(readerNet.GetBoolean(2));
        Assert.Equal(0.5, readerNet.GetDouble(3));
        await readerNet.CloseAsync();

        // 2. Verify all 7 feature types exist
        await using var cmdTypes = conn.CreateCommand();
        cmdTypes.CommandText = "SELECT count(*) FROM feature_types WHERE network_domain = 'WATER';";
        var count = Convert.ToInt32(await cmdTypes.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Assert.Equal(7, count);
    }

    [Fact]
    public async Task SpatialTables_ShouldHaveBothGistIndexes()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        string[] requiredIndexes =
        [
            "idx_points_geom", "idx_points_geog",
            "idx_lines_geom", "idx_lines_geog",
            "idx_polygons_geom", "idx_polygons_geog"
        ];

        foreach (var indexName in requiredIndexes)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM pg_indexes WHERE indexname = @idx;";
            cmd.Parameters.AddWithValue("idx", indexName);
            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            Assert.True(exists > 0, $"Expected GiST index '{indexName}' to exist.");
        }
    }

    [Fact]
    public async Task Points_WhenNameIsEmpty_ShouldFailConstraint()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO points (network_id, feature_type_code, name, geom)
            VALUES (@net, 'VALVE', '   ', ST_SetSRID(ST_MakePoint(31.6, 30.0), 4326));";
        cmd.Parameters.AddWithValue("net", _waterNetworkId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", ex.SqlState); // check_violation
        Assert.Contains("chk_points_name_not_empty", ex.ConstraintName);
    }

    [Fact]
    public async Task Points_WhenGeometryKindIsNotPoint_ShouldFailConstraint()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO points (network_id, feature_type_code, name, geom)
            VALUES (@net, 'VALVE', 'Invalid Valve', ST_GeomFromText('LINESTRING(31.6 30.0, 31.7 30.1)', 4326));";
        cmd.Parameters.AddWithValue("net", _waterNetworkId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.True(ex.SqlState is "22023" or "23514" or "42804");
        Assert.Contains("Geometry type", ex.Message);
    }

    [Fact]
    public async Task Points_WhenStatusIsInvalid_ShouldFailConstraint()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO points (network_id, feature_type_code, name, status, geom)
            VALUES (@net, 'VALVE', 'Bad Status Valve', 'NON_EXISTENT_STATUS'::feature_status, ST_SetSRID(ST_MakePoint(31.6, 30.0), 4326));";
        cmd.Parameters.AddWithValue("net", _waterNetworkId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("22P02", ex.SqlState); // invalid_text_representation
    }

    [Fact]
    public async Task Lines_WhenLengthIsZero_ShouldFailConstraint()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lines (network_id, feature_type_code, name, diameter_mm, geom)
            VALUES (@net, 'PIPE', 'Zero Length Pipe', 200, ST_GeomFromText('LINESTRING(31.6 30.0, 31.6 30.0)', 4326));";
        cmd.Parameters.AddWithValue("net", _waterNetworkId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", ex.SqlState); // check_violation
        Assert.True(ex.ConstraintName is "chk_lines_geom_valid" or "chk_lines_nonzero_length");
    }

    [Fact]
    public async Task Lines_WhenPipeDiameterIsZeroOrNegative_ShouldFailConstraint()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lines (network_id, feature_type_code, name, diameter_mm, geom)
            VALUES (@net, 'PIPE', 'Invalid Pipe', 0, ST_GeomFromText('LINESTRING(31.6 30.0, 31.7 30.1)', 4326));";
        cmd.Parameters.AddWithValue("net", _waterNetworkId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteNonQueryAsync());
        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("chk_lines_pipe_diameter", ex.ConstraintName);
    }

    [Fact]
    public async Task UpdateTrigger_ShouldIncrementVersionAndSetUpdatedAt()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        // 1. Insert a test point
        var pointId = Guid.NewGuid();
        await using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO points (id, network_id, feature_type_code, name, geom)
            VALUES (@id, @net, 'VALVE', 'Trigger Test Valve', ST_SetSRID(ST_MakePoint(31.65, 30.05), 4326));";
        insertCmd.Parameters.AddWithValue("id", pointId);
        insertCmd.Parameters.AddWithValue("net", _waterNetworkId);
        await insertCmd.ExecuteNonQueryAsync();

        // 2. Query initial version
        await using var selectCmd = conn.CreateCommand();
        selectCmd.CommandText = "SELECT version, updated_at FROM points WHERE id = @id;";
        selectCmd.Parameters.AddWithValue("id", pointId);
        await using var reader = await selectCmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var initialVersion = reader.GetInt32(0);
        var initialUpdatedAt = reader.GetDateTime(1);
        await reader.CloseAsync();

        // 3. Update point
        await Task.Delay(50); // Ensure timestamp advances
        await using var updateCmd = conn.CreateCommand();
        updateCmd.CommandText = "UPDATE points SET name = 'Renamed Valve' WHERE id = @id;";
        updateCmd.Parameters.AddWithValue("id", pointId);
        await updateCmd.ExecuteNonQueryAsync();

        // 4. Query updated version
        await using var verifyCmd = conn.CreateCommand();
        verifyCmd.CommandText = "SELECT version, updated_at FROM points WHERE id = @id;";
        verifyCmd.Parameters.AddWithValue("id", pointId);
        await using var verifyReader = await verifyCmd.ExecuteReaderAsync();
        Assert.True(await verifyReader.ReadAsync());
        var newVersion = verifyReader.GetInt32(0);
        var newUpdatedAt = verifyReader.GetDateTime(1);
        await verifyReader.CloseAsync();

        Assert.Equal(initialVersion + 1, newVersion);
        Assert.True(newUpdatedAt >= initialUpdatedAt);

        // Cleanup
        await using var deleteCmd = conn.CreateCommand();
        deleteCmd.CommandText = "DELETE FROM points WHERE id = @id;";
        deleteCmd.Parameters.AddWithValue("id", pointId);
        await deleteCmd.ExecuteNonQueryAsync();
    }
}
