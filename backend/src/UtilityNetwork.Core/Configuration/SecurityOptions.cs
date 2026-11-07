namespace UtilityNetwork.Core.Configuration;

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "ACUDGeoManager";
    public string Audience { get; init; } = "ACUDGeoManagerClient";
    public string Secret { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}

public sealed record AdminOptions
{
    public const string SectionName = "Admin";

    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
