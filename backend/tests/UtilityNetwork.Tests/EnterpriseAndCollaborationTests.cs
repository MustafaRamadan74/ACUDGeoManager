using UtilityNetwork.Api.Services;
using UtilityNetwork.Core.Models;
using UtilityNetwork.Core.Repositories;
using UtilityNetwork.Data.Services;

namespace UtilityNetwork.Tests;

public sealed class EnterpriseAndCollaborationTests
{
    private sealed class FakeEnterpriseRepository : IEnterpriseRepository
    {
        public Task<IReadOnlyList<FeatureAttachmentDto>> GetAttachmentsByFeatureAsync(Guid featureId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeatureAttachmentDto>>([]);

        public Task<FeatureAttachmentDto?> GetAttachmentByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<FeatureAttachmentDto?>(null);

        public Task<FeatureAttachmentDto> AddAttachmentAsync(FeatureAttachmentDto attachment, CancellationToken ct = default)
            => Task.FromResult(attachment);

        public Task<bool> DeleteAttachmentAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<FeatureRelationshipDto>> GetRelationshipsByFeatureAsync(Guid featureId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<FeatureRelationshipDto>>([]);

        public Task<FeatureRelationshipDto> AddRelationshipAsync(FeatureRelationshipDto relationship, CancellationToken ct = default)
            => Task.FromResult(relationship);

        public Task<bool> DeleteRelationshipAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<FeatureLockDto?> GetLockAsync(Guid featureId, Guid? currentUserId = null, CancellationToken ct = default)
            => Task.FromResult<FeatureLockDto?>(null);

        public Task<(bool Success, FeatureLockDto LockInfo)> TryAcquireLockAsync(
            Guid featureId, string featureTable, Guid userId, string username, int ttlMinutes, string? clientSessionId, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow;
            var lockDto = new FeatureLockDto(featureId, featureTable, userId, username, now, now.AddMinutes(ttlMinutes), clientSessionId, true, true);
            return Task.FromResult((true, lockDto));
        }

        public Task<bool> ReleaseLockAsync(Guid featureId, Guid userId, bool isAdmin = false, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> RefreshLockAsync(Guid featureId, Guid userId, int ttlMinutes, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task ClearExpiredLocksAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<LayerDataQualityDto>> GetDataQualityReportAsync(Guid? networkId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LayerDataQualityDto>>([]);
    }

    [Fact]
    public async Task UploadAttachment_WhenExtensionDisallowed_ThrowsArgumentException()
    {
        var repo = new FakeEnterpriseRepository();
        var service = new EnterpriseService(repo);
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAttachmentAsync(
            Guid.NewGuid(),
            "points",
            ms,
            "malicious_script.exe",
            "application/x-msdownload",
            3,
            "test",
            "tester"
        ));
    }

    [Fact]
    public async Task UploadAttachment_WhenTableInvalid_ThrowsArgumentException()
    {
        var repo = new FakeEnterpriseRepository();
        var service = new EnterpriseService(repo);
        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAttachmentAsync(
            Guid.NewGuid(),
            "invalid_table",
            ms,
            "drawing.dwg",
            "application/acad",
            3,
            "test",
            "tester"
        ));
    }

    [Fact]
    public async Task CreateRelationship_SelfRelationship_ThrowsArgumentException()
    {
        var repo = new FakeEnterpriseRepository();
        var service = new EnterpriseService(repo);

        var sameId = Guid.NewGuid();
        var req = new CreateRelationshipRequest(
            sameId,
            "points",
            sameId,
            "points",
            "FEEDS",
            "Self link"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRelationshipAsync(req, "tester"));
    }

    [Fact]
    public async Task CreateRelationship_InvalidType_ThrowsArgumentException()
    {
        var repo = new FakeEnterpriseRepository();
        var service = new EnterpriseService(repo);

        var req = new CreateRelationshipRequest(
            Guid.NewGuid(),
            "points",
            Guid.NewGuid(),
            "lines",
            "NON_EXISTENT_TYPE",
            "Bad link"
        );

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateRelationshipAsync(req, "tester"));
    }

    [Fact]
    public async Task PresenceTracker_TrackingOperations_MaintainAccurateCounts()
    {
        var tracker = new PresenceTracker();
        var ws1 = Guid.NewGuid();
        var ws2 = Guid.NewGuid();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        await tracker.UserConnectedAsync("conn-1", u1, "engineer1", "EDITOR", ws1);
        await tracker.UserConnectedAsync("conn-2", u2, "viewer1", "VIEWER", ws1);

        var ws1Users = tracker.GetUsersInWorkspace(ws1);
        Assert.Equal(2, ws1Users.Count);

        // Switch user 2 to workspace 2
        await tracker.SwitchWorkspaceAsync("conn-2", ws2);
        Assert.Single(tracker.GetUsersInWorkspace(ws1));
        Assert.Single(tracker.GetUsersInWorkspace(ws2));

        // Disconnect user 1
        var removed = await tracker.UserDisconnectedAsync("conn-1");
        Assert.NotNull(removed);
        Assert.Equal(u1, removed.UserId);
        Assert.Empty(tracker.GetUsersInWorkspace(ws1));
    }
}
