using System.CommandLine;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SymplrCli.Auth;
using SymplrCli.Output;
using SymplrCli.Platform;

namespace SymplrCli.Commands;

public static class AuthCommands
{
    public static Command Build(Option<SymplrEnvironment> envOption)
    {
        var auth = new Command("auth", "Authenticate with symplr Platform");
        auth.AddCommand(BuildLogin(envOption));
        auth.AddCommand(BuildLogout(envOption));
        auth.AddCommand(BuildStatus());
        auth.AddCommand(BuildSwitch());
        return auth;
    }

    private static Command BuildLogin(Option<SymplrEnvironment> envOption)
    {
        var tokenOption = new Option<string?>("--token", "Skip browser flow and store this Bearer token directly");
        var cmd = new Command("login", "Log in to symplr Platform (opens browser for SSO)");
        cmd.AddOption(tokenOption);
        cmd.SetHandler(async (env, rawToken) =>
        {
            var store = new TokenStore();

            if (rawToken is not null)
            {
                store.Save(env, new StoredToken(rawToken, null, DateTimeOffset.UtcNow.AddHours(1)));
                Console.WriteLine($"Token stored for {env}.");
                return;
            }

            var config = EnvironmentConfig.For(env);
            var flow = new PkceFlowClient(new HttpClient());

            Console.WriteLine($"Logging in to symplr Platform ({env})...");

            TokenResponse? token;
            try
            {
                token = await flow.LoginAsync(config, authUrl =>
                {
                    Console.WriteLine("Opening your browser for authentication.");
                    Console.WriteLine($"If it does not open automatically, visit:");
                    Console.WriteLine($"  {authUrl}");
                    OpenBrowser(authUrl);
                });
            }
            catch (InvalidOperationException ex)
            {
                Formatter.Error(ex.Message);
                return;
            }
            catch (Exception ex)
            {
                Formatter.Error($"Login failed: {ex.Message}");
                return;
            }

            if (token is null) { Formatter.Error("Login was cancelled or timed out."); return; }

            store.Save(env, new StoredToken(
                token.AccessToken,
                token.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)));

            Console.WriteLine($"Logged in to {env}.");
        }, envOption, tokenOption);
        return cmd;
    }

    private static Command BuildLogout(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("logout", "Log out and revoke the stored token");
        cmd.SetHandler(async (env) =>
        {
            var store = new TokenStore();
            var stored = store.Load(env);
            if (stored is null) { Console.WriteLine($"Not logged in to {env}."); return; }

            var config = EnvironmentConfig.For(env);
            var flow = new PkceFlowClient(new HttpClient());
            try { await flow.RevokeAsync(config, stored.AccessToken); }
            catch { /* best-effort revocation */ }

            store.Remove(env);
            Console.WriteLine($"Logged out of {env}.");
        }, envOption);
        return cmd;
    }

    private static Command BuildStatus()
    {
        var cmd = new Command("status", "Show current authentication state");
        cmd.SetHandler(() =>
        {
            var store = new TokenStore();
            var all = store.All();
            var active = store.ActiveEnvironment();

            if (all.Count == 0) { Console.WriteLine("Not logged in to any environment."); return; }

            Formatter.PrintTable(
                ["ENVIRONMENT", "EXPIRES", "STATUS"],
                all.Select(kv =>
                {
                    var expired = kv.Value.ExpiresAt < DateTimeOffset.UtcNow;
                    var marker = kv.Key.Equals(active?.ToString(), StringComparison.OrdinalIgnoreCase) ? "*" : " ";
                    return new[]
                    {
                        $"{marker} {kv.Key}",
                        kv.Value.ExpiresAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        expired ? "expired" : "active",
                    };
                }));
        });
        return cmd;
    }

    private static Command BuildSwitch()
    {
        var envArg = new Argument<SymplrEnvironment>("environment", "Environment to switch to (dev, qa, stable, staging, production)");
        var cmd = new Command("switch", "Switch the default environment");
        cmd.AddArgument(envArg);
        cmd.SetHandler((env) =>
        {
            var store = new TokenStore();
            var token = store.Load(env);

            if (token is null)
                Console.WriteLine($"Warning: no stored session for {env}. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");
            else if (token.ExpiresAt < DateTimeOffset.UtcNow)
                Console.WriteLine($"Warning: token for {env} has expired. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");

            store.SetActive(env);
            Console.WriteLine($"Switched to {env}.");
        }, envArg);
        return cmd;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Process.Start("xdg-open", url);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
        }
        catch { /* non-fatal — user can copy the URL manually */ }
    }
}
