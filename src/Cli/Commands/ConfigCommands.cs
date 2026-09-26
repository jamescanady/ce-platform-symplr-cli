using System.CommandLine;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class ConfigCommands
{
    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("config", "Show current CLI configuration");
        cmd.SetHandler((resolvedEnv) =>
        {
            var configFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "symplr", "config.json");

            var envVar = Environment.GetEnvironmentVariable("SYMPLR_ENVIRONMENT");
            var envSource = envVar is not null
                ? $"{resolvedEnv}  (from SYMPLR_ENVIRONMENT)"
                : $"{resolvedEnv}  (default)";

            var store = new TokenStore();
            var sessions = store.All();
            var activeEnv = store.ActiveEnvironment();

            Formatter.PrintTable(
                ["SETTING", "VALUE"],
                [
                    ["config file",        configFile],
                    ["SYMPLR_ENVIRONMENT", envVar ?? "(not set)"],
                    ["default env",        envSource],
                ]);

            Console.WriteLine();

            if (sessions.Count == 0)
            {
                Console.WriteLine("No stored sessions. Run: symplr auth login");
                return;
            }

            Console.WriteLine("Stored sessions:");
            Formatter.PrintTable(
                ["  ENVIRONMENT", "EXPIRES", "STATUS"],
                sessions.Select(kv =>
                {
                    var expired = kv.Value.ExpiresAt < DateTimeOffset.UtcNow;
                    var marker = kv.Key.Equals(activeEnv?.ToString(), StringComparison.OrdinalIgnoreCase) ? "*" : " ";
                    return new[]
                    {
                        $"  {marker} {kv.Key}",
                        kv.Value.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        expired ? "expired" : "active",
                    };
                }));
        }, envOption);
        return cmd;
    }
}
