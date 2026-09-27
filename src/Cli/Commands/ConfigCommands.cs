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

            var credentials = store.AllClientCredentials();
            if (credentials.Count > 0)
            {
                Console.WriteLine("Client credentials:");
                Formatter.PrintTable(
                    ["  ENVIRONMENT", "CLIENT ID", "SECRET"],
                    credentials.Select(kv => new[]
                    {
                        $"  {kv.Key}",
                        kv.Value.ClientId,
                        $"****{kv.Value.ClientSecret[^Math.Min(4, kv.Value.ClientSecret.Length)..]}",
                    }));
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
        set.AddCommand(BuildSetClientIdCommand(envOption));
        set.AddCommand(BuildSetClientSecretCommand(envOption));
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

    private static Command BuildSetClientIdCommand(Option<SymplrEnvironment> envOption)
    {
        var idArg = new Argument<string>("client-id", "OAuth client ID");
        var cmd = new Command("client-id", "Store the client ID for client credentials login");
        cmd.AddArgument(idArg);
        cmd.SetHandler((env, id) =>
        {
            var store = new TokenStore();
            var existing = store.GetClientCredential(env);
            store.SetClientCredential(env, id, existing?.ClientSecret ?? "");
            Console.WriteLine($"Client ID for {env} set to: {id}");
            if (existing?.ClientSecret is null or "")
                Console.WriteLine($"  Run 'symplr config set client-secret' to complete the configuration.");
        }, envOption, idArg);
        return cmd;
    }

    private static Command BuildSetClientSecretCommand(Option<SymplrEnvironment> envOption)
    {
        var secretArg = new Argument<string>("client-secret", "OAuth client secret");
        var cmd = new Command("client-secret", "Store the client secret for client credentials login");
        cmd.AddArgument(secretArg);
        cmd.SetHandler((env, secret) =>
        {
            var store = new TokenStore();
            var existing = store.GetClientCredential(env);
            store.SetClientCredential(env, existing?.ClientId ?? "", secret);
            Console.WriteLine($"Client secret for {env} stored.");
            if (existing?.ClientId is null or "")
                Console.WriteLine($"  Run 'symplr config set client-id' to complete the configuration.");
        }, envOption, secretArg);
        return cmd;
    }

    // ─── unset ───────────────────────────────────────────────────────────────

    private static Command BuildUnsetCommand(Option<SymplrEnvironment> envOption)
    {
        var unset = new Command("unset", "Remove a configuration override (restores default)");
        unset.AddCommand(BuildUnsetPlatformHostCommand(envOption));
        unset.AddCommand(BuildUnsetRoutePrefixCommand());
        unset.AddCommand(BuildUnsetClientCredentialsCommand(envOption));
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

    private static Command BuildUnsetClientCredentialsCommand(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("client-credentials", "Remove stored client credentials for an environment");
        cmd.SetHandler((env) =>
        {
            new TokenStore().UnsetClientCredential(env);
            Console.WriteLine($"Client credentials for {env} removed. 'symplr auth login' will use device flow.");
        }, envOption);
        return cmd;
    }
}
