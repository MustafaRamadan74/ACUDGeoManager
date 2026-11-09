using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class UndoRedoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private readonly WebApplicationFactory<Program> _factory;
    private static readonly JsonElement EmptyJson = JsonDocument.Parse("{}").RootElement;

    public UndoRedoTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateTestUserClientAsync(string role = "ADMIN", string prefix = "undo_usr")
    {
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"{prefix}_{suffix}";
        var email = $"{username}@acud.gov.eg";

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"
            INSERT INTO users (id, username, email, password_hash, role, is_active, created_at)
            VALUES (@id, @username, @email, 'dummy_hash', @role::user_role, true, now())
            ON CONFLICT DO NOTHING;
            INSERT INTO user_network_access (user_id, network_id)
            VALUES (@id, @networkId)
            ON CONFLICT DO NOTHING;
        ", new { id = userId, username, email, role, networkId = _waterNetworkId });

        var tokenService = _factory.Services.GetRequiredService<UtilityNetwork.Core.Services.ITokenService>();
        var userRole = Enum.Parse<UtilityNetwork.Core.Entities.UserRole>(role, true);
        var user = new UtilityNetwork.Core.Entities.User
        {
            Id = userId,
            Username = username,
            Email = email,
            Role = userRole,
            IsActive = true
        };
        var (token, _) = tokenService.GenerateAccessToken(user, new[] { _waterNetworkId });

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> CreateEditorClientAsync(string prefix = "undo_ed")
    {
        return await CreateTestUserClientAsync("EDITOR", prefix);
    }

    [Fact]
    public async Task CreateFeature_UndoDeletes_AndRedoRestores()
    {
        var client = await CreateTestUserClientAsync("ADMIN", "crt_ad");

        // 1. Create a point feature
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Undo Test Valve 1",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.751,
            Y: 30.011,
            IsOpen: true);

        var createRes = await client.PostAsJsonAsync("/api/v1/points", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(created);

        // 2. Stack check: canUndo must be true
        var stackRes = await client.GetAsync("/api/v1/history/stack");
        Assert.Equal(HttpStatusCode.OK, stackRes.StatusCode);
        var stack = await stackRes.Content.ReadFromJsonAsync<UndoRedoStackDto>();
        Assert.NotNull(stack);
        Assert.True(stack.CanUndo);

        // 3. Undo: must delete the point
        var undoRes = await client.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, undoRes.StatusCode);
        var undoResult = await undoRes.Content.ReadFromJsonAsync<UndoResultDto>();
        Assert.NotNull(undoResult);
        Assert.Equal("UNDONE", undoResult.Status);

        // Verify point does not exist
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = created.Id });
            Assert.False(exists, "Point should have been deleted by Undo.");
        }

        // 4. Redo: must restore the point
        var redoRes = await client.PostAsync("/api/v1/history/redo", null);
        Assert.Equal(HttpStatusCode.OK, redoRes.StatusCode);
        var redoResult = await redoRes.Content.ReadFromJsonAsync<RedoResultDto>();
        Assert.NotNull(redoResult);
        Assert.Equal("APPLIED", redoResult.Status);

        // Verify point exists again with original attributes
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var restoredName = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT name FROM points WHERE id = @id;", new { id = created.Id });
            Assert.Equal("Undo Test Valve 1", restoredName);
        }
    }

    [Fact]
    public async Task UpdateFeature_UndoRestoresOldValues_AndRedoReappliesNewValues()
    {
        var client = await CreateTestUserClientAsync("ADMIN", "upd_ad");

        // 1. Create a point
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Original Valve Name",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.752,
            Y: 30.012,
            IsOpen: true);

        var createRes = await client.PostAsJsonAsync("/api/v1/points", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(created);

        // 2. Update the point
        var updateReq = new UpdatePointRequest(
            Name: "Updated Valve Name",
            Status: "INACTIVE",
            Attributes: null,
            X: null,
            Y: null,
            IsOpen: false,
            Version: created.Version);

        var updateRes = await client.PatchAsJsonAsync($"/api/v1/points/{created.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 3. Undo: restores "Original Valve Name" and "ACTIVE"
        var undoRes = await client.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, undoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var row = await conn.QuerySingleAsync<(string Name, string Status, bool? IsOpen)>(
                "SELECT name, status::text, is_open FROM points WHERE id = @id;", new { id = created.Id });
            Assert.Equal("Original Valve Name", row.Name);
            Assert.Equal("ACTIVE", row.Status);
            Assert.True(row.IsOpen);
        }

        // 4. Redo: reapplies "Updated Valve Name" and "INACTIVE"
        var redoRes = await client.PostAsync("/api/v1/history/redo", null);
        Assert.Equal(HttpStatusCode.OK, redoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var row = await conn.QuerySingleAsync<(string Name, string Status, bool? IsOpen)>(
                "SELECT name, status::text, is_open FROM points WHERE id = @id;", new { id = created.Id });
            Assert.Equal("Updated Valve Name", row.Name);
            Assert.Equal("INACTIVE", row.Status);
            Assert.False(row.IsOpen);
        }
    }

    [Fact]
    public async Task DeleteFeature_UndoRestoresFeature_AndRedoReDeletes()
    {
        var client = await CreateTestUserClientAsync("ADMIN", "del_ad");

        // 1. Create a point
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Valve To Be Deleted",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.753,
            Y: 30.013,
            IsOpen: true);

        var createRes = await client.PostAsJsonAsync("/api/v1/points", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(created);

        // 2. Delete the point
        var delRes = await client.DeleteAsync($"/api/v1/points/{created.Id}?version={created.Version}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

        // 3. Undo: point should be restored
        var undoRes = await client.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, undoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var restored = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT name FROM points WHERE id = @id;", new { id = created.Id });
            Assert.Equal("Valve To Be Deleted", restored);
        }

        // 4. Redo: point should be re-deleted
        var redoRes = await client.PostAsync("/api/v1/history/redo", null);
        Assert.Equal(HttpStatusCode.OK, redoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = created.Id });
            Assert.False(exists, "Point should be re-deleted after redo.");
        }
    }

    [Fact]
    public async Task TwoUsers_HaveIndependentStacks()
    {
        var adminClient = await CreateTestUserClientAsync("ADMIN", "independent_ad");
        var editorClient = await CreateEditorClientAsync("independent_ed");

        // Admin creates a point
        var p1Req = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Admin Exclusive Valve",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.754,
            Y: 30.014,
            IsOpen: true);
        var p1Res = await adminClient.PostAsJsonAsync("/api/v1/points", p1Req);
        var p1 = await p1Res.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(p1);

        // Editor creates a point
        var p2Req = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Editor Exclusive Valve",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.755,
            Y: 30.015,
            IsOpen: true);
        var p2Res = await editorClient.PostAsJsonAsync("/api/v1/points", p2Req);
        var p2 = await p2Res.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(p2);

        // Editor undos: only Editor's point (p2) is undone
        var editorUndoRes = await editorClient.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, editorUndoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var p1Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p1.Id });
            var p2Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p2.Id });

            Assert.True(p1Exists, "Admin's point must NOT be affected by Editor's undo.");
            Assert.False(p2Exists, "Editor's point must be undone.");
        }

        // Admin undos: now Admin's point (p1) is undone
        var adminUndoRes = await adminClient.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, adminUndoRes.StatusCode);

        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var p1Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p1.Id });
            Assert.False(p1Exists, "Admin's point is now undone.");
        }
    }

    [Fact]
    public async Task ConflictRule_DetectsVersionMismatch_Returns409AndDoesNothing()
    {
        var adminClient = await CreateTestUserClientAsync("ADMIN", "conflict_ad");
        var editorClient = await CreateEditorClientAsync("conflict_ed");

        // 1. Admin creates a valve
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Conflict Valve Test",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.756,
            Y: 30.016,
            IsOpen: true);
        var createRes = await adminClient.PostAsJsonAsync("/api/v1/points", createReq);
        var created = await createRes.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(created);

        // 2. Editor modifies the same valve (bumping its version)
        var updateReq = new UpdatePointRequest(
            Name: "Editor Interrupted Name",
            Status: "INACTIVE",
            Attributes: null,
            X: null,
            Y: null,
            IsOpen: false,
            Version: created.Version);
        var updateRes = await editorClient.PatchAsJsonAsync($"/api/v1/points/{created.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        // 3. Admin attempts to undo creation -> must detect conflict and return 409!
        var undoRes = await adminClient.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.Conflict, undoRes.StatusCode);

        // 4. Verify feature was NOT modified or deleted
        await using (var conn = new NpgsqlConnection(ConnectionString))
        {
            await conn.OpenAsync();
            var currentName = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT name FROM points WHERE id = @id;", new { id = created.Id });
            Assert.Equal("Editor Interrupted Name", currentName);
        }
    }

    [Fact]
    public async Task NewEdit_ClearsRedoStackForUser()
    {
        var client = await CreateTestUserClientAsync("ADMIN", "redo_ad");

        // 1. Create a point
        var createReq = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Redo Clear Valve 1",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.757,
            Y: 30.017,
            IsOpen: true);
        var createRes = await client.PostAsJsonAsync("/api/v1/points", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        // 2. Undo creation
        var undoRes = await client.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, undoRes.StatusCode);

        // 3. Stack check: canRedo must be true
        var stack1 = await client.GetFromJsonAsync<UndoRedoStackDto>("/api/v1/history/stack");
        Assert.NotNull(stack1);
        Assert.True(stack1.CanRedo);
        Assert.NotEmpty(stack1.RedoStack);

        // 4. Create another point (normal edit)
        var createReq2 = new CreatePointRequest(
            NetworkId: _waterNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Redo Clear Valve 2",
            Status: "ACTIVE",
            Attributes: EmptyJson,
            X: 31.758,
            Y: 30.018,
            IsOpen: true);
        var createRes2 = await client.PostAsJsonAsync("/api/v1/points", createReq2);
        Assert.Equal(HttpStatusCode.Created, createRes2.StatusCode);

        // 5. Stack check: redo stack must now be empty!
        var stack2 = await client.GetFromJsonAsync<UndoRedoStackDto>("/api/v1/history/stack");
        Assert.NotNull(stack2);
        Assert.False(stack2.CanRedo, "Redo stack must be cleared after a new edit.");
        Assert.Empty(stack2.RedoStack);
    }

    [Fact]
    public async Task MultiFeatureChangeSet_UndoInvertsAllActionsAtomically()
    {
        var client = await CreateTestUserClientAsync("ADMIN", "multi_ad");

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        var multiUserId = await conn.QuerySingleAsync<Guid>(
            "SELECT id FROM users WHERE username LIKE 'multi_ad_%' ORDER BY created_at DESC LIMIT 1;");

        using var scope = _factory.Services.CreateScope();
        var uowFactory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        var userContext = new UserContext(multiUserId, "multi_ad", "ADMIN", "multi-feat-trace");
        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();

        // 1. Perform multi-feature write inside a single Unit of Work
        await using (var uow = await uowFactory.BeginWriteAsync(userContext, description: "Multi Feature Batch Test"))
        {
            const string insertSql = @"
                INSERT INTO points (id, network_id, feature_type_code, name, geom)
                VALUES (@id, @net, 'VALVE', @name, ST_SetSRID(ST_MakePoint(@x, @y), 4326));";

            await uow.Connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { id = p1Id, net = _waterNetworkId, name = "Batch Valve 1", x = 31.759, y = 30.019 },
                transaction: uow.Transaction));

            await uow.Connection.ExecuteAsync(new CommandDefinition(
                insertSql,
                new { id = p2Id, net = _waterNetworkId, name = "Batch Valve 2", x = 31.760, y = 30.020 },
                transaction: uow.Transaction));

            await uow.CommitAsync();
        }

        // Verify both exist
        var p1Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p1Id });
        var p2Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p2Id });
        Assert.True(p1Exists && p2Exists);

        // 2. Undo via API
        var undoRes = await client.PostAsync("/api/v1/history/undo", null);
        Assert.Equal(HttpStatusCode.OK, undoRes.StatusCode);
        var undoResult = await undoRes.Content.ReadFromJsonAsync<UndoResultDto>();
        Assert.NotNull(undoResult);
        Assert.Equal(2, undoResult.RevertedActionsCount);

        // 3. Verify both deleted atomically
        p1Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p1Id });
        p2Exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM points WHERE id = @id);", new { id = p2Id });
        Assert.False(p1Exists || p2Exists, "Both features must be deleted by undoing multi-feature change set.");

        // 4. Verify audit row was written with reason = 'UNDO'
        var undoReasonExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM audit_log WHERE record_id = @id AND reason = 'UNDO'::audit_reason);",
            new { id = p1Id });
        Assert.True(undoReasonExists, "Audit log must record action with reason = 'UNDO'.");
    }
}
