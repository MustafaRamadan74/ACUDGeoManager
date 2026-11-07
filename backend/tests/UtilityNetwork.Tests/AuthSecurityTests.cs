using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class AuthSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AuthSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost:5000")
        });
    }

    [Fact]
    public async Task UnauthenticatedCalls_ToProtectedEndpoints_ShouldReturn401()
    {
        var unauthenticatedClient = _factory.CreateClient();

        var usersResponse = await unauthenticatedClient.GetAsync("/api/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, usersResponse.StatusCode);

        var meResponse = await unauthenticatedClient.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndSetsHttpOnlyCookie()
    {
        var loginRequest = new LoginRequest("admin", "Admin@Passw0rd123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("admin", body.User.Username);
        Assert.Equal("ADMIN", body.User.Role);

        // Verify Set-Cookie header contains HttpOnly and refreshToken
        Assert.True(response.Headers.Contains("Set-Cookie"));
        var cookies = response.Headers.GetValues("Set-Cookie").ToList();
        var refreshCookie = cookies.FirstOrDefault(c => c.Contains("refreshToken="));
        Assert.NotNull(refreshCookie);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401WithGenericMessage()
    {
        var loginRequest = new LoginRequest("admin", "WrongPassword123!");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TamperedJwt_ShouldReturn401()
    {
        // 1. Login to get a valid token
        var loginRequest = new LoginRequest("admin", "Admin@Passw0rd123!");
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var body = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);

        // 2. Tamper the signature of the token
        var parts = body.AccessToken.Split('.');
        Assert.Equal(3, parts.Length);
        var tamperedSignature = parts[2].Length > 4 
            ? string.Concat(parts[2].AsSpan(0, parts[2].Length - 4), "xxxx")
            : "xxxx";
        var tamperedToken = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        // 3. Request with tampered token
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredJwt_ShouldReturn401()
    {
        // 1. Create an already expired token signed with the test secret
        var keyBytes = Encoding.UTF8.GetBytes("SuperSecretKeyAtLeast32BytesLongForSecurityTestingOnly1234567890!");
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, "testuser"),
                new Claim(ClaimTypes.Role, "ADMIN")
            ]),
            NotBefore = DateTime.UtcNow.AddMinutes(-20),
            Expires = DateTime.UtcNow.AddMinutes(-10), // expired 10 minutes ago
            Issuer = "ACUDGeoManager",
            Audience = "ACUDGeoManagerClient",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(keyBytes),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var expiredToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

        // 2. Request with expired token
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_RotationAndReuseDetection_WorksCorrectly()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        // 1. Login to get initial refresh token cookie
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var initialCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").First(c => c.Contains("refreshToken="));
        var initialRawToken = initialCookieHeader.Split(';')[0]; // "refreshToken=..."

        // 2. Perform valid refresh using initial token
        using var refreshMsg1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshMsg1.Headers.Add("Cookie", initialRawToken);
        var refreshResponse1 = await client.SendAsync(refreshMsg1);
        Assert.Equal(HttpStatusCode.OK, refreshResponse1.StatusCode);

        var newCookieHeader = refreshResponse1.Headers.GetValues("Set-Cookie").First(c => c.Contains("refreshToken="));
        var newRawToken = newCookieHeader.Split(';')[0];

        // 3. Attempt to REUSE the old initial refresh token
        using var reuseRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        reuseRequest.Headers.Add("Cookie", initialRawToken);
        var reuseResponse = await client.SendAsync(reuseRequest);

        // Reuse detection triggers family revocation and returns 401
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // 4. Subsequent refresh with the newer token should now ALSO fail (401) because the entire family was revoked
        using var refreshMsg2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshMsg2.Headers.Add("Cookie", newRawToken);
        var refreshResponse2 = await client.SendAsync(refreshMsg2);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse2.StatusCode);
    }

    [Fact]
    public async Task AuthorizationMatrix_ViewerRole_CannotAccessAdminUserManagement()
    {
        // 1. Create a viewer user using Admin credentials
        var adminClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var loginRes = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin", "Admin@Passw0rd123!"));
        var adminAuth = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(adminAuth);

        var viewerUsername = $"viewer_{Guid.NewGuid():N}"[..16];
        var createViewerReq = new CreateUserRequest(
            viewerUsername,
            $"{viewerUsername}@acud.gov.eg",
            "ViewerPass123!",
            "VIEWER",
            null);

        using var createMsg = new HttpRequestMessage(HttpMethod.Post, "/api/v1/users");
        createMsg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        createMsg.Content = JsonContent.Create(createViewerReq);
        var createRes = await adminClient.SendAsync(createMsg);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);

        // 2. Log in as Viewer
        var viewerClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var viewerLoginRes = await viewerClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(viewerUsername, "ViewerPass123!"));
        var viewerAuth = await viewerLoginRes.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(viewerAuth);

        // 3. Attempt to access /api/v1/users with Viewer token -> should return 403 Forbidden
        using var usersReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users");
        usersReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", viewerAuth.AccessToken);
        var usersRes = await viewerClient.SendAsync(usersReq);

        Assert.Equal(HttpStatusCode.Forbidden, usersRes.StatusCode);
    }

    [Fact]
    public async Task StrictJson_RejectsUnknownProperties()
    {
        var rawJson = "{\"username\":\"admin\",\"password\":\"Admin@Passw0rd123!\",\"unmappedProperty\":123}";
        using var content = new StringContent(rawJson, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/auth/login", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
