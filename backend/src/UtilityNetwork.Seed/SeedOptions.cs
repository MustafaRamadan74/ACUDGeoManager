namespace UtilityNetwork.Seed;

public sealed record SeedOptions
{
    public string Size { get; init; } = "small";
    public bool Clean { get; init; }
    public string? ConnectionString { get; init; }

    public static SeedOptions Parse(string[] args)
    {
        var size = "small";
        var clean = false;
        string? connectionString = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--size", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                size = args[++i].ToLowerInvariant();
            }
            else if (arg.StartsWith("--size=", StringComparison.OrdinalIgnoreCase))
            {
                size = arg["--size=".Length..].ToLowerInvariant();
            }
            else if (arg.Equals("--clean", StringComparison.OrdinalIgnoreCase))
            {
                clean = true;
            }
            else if (arg.Equals("--connection-string", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                connectionString = args[++i];
            }
            else if (arg.StartsWith("--connection-string=", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = arg["--connection-string=".Length..];
            }
        }

        if (size is not ("small" or "medium" or "stress"))
        {
            Console.WriteLine($"[WARN] Unrecognized size '{size}', defaulting to 'small'.");
            size = "small";
        }

        return new SeedOptions
        {
            Size = size,
            Clean = clean,
            ConnectionString = connectionString
        };
    }
}
