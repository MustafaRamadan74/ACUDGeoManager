namespace UtilityNetwork.Core.Configuration;

public sealed record DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    public string DefaultConnection { get; init; } = string.Empty;
}

