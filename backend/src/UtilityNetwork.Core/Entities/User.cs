namespace UtilityNetwork.Core.Entities;

public enum UserRole
{
    Viewer,
    Editor,
    Approver,
    Admin
}

public sealed record User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.Viewer;
    public bool IsActive { get; init; } = true;
    public int FailedLoginCount { get; init; }
    public DateTime? LockedUntil { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public string? Department { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public sealed record RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string TokenHash { get; init; } = string.Empty;
    public Guid FamilyId { get; init; } = Guid.NewGuid();
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public Guid? ReplacedBy { get; init; }
    public string? CreatedIp { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
