namespace UtilityNetwork.Seed;

public static class EnvLoader
{
    public static string ResolveConnectionString(string? explicitConnectionString)
    {
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var envVar = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            return envVar;
        }

        // Search for .env file in current directory or up to 4 parent levels
        var currentDir = Directory.GetCurrentDirectory();
        for (var i = 0; i < 5; i++)
        {
            var envPath = Path.Combine(currentDir, ".env");
            if (File.Exists(envPath))
            {
                var lines = File.ReadAllLines(envPath);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                    {
                        continue;
                    }

                    var parts = trimmed.Split('=', 2);
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (key.Equals("ConnectionStrings__DefaultConnection", StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }
            }

            var parent = Directory.GetParent(currentDir);
            if (parent == null)
            {
                break;
            }
            currentDir = parent.FullName;
        }

        return "Host=localhost;Port=5434;Database=acud_geom_db;Username=postgres;Password=postgres_password;Include Error Detail=true";
    }
}
