using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using UtilityNetwork.Core.Models;

namespace UtilityNetwork.Tests;

public sealed class GeneralValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly Guid _urbanNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private readonly Guid _waterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly double[][] ShortPipeCoords =
    [
        [31.750000, 30.020000],
        [31.750001, 30.020001]
    ];
    private readonly WebApplicationFactory<Program> _factory;

    public GeneralValidationTests(WebApplicationFactory<Program> factory)
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
    public async Task RunValidation_UrbanNetwork_DetectsPlantedDefectsAndNamesAssets()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Trigger validation run for Urban Assets
        var runRes = await client.PostAsync($"/api/v1/networks/{_urbanNetworkId}/validation/run", null);
        Assert.Equal(HttpStatusCode.OK, runRes.StatusCode);

        var run = await runRes.Content.ReadFromJsonAsync<ValidationRunDto>();
        Assert.NotNull(run);
        Assert.True(run.Summary.TotalIssues >= 4, "Must detect at least the 4 planted urban defects.");
        Assert.True(run.Summary.DurationMs >= 0);

        // 2. Query issues for this run
        var issuesRes = await client.GetAsync($"/api/v1/validation/issues?runId={run.Id}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, issuesRes.StatusCode);

        var pageResult = await issuesRes.Content.ReadFromJsonAsync<ValidationIssuePageResult>();
        Assert.NotNull(pageResult);
        Assert.NotEmpty(pageResult.Items);

        // 3. Verify each planted urban defect is detected and issue message names the asset
        var issues = pageResult.Items;

        // Error 1: Near-duplicate buildings BLD-ERR-01
        var bldDupIssue = issues.FirstOrDefault(i => i.Message.Contains("BLD-ERR-01"));
        Assert.NotNull(bldDupIssue);
        Assert.Contains("BLD-ERR-01", bldDupIssue.Message);

        // Error 2: Duplicate smart poles POLE-ERR-02
        var poleDupIssue = issues.FirstOrDefault(i => i.Message.Contains("POLE-ERR-02"));
        Assert.NotNull(poleDupIssue);
        Assert.Contains("POLE-ERR-02", poleDupIssue.Message);

        // Error 3: Very short road segment ROAD-ERR-03
        var shortRoadIssue = issues.FirstOrDefault(i => i.Message.Contains("ROAD-ERR-03"));
        Assert.NotNull(shortRoadIssue);
        Assert.Contains("ROAD-ERR-03", shortRoadIssue.Message);
        Assert.Equal("RULE_ZERO_LENGTH_LINE", shortRoadIssue.RuleCode);

        // Error 4: Building overlapping road BLD-ERR-04
        var bldRoadIssue = issues.FirstOrDefault(i => i.Message.Contains("BLD-ERR-04"));
        Assert.NotNull(bldRoadIssue);
        Assert.Contains("BLD-ERR-04", bldRoadIssue.Message);
        Assert.Equal("RULE_FORBIDDEN_OVERLAP", bldRoadIssue.RuleCode);
    }

    [Fact]
    public async Task RunValidation_WaterNetwork_DetectsPlantedDefectsAndNamesAssets()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Trigger validation run for Water Network
        var runRes = await client.PostAsync($"/api/v1/networks/{_waterNetworkId}/validation/run", null);
        Assert.Equal(HttpStatusCode.OK, runRes.StatusCode);

        var run = await runRes.Content.ReadFromJsonAsync<ValidationRunDto>();
        Assert.NotNull(run);
        Assert.True(run.Summary.TotalIssues >= 6, "Must detect at least the 6 planted water defects.");

        // 2. Query issues
        var issuesRes = await client.GetAsync($"/api/v1/validation/issues?runId={run.Id}&pageSize=500");
        var pageResult = await issuesRes.Content.ReadFromJsonAsync<ValidationIssuePageResult>();
        Assert.NotNull(pageResult);

        var issues = pageResult.Items;

        // Error 5: Floating valve off-pipe VALVE-ERR-05
        var valveIssue = issues.FirstOrDefault(i => i.Message.Contains("VALVE-ERR-05"));
        Assert.NotNull(valveIssue);
        Assert.Equal("RULE_VALVE_OFF_PIPE", valveIssue.RuleCode);

        // Error 6: Dangling pipe end PIPE-ERR-06
        var danglingIssue = issues.FirstOrDefault(i => i.Message.Contains("PIPE-ERR-06"));
        Assert.NotNull(danglingIssue);
        Assert.Equal("RULE_DANGLING_PIPE", danglingIssue.RuleCode);

        // Error 7: Duplicate hydrants HYD-ERR-07
        var hydDupIssue = issues.FirstOrDefault(i => i.Message.Contains("HYD-ERR-07"));
        Assert.NotNull(hydDupIssue);
        Assert.Equal("RULE_DUPLICATE_FEATURE", hydDupIssue.RuleCode);

        // Error 8: Unknown flow direction PIPE-ERR-08
        var unknownFlowIssue = issues.FirstOrDefault(i => i.Message.Contains("PIPE-ERR-08"));
        Assert.NotNull(unknownFlowIssue);
        Assert.Equal("RULE_MISSING_REQUIRED_ATTR", unknownFlowIssue.RuleCode);

        // Error 9: Reversed flow direction PIPE-ERR-09
        var reversedFlowIssue = issues.FirstOrDefault(i => i.Message.Contains("PIPE-ERR-09"));
        Assert.NotNull(reversedFlowIssue);
        Assert.Equal("RULE_REVERSED_FLOW", reversedFlowIssue.RuleCode);

        // Error 10: Segment with no source PIPE-ERR-10
        var noSourceIssue = issues.FirstOrDefault(i => i.Message.Contains("PIPE-ERR-10"));
        Assert.NotNull(noSourceIssue);
        Assert.Equal("RULE_ISOLATED_NO_SOURCE", noSourceIssue.RuleCode);
    }

    [Fact]
    public async Task IssueStatusWorkflow_TransitionsThroughAcknowledgedAndResolved()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Run validation
        var runRes = await client.PostAsync($"/api/v1/networks/{_waterNetworkId}/validation/run", null);
        var run = await runRes.Content.ReadFromJsonAsync<ValidationRunDto>();
        Assert.NotNull(run);

        // 2. Fetch an open issue
        var issuesRes = await client.GetAsync($"/api/v1/validation/issues?runId={run.Id}&pageSize=1");
        var page = await issuesRes.Content.ReadFromJsonAsync<ValidationIssuePageResult>();
        Assert.NotNull(page);
        var targetIssue = page.Items[0];
        Assert.Equal(IssueStatus.OPEN, targetIssue.Status);

        // 3. Acknowledge issue
        var ackReq = new UpdateIssueStatusRequest(IssueStatus.ACKNOWLEDGED);
        var ackRes = await client.PatchAsJsonAsync($"/api/v1/validation/issues/{targetIssue.Id}/status", ackReq);
        Assert.Equal(HttpStatusCode.OK, ackRes.StatusCode);

        var ackBody = await ackRes.Content.ReadFromJsonAsync<ValidationIssueDto>();
        Assert.NotNull(ackBody);
        Assert.Equal(IssueStatus.ACKNOWLEDGED, ackBody.Status);
        Assert.Equal("admin", ackBody.AcknowledgedBy);
        Assert.NotNull(ackBody.AcknowledgedAt);

        // 4. Resolve issue
        var resReq = new UpdateIssueStatusRequest(IssueStatus.RESOLVED);
        var resRes = await client.PatchAsJsonAsync($"/api/v1/validation/issues/{targetIssue.Id}/status", resReq);
        Assert.Equal(HttpStatusCode.OK, resRes.StatusCode);

        var resBody = await resRes.Content.ReadFromJsonAsync<ValidationIssueDto>();
        Assert.NotNull(resBody);
        Assert.Equal(IssueStatus.RESOLVED, resBody.Status);
        Assert.Equal("admin", resBody.ResolvedBy);
        Assert.NotNull(resBody.ResolvedAt);
    }

    [Fact]
    public async Task FixingAnError_AndReRunningValidation_ClearsItFromNewRun()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var token = await GetAdminTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Create a temporary invalid feature (sub-threshold short line < 1m)
        var createReq = new CreateLineRequest(
            _waterNetworkId,
            "PIPE",
            "Temporary Ultra Short Pipe",
            "ACTIVE",
            JsonDocument.Parse("""{"pressure_bar": 4.0, "installation_year": 2024}""").RootElement,
            ShortPipeCoords,
            "WITH_DIGITIZED",
            150,
            "PVC"
        );

        var createRes = await client.PostAsJsonAsync("/api/v1/lines", createReq);
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var createdLine = await createRes.Content.ReadFromJsonAsync<LineFeatureDto>();
        Assert.NotNull(createdLine);

        try
        {
            // 2. Run validation -> Must detect our temporary short pipe
            var run1Res = await client.PostAsync($"/api/v1/networks/{_waterNetworkId}/validation/run", null);
            var run1 = await run1Res.Content.ReadFromJsonAsync<ValidationRunDto>();
            Assert.NotNull(run1);

            var issues1Res = await client.GetAsync($"/api/v1/validation/issues?runId={run1.Id}&ruleCode=RULE_ZERO_LENGTH_LINE&pageSize=50");
            var issues1 = await issues1Res.Content.ReadFromJsonAsync<ValidationIssuePageResult>();
            Assert.NotNull(issues1);
            Assert.Contains(issues1.Items, i => i.FeatureId == createdLine.Id);

            // 3. Fix the error by deleting the defective feature (with optimistic version parameter)
            var delRes = await client.DeleteAsync($"/api/v1/lines/{createdLine.Id}?version={createdLine.Version}");
            Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);

            // 4. Re-run validation -> Must be cleared from the new run!
            var run2Res = await client.PostAsync($"/api/v1/networks/{_waterNetworkId}/validation/run", null);
            var run2 = await run2Res.Content.ReadFromJsonAsync<ValidationRunDto>();
            Assert.NotNull(run2);

            var issues2Res = await client.GetAsync($"/api/v1/validation/issues?runId={run2.Id}&ruleCode=RULE_ZERO_LENGTH_LINE&pageSize=50");
            var issues2 = await issues2Res.Content.ReadFromJsonAsync<ValidationIssuePageResult>();
            Assert.NotNull(issues2);
            Assert.DoesNotContain(issues2.Items, i => i.FeatureId == createdLine.Id);
        }
        finally
        {
            // Ensure cleanup
            await client.DeleteAsync($"/api/v1/lines/{createdLine.Id}?version={createdLine.Version}");
        }
    }
}
