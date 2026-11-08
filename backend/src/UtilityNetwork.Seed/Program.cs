using UtilityNetwork.Seed;

var options = SeedOptions.Parse(args);
var connectionString = EnvLoader.ResolveConnectionString(options.ConnectionString);

var runner = new SeedRunner(connectionString);
var exitCode = await runner.RunAsync(options);
return exitCode;
