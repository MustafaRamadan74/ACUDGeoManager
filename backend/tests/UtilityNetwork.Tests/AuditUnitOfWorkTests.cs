using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UtilityNetwork.Core.Data;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class AuditUnitOfWorkTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private readonly WebApplicationFactory<Program> _factory;

    public AuditUnitOfWorkTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnitOfWork_InsertUpdateDelete_CreatesAuditRowsWithContextAndChangeSet()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var adminUser = await conn.QuerySingleAsync<Guid>("SELECT id FROM users WHERE username = 'admin';");

        using var scope = _factory.Services.CreateScope();
        var uowFactory = scope.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>();

        var testUserId = adminUser;
        var userContext = new UserContext(
            UserId: testUserId,
            Username: "admin",
            Role: "ADMIN",
            RequestId: "req-trace-999");

        var pointId = Guid.NewGuid();
        Guid changeSetId;

        // 1. Execute Write operations inside Unit of Work
        await using (var uow = await uowFactory.BeginWriteAsync(userContext, description: "Phase 4 Audit Test Operations"))
        {
            changeSetId = uow.ChangeSetId;

            // INSERT
            const string insertSql = @"
                INSERT INTO points (id, network_id, feature_type_code, name, geom)
                VALUES (@pointId, @net, 'VALVE', 'Audit Test Valve', ST_SetSRID(ST_MakePoint(31.62, 30.02), 4326));";
            await uow.Connection.ExecuteAsync(new CommandDefinition(insertSql, new { pointId, net = _waterNetworkId }, transaction: uow.Transaction));

            // UPDATE
            const string updateSql = @"
                UPDATE points SET name = 'Audit Test Valve Renamed' WHERE id = @pointId;";
            await uow.Connection.ExecuteAsync(new CommandDefinition(updateSql, new { pointId }, transaction: uow.Transaction));

            // DELETE
            const string deleteSql = @"
                DELETE FROM points WHERE id = @pointId;";
            await uow.Connection.ExecuteAsync(new CommandDefinition(deleteSql, new { pointId }, transaction: uow.Transaction));

            await uow.CommitAsync();
        }

        // 2. Verify Audit Log Rows in Database
        const string queryAuditSql = @"
            SELECT action::text AS Action, 
                   old_data::text AS OldData, 
                   new_data::text AS NewData, 
                   user_id AS UserId, 
                   username AS Username, 
                   role AS Role, 
                   request_id AS RequestId, 
                   change_set_id AS ChangeSetId
            FROM audit_log
            WHERE record_id = @pointId
            ORDER BY id ASC;";

        var auditRows = (await conn.QueryAsync<TestAuditDetailRow>(queryAuditSql, new { pointId })).ToList();
        Assert.Equal(3, auditRows.Count);

        // Verify INSERT row
        var insertRow = auditRows[0];
        Assert.Equal("INSERT", insertRow.Action);
        Assert.Null(insertRow.OldData);
        Assert.NotNull(insertRow.NewData);
        Assert.Contains("Audit Test Valve", insertRow.NewData);
        Assert.Equal(testUserId, insertRow.UserId);
        Assert.Equal("admin", insertRow.Username);
        Assert.Equal("ADMIN", insertRow.Role);
        Assert.Equal("req-trace-999", insertRow.RequestId);
        Assert.Equal(changeSetId, insertRow.ChangeSetId);

        // Verify UPDATE row
        var updateRow = auditRows[1];
        Assert.Equal("UPDATE", updateRow.Action);
        Assert.NotNull(updateRow.OldData);
        Assert.NotNull(updateRow.NewData);
        Assert.Contains("Audit Test Valve", updateRow.OldData);
        Assert.Contains("Audit Test Valve Renamed", updateRow.NewData);

        // Verify DELETE row
        var deleteRow = auditRows[2];
        Assert.Equal("DELETE", deleteRow.Action);
        Assert.NotNull(deleteRow.OldData);
        Assert.Null(deleteRow.NewData);
        Assert.Contains("Audit Test Valve Renamed", deleteRow.OldData);

        // Verify change_sets record exists
        var changeSetStatus = await conn.ExecuteScalarAsync<string>(
            "SELECT status::text FROM change_sets WHERE id = @changeSetId;", new { changeSetId });
        Assert.Equal("APPLIED", changeSetStatus);
    }

    [Fact]
    public async Task RawSqlChange_OutsideApi_RecordsSystemInAuditLog()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();

        var pointId = Guid.NewGuid();

        // Direct SQL insert without setting app.* variables (simulating direct DBA or external tool)
        const string insertSql = @"
            INSERT INTO points (id, network_id, feature_type_code, name, geom)
            VALUES (@pointId, @net, 'VALVE', 'Direct SQL Valve', ST_SetSRID(ST_MakePoint(31.63, 30.03), 4326));";
        await conn.ExecuteAsync(insertSql, new { pointId, net = _waterNetworkId });

        // Query audit log
        const string queryAuditSql = @"
            SELECT username AS Username, 
                   role AS Role, 
                   action::text AS Action, 
                   change_set_id AS ChangeSetId
            FROM audit_log
            WHERE record_id = @pointId;";

        var auditRow = await conn.QuerySingleOrDefaultAsync<TestAuditSummaryRow>(queryAuditSql, new { pointId });
        Assert.NotNull(auditRow);
        Assert.Equal("system", auditRow.Username);
        Assert.Equal("SYSTEM", auditRow.Role);
        Assert.Equal("INSERT", auditRow.Action);
        Assert.Null(auditRow.ChangeSetId);

        // Cleanup
        await conn.ExecuteAsync("DELETE FROM points WHERE id = @pointId;", new { pointId });
    }

    [Fact]
    public async Task HistoryApi_RoleIsolation_WorksCorrectly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // 1. Login as Admin
        var adminLoginRes = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        var adminAuth = await adminLoginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminAuth);

        // 2. Query history as Admin -> should return 200 and items list
        using var adminReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/history?page=1&pageSize=10");
        adminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        var adminHistoryRes = await client.SendAsync(adminReq);

        Assert.Equal(HttpStatusCode.OK, adminHistoryRes.StatusCode);
        var pagedResult = await adminHistoryRes.Content.ReadFromJsonAsync<PagedResult<AuditLogEntry>>();
        Assert.NotNull(pagedResult);
        Assert.True(pagedResult.TotalCount > 0);
    }

    private sealed record TestAuditDetailRow(
        string Action,
        string? OldData,
        string? NewData,
        Guid? UserId,
        string Username,
        string Role,
        string? RequestId,
        Guid? ChangeSetId);

    private sealed record TestAuditSummaryRow(
        string Username,
        string Role,
        string Action,
        Guid? ChangeSetId);
}
