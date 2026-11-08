using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using UtilityNetwork.Core.Exceptions;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Core.Services;

namespace UtilityNetwork.Tests;

public sealed class AuthorizationMatrixTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthorizationMatrixTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost:5000")
        });
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role, IEnumerable<Guid>? accessibleNetworks = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Name, $"user_{role}"),
            new(ClaimTypes.Role, role)
        };

        if (accessibleNetworks != null)
        {
            claims.AddRange(accessibleNetworks.Select(nid => new Claim("network_id", nid.ToString())));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task CombinedPermissionCheck_AuthorizationMatrix_EnforcesMostRestrictiveRule()
    {
        using var scope = _factory.Services.CreateScope();
        var permService = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        var permRepo = scope.ServiceProvider.GetRequiredService<ILayerPermissionRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var testNetwork1 = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        var testNetwork2 = Guid.Parse("a0000000-0000-0000-0000-000000000002");
        var unassignedNetwork = Guid.NewGuid();

        var editorId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Create actual user records in DB for foreign key constraint
        await userRepo.CreateAsync(new UtilityNetwork.Core.Entities.User
        {
            Id = editorId,
            Username = $"ed_{Guid.NewGuid():N}"[..18],
            Email = $"ed_{Guid.NewGuid():N}"[..14] + "@acud.gov.eg",
            PasswordHash = "hash",
            Role = UtilityNetwork.Core.Entities.UserRole.Editor,
            Department = "Sewer Networks"
        });

        await userRepo.CreateAsync(new UtilityNetwork.Core.Entities.User
        {
            Id = viewerId,
            Username = $"vw_{Guid.NewGuid():N}"[..18],
            Email = $"vw_{Guid.NewGuid():N}"[..14] + "@acud.gov.eg",
            PasswordHash = "hash",
            Role = UtilityNetwork.Core.Entities.UserRole.Viewer,
            Department = "Inspection Directorate"
        });

        await userRepo.CreateAsync(new UtilityNetwork.Core.Entities.User
        {
            Id = adminId,
            Username = $"ad_{Guid.NewGuid():N}"[..18],
            Email = $"ad_{Guid.NewGuid():N}"[..14] + "@acud.gov.eg",
            PasswordHash = "hash",
            Role = UtilityNetwork.Core.Entities.UserRole.Admin,
            Department = "IT Operations"
        });

        // Configure Layer Permissions for Editor:
        // - VALVE: View only (CanRead=true, CanCreate=false, CanUpdate=false, CanDelete=false)
        // - HYDRANT: Full edit (CanRead=true, CanCreate=true, CanUpdate=true, CanDelete=true)
        // - PIPE: Denied entirely (CanRead=false, CanCreate=false, CanUpdate=false, CanDelete=false)
        await permRepo.SetUserLayerPermissionsAsync(editorId,
        [
            new UpdateLayerPermissionRequest("VALVE", CanRead: true, CanCreate: false, CanUpdate: false, CanDelete: false),
            new UpdateLayerPermissionRequest("HYDRANT", CanRead: true, CanCreate: true, CanUpdate: true, CanDelete: true),
            new UpdateLayerPermissionRequest("PIPE", CanRead: false, CanCreate: false, CanUpdate: false, CanDelete: false)
        ]);

        // Configure Layer Permissions for Viewer:
        // Give viewer CanCreate=true on VALVE in table, but role ceiling MUST override and forbid!
        await permRepo.SetUserLayerPermissionsAsync(viewerId,
        [
            new UpdateLayerPermissionRequest("VALVE", CanRead: true, CanCreate: true, CanUpdate: true, CanDelete: true)
        ]);

        var adminUser = CreatePrincipal(adminId, "ADMIN");
        var editorUser = CreatePrincipal(editorId, "EDITOR", [testNetwork1, testNetwork2]);
        var viewerUser = CreatePrincipal(viewerId, "VIEWER", [testNetwork1]);

        // 1. Admin has full access across all layers, workspaces, and actions
        Assert.True(await permService.HasLayerPermissionAsync(adminUser, testNetwork1, "VALVE", LayerAction.Create));
        Assert.True(await permService.HasLayerPermissionAsync(adminUser, unassignedNetwork, "VALVE", LayerAction.Delete));

        // 2. Editor on unassigned network is blocked by Workspace Scope
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, unassignedNetwork, "HYDRANT", LayerAction.Read));
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, unassignedNetwork, "HYDRANT", LayerAction.Create));

        // 3. User with view-only on one layer cannot edit it but can edit another:
        // - On VALVE (view-only): Read is allowed, but Create/Update/Delete are DENIED
        Assert.True(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "VALVE", LayerAction.Read));
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "VALVE", LayerAction.Create));
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "VALVE", LayerAction.Update));
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "VALVE", LayerAction.Delete));

        // - On HYDRANT (full-edit): Read AND Create are ALLOWED
        Assert.True(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "HYDRANT", LayerAction.Read));
        Assert.True(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "HYDRANT", LayerAction.Create));
        Assert.True(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "HYDRANT", LayerAction.Update));
        Assert.True(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "HYDRANT", LayerAction.Delete));

        // - On PIPE (no-read): Read is DENIED
        Assert.False(await permService.HasLayerPermissionAsync(editorUser, testNetwork1, "PIPE", LayerAction.Read));

        // 4. Role Ceiling: Viewer cannot Create even if granted CanCreate in table
        Assert.True(await permService.HasLayerPermissionAsync(viewerUser, testNetwork1, "VALVE", LayerAction.Read));
        Assert.False(await permService.HasLayerPermissionAsync(viewerUser, testNetwork1, "VALVE", LayerAction.Create));
        Assert.False(await permService.HasLayerPermissionAsync(viewerUser, testNetwork1, "VALVE", LayerAction.Update));
        Assert.False(await permService.HasLayerPermissionAsync(viewerUser, testNetwork1, "VALVE", LayerAction.Delete));
    }

    [Fact]
    public async Task AdminUser_CanGetAndSetUserLayerPermissions_ViaApi()
    {
        // Login as admin
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

        // Create a new test user with Department
        var createRequest = new CreateUserRequest(
            Username: $"perm_user_{Guid.NewGuid():N}"[..18],
            Email: $"perm_{Guid.NewGuid():N}"[..14] + "@acud.gov.eg",
            Password: "TestPassword123!",
            Role: "EDITOR",
            NetworkIds: [Guid.Parse("a0000000-0000-0000-0000-000000000001")],
            Department: "Water Utility Directorate"
        );

        var createResponse = await _client.PostAsJsonAsync("/api/v1/users", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var createdUser = await createResponse.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(createdUser);
        Assert.Equal("Water Utility Directorate", createdUser.Department);

        // Fetch layer permissions for this user
        var getPermsResponse = await _client.GetAsync($"/api/v1/users/{createdUser.Id}/permissions");
        Assert.Equal(HttpStatusCode.OK, getPermsResponse.StatusCode);
        var permsList = await getPermsResponse.Content.ReadFromJsonAsync<List<LayerPermissionDto>>();
        Assert.NotNull(permsList);
        Assert.NotEmpty(permsList);

        // Update layer permissions: set VALVE to view-only and HYDRANT to edit
        var updatePermsRequest = new UserLayerPermissionsUpdateRequest(
        [
            new UpdateLayerPermissionRequest("VALVE", CanRead: true, CanCreate: false, CanUpdate: false, CanDelete: false),
            new UpdateLayerPermissionRequest("HYDRANT", CanRead: true, CanCreate: true, CanUpdate: true, CanDelete: true)
        ]);

        var setPermsResponse = await _client.PutAsJsonAsync($"/api/v1/users/{createdUser.Id}/permissions", updatePermsRequest);
        Assert.Equal(HttpStatusCode.NoContent, setPermsResponse.StatusCode);

        // Verify updated permissions
        var verifyResponse = await _client.GetAsync($"/api/v1/users/{createdUser.Id}/permissions");
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var updatedPerms = await verifyResponse.Content.ReadFromJsonAsync<List<LayerPermissionDto>>();
        Assert.NotNull(updatedPerms);

        var valvePerm = updatedPerms.FirstOrDefault(p => p.FeatureTypeCode == "VALVE");
        Assert.NotNull(valvePerm);
        Assert.True(valvePerm.CanRead);
        Assert.False(valvePerm.CanCreate);
        Assert.False(valvePerm.CanUpdate);
        Assert.False(valvePerm.CanDelete);

        var hydrantPerm = updatedPerms.FirstOrDefault(p => p.FeatureTypeCode == "HYDRANT");
        Assert.NotNull(hydrantPerm);
        Assert.True(hydrantPerm.CanRead);
        Assert.True(hydrantPerm.CanCreate);
    }

    [Fact]
    public async Task FeatureEndpoints_EnforceLayerPermissions_ViewOnlyLayerReturns403_WhileEditableLayerSucceeds()
    {
        // 1. Admin logs in and creates an editor user
        var adminLoginResp = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        var adminLogin = await adminLoginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminLogin);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminLogin.AccessToken);

        var testNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
        var editorUsername = $"ed_matrix_{Guid.NewGuid():N}"[..18];
        var editorPassword = "Editor@Password123!";

        var createEditorReq = new CreateUserRequest(
            Username: editorUsername,
            Email: $"{editorUsername}@acud.gov.eg",
            Password: editorPassword,
            Role: "EDITOR",
            NetworkIds: [testNetworkId],
            Department: "Water Distribution"
        );

        var createResp = await _client.PostAsJsonAsync("/api/v1/users", createEditorReq);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var createdEditor = await createResp.Content.ReadFromJsonAsync<UserResponse>();
        Assert.NotNull(createdEditor);

        // 2. Set Layer Permissions for this editor:
        // - VALVE: View only (CanCreate=false)
        // - HYDRANT: Editable (CanCreate=true)
        var updatePerms = new UserLayerPermissionsUpdateRequest(
        [
            new UpdateLayerPermissionRequest("VALVE", CanRead: true, CanCreate: false, CanUpdate: false, CanDelete: false),
            new UpdateLayerPermissionRequest("HYDRANT", CanRead: true, CanCreate: true, CanUpdate: true, CanDelete: true)
        ]);

        var setPermsResp = await _client.PutAsJsonAsync($"/api/v1/users/{createdEditor.Id}/permissions", updatePerms);
        Assert.Equal(HttpStatusCode.NoContent, setPermsResp.StatusCode);

        // 3. Now login as the editor user
        var editorClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost:5000")
        });

        var editorLoginResp = await editorClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(editorUsername, editorPassword));
        Assert.Equal(HttpStatusCode.OK, editorLoginResp.StatusCode);
        var editorLogin = await editorLoginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(editorLogin);

        editorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", editorLogin.AccessToken);

        // 4. Attempt to create a VALVE (which is view-only for this user) -> MUST return 403 Forbidden!
        var valveCreateReq = new CreatePointRequest(
            NetworkId: testNetworkId,
            FeatureTypeCode: "VALVE",
            Name: "Unauthorized Valve Test",
            Status: "ACTIVE",
            Attributes: System.Text.Json.JsonDocument.Parse("{\"valve_type\":\"GATE\"}").RootElement,
            X: 31.748,
            Y: 30.015,
            IsOpen: true
        );

        var valveResp = await editorClient.PostAsJsonAsync("/api/v1/points", valveCreateReq);
        Assert.Equal(HttpStatusCode.Forbidden, valveResp.StatusCode);

        // 5. Attempt to create a HYDRANT (which is editable for this user) -> MUST succeed (201 Created)!
        var hydrantCreateReq = new CreatePointRequest(
            NetworkId: testNetworkId,
            FeatureTypeCode: "HYDRANT",
            Name: "Authorized Hydrant Test",
            Status: "ACTIVE",
            Attributes: System.Text.Json.JsonDocument.Parse("{\"flow_rate_lps\": 25.5, \"outlet_diameter_mm\": 65}").RootElement,
            X: 31.749,
            Y: 30.016,
            IsOpen: null
        );

        var hydrantResp = await editorClient.PostAsJsonAsync("/api/v1/points", hydrantCreateReq);
        Assert.Equal(HttpStatusCode.Created, hydrantResp.StatusCode);
        var createdHydrant = await hydrantResp.Content.ReadFromJsonAsync<PointFeatureDto>();
        Assert.NotNull(createdHydrant);
        Assert.Equal("Authorized Hydrant Test", createdHydrant.Name);
    }
}
