using System.CommandLine;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class ConfigCommands
{
    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("config", "Show or modify CLI configuration");
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

            var platformHosts = store.AllPlatformHosts();
            if (platformHosts.Count > 0)
            {
                Console.WriteLine("Platform host overrides:");
                Formatter.PrintTable(
                    ["  ENVIRONMENT", "HOST"],
                    platformHosts.Select(kv => new[] { $"  {kv.Key}", kv.Value }));
                Console.WriteLine();
            }

            var routePrefixes = store.AllRoutePrefixes();
            if (routePrefixes.Count > 0)
            {
                Console.WriteLine("Route prefix overrides:");
                Formatter.PrintTable(
                    ["  SERVICE", "PREFIX"],
                    routePrefixes.Select(kv => new[] { $"  {kv.Key}", kv.Value }));
                Console.WriteLine();
            }

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

        cmd.AddCommand(BuildSetCommand(envOption));
        cmd.AddCommand(BuildUnsetCommand(envOption));
        return cmd;
    }

    // ─── set ─────────────────────────────────────────────────────────────────

    private static Command BuildSetCommand(Option<SymplrEnvironment> envOption)
    {
        var set = new Command("set", "Set a configuration override");
        set.AddCommand(BuildSetPlatformHostCommand(envOption));
        set.AddCommand(BuildSetRoutePrefixCommand());
        return set;
    }

    private static Command BuildSetPlatformHostCommand(Option<SymplrEnvironment> envOption)
    {
        var hostArg = new Argument<string>("host", "Platform hostname (e.g. my-stable.example.com)");
        var cmd = new Command("platform-host", "Override the platform hostname for an environment");
        cmd.AddArgument(hostArg);
        cmd.SetHandler((env, host) =>
        {
            new TokenStore().SetPlatformHost(env, host);
            Console.WriteLine($"Platform host for {env} set to: {host}");
            Console.WriteLine($"Resolved URL example: https://{host}/<route-prefix>");
        }, envOption, hostArg);
        return cmd;
    }

    private static Command BuildSetRoutePrefixCommand()
    {
        var serviceArg = new Argument<string>("service", "Service key (e.g. tcm)");
        var prefixArg = new Argument<string>("prefix", "Route prefix (e.g. ce-platform-tenant-configuration-service)");
        var cmd = new Command("route-prefix", "Override the route prefix for a service");
        cmd.AddArgument(serviceArg);
        cmd.AddArgument(prefixArg);
        cmd.SetHandler((service, prefix) =>
        {
            new TokenStore().SetRoutePrefix(service, prefix);
            Console.WriteLine($"Route prefix for '{service}' set to: {prefix}");
        }, serviceArg, prefixArg);
        return cmd;
    }

    // ─── unset ───────────────────────────────────────────────────────────────

    private static Command BuildUnsetCommand(Option<SymplrEnvironment> envOption)
    {
        var unset = new Command("unset", "Remove a configuration override (restores default)");
        unset.AddCommand(BuildUnsetPlatformHostCommand(envOption));
        unset.AddCommand(BuildUnsetRoutePrefixCommand());
        return unset;
    }

    private static Command BuildUnsetPlatformHostCommand(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("platform-host", "Remove the platform hostname override for an environment");
        cmd.SetHandler((env) =>
        {
            new TokenStore().UnsetPlatformHost(env);
            Console.WriteLine($"Platform host override for {env} removed. Default: {ServiceUrlResolver.DefaultPlatformHost(env)}");
        }, envOption);
        return cmd;
    }

    private static Command BuildUnsetRoutePrefixCommand()
    {
        var serviceArg = new Argument<string>("service", "Service key (e.g. tcm)");
        var cmd = new Command("route-prefix", "Remove the route prefix override for a service");
        cmd.AddArgument(serviceArg);
        cmd.SetHandler((service) =>
        {
            new TokenStore().UnsetRoutePrefix(service);
            Console.WriteLine($"Route prefix override for '{service}' removed.");
        }, serviceArg);
        return cmd;
    }
}
