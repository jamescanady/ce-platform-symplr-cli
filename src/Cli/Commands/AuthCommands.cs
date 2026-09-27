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
        auth.Subcommands.Add(BuildLogin(envOption));
        auth.Subcommands.Add(BuildToken(envOption));
        auth.Subcommands.Add(BuildLogout(envOption));
        auth.Subcommands.Add(BuildStatus());
        auth.Subcommands.Add(BuildSwitch());
        return auth;
    }

    // ── auth login ────────────────────────────────────────────────────────────

    private static Command BuildLogin(Option<SymplrEnvironment> envOption)
    {
        var tokenOption = new Option<string?>("--token")
        {
            Description = "Skip browser flow and store this Bearer token directly",
        };

        var cmd = new Command("login", "Log in interactively via device flow (opens browser)");
        cmd.Options.Add(tokenOption);
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env      = parseResult.GetValue(envOption);
            var rawToken = parseResult.GetValue(tokenOption);

            var store  = new TokenStore();
            var config = EnvironmentConfig.For(env);

            if (rawToken is not null)
            {
                store.Save(env, new StoredToken(rawToken, null, DateTimeOffset.UtcNow.AddHours(1)));
                Console.WriteLine($"Token stored for {env}.");
                return;
            }

            Console.WriteLine($"Logging in to symplr Platform ({env})...");

            TokenResponse? token;
            try
            {
                token = await new DeviceFlowClient(new HttpClient())
                    .LoginAsync(config, (userCode, verificationUri) =>
                    {
                        Console.WriteLine($"Open your browser to: {verificationUri}");
                        Console.WriteLine($"Enter the code:       {userCode}");
                        Console.WriteLine("Waiting for authentication...");
                        OpenBrowser(verificationUri);
                    }, ct);
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
        });
        return cmd;
    }

    // ── auth token ────────────────────────────────────────────────────────────

    private static Command BuildToken(Option<SymplrEnvironment> envOption)
    {
        var clientIdOption = new Option<string?>("--client-id")
        {
            Description = "OAuth client ID (overrides stored value)",
        };
        var secretOption = new Option<string?>("--client-secret")
        {
            Description = "OAuth client secret (overrides stored value)",
        };
        var scopeOption = new Option<string?>("--scope")
        {
            Description = "OAuth scope (optional; server uses client defaults if omitted)",
        };

        var cmd = new Command("token", "Obtain a token via client credentials (non-interactive)");
        cmd.Options.Add(clientIdOption);
        cmd.Options.Add(secretOption);
        cmd.Options.Add(scopeOption);

        cmd.SetAction(async (parseResult, ct) =>
        {
            var env          = parseResult.GetValue(envOption);
            var clientId     = parseResult.GetValue(clientIdOption);
            var clientSecret = parseResult.GetValue(secretOption);
            var scope        = parseResult.GetValue(scopeOption);

            var store  = new TokenStore();
            var stored = store.GetClientCredential(env);

            var resolvedId     = clientId     ?? stored?.ClientId;
            var resolvedSecret = clientSecret ?? stored?.ClientSecret;

            if (resolvedId is null || resolvedSecret is null)
            {
                Formatter.Error(
                    resolvedId is null && resolvedSecret is null
                        ? $"No client credentials for {env}. Provide --client-id / --client-secret or run: symplr config set client-id / client-secret"
                        : resolvedId is null
                            ? "Missing --client-id (not stored). Run: symplr config set client-id <id>"
                            : "Missing --client-secret (not stored). Run: symplr config set client-secret <secret>");
                return;
            }

            TokenResponse token;
            try
            {
                token = await new ClientCredentialsFlowClient(new HttpClient())
                    .LoginAsync(EnvironmentConfig.For(env), resolvedId, resolvedSecret, scope);
            }
            catch (HttpRequestException ex)
            {
                Formatter.Error($"Token request failed: {(int?)ex.StatusCode} {ex.Message}");
                return;
            }

            store.Save(env, new StoredToken(
                token.AccessToken,
                token.RefreshToken,
                DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn)));

            Console.WriteLine($"Token stored for {env} (expires in {token.ExpiresIn}s).");
        });
        return cmd;
    }

    // ── auth logout ───────────────────────────────────────────────────────────

    private static Command BuildLogout(Option<SymplrEnvironment> envOption)
    {
        var cmd = new Command("logout", "Log out and revoke the stored token");
        cmd.SetAction(async (parseResult, ct) =>
        {
            var env   = parseResult.GetValue(envOption);
            var store = new TokenStore();
            var stored = store.Load(env);
            if (stored is null) { Console.WriteLine($"Not logged in to {env}."); return; }

            var config = EnvironmentConfig.For(env);
            try { await new DeviceFlowClient(new HttpClient()).RevokeAsync(config, stored.AccessToken); }
            catch { /* best-effort revocation */ }

            store.Remove(env);
            Console.WriteLine($"Logged out of {env}.");
        });
        return cmd;
    }

    // ── auth status ───────────────────────────────────────────────────────────

    private static Command BuildStatus()
    {
        var cmd = new Command("status", "Show current authentication state");
        cmd.SetAction(parseResult =>
        {
            var store  = new TokenStore();
            var all    = store.All();
            var active = store.ActiveEnvironment();

            if (all.Count == 0) { Console.WriteLine("Not logged in to any environment."); return; }

            Formatter.PrintTable(
                ["ENVIRONMENT", "EXPIRES", "STATUS"],
                all.Select(kv =>
                {
                    var expired = kv.Value.ExpiresAt < DateTimeOffset.UtcNow;
                    var marker  = kv.Key.Equals(active?.ToString(), StringComparison.OrdinalIgnoreCase) ? "*" : " ";
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

    // ── auth switch ───────────────────────────────────────────────────────────

    private static Command BuildSwitch()
    {
        var envArg = new Argument<SymplrEnvironment>("environment")
        {
            Description = "Environment to switch to (dev, qa, stable, staging, production)",
        };
        var cmd = new Command("switch", "Switch the default environment");
        cmd.Arguments.Add(envArg);
        cmd.SetAction(parseResult =>
        {
            var env   = parseResult.GetValue(envArg);
            var store = new TokenStore();
            var token = store.Load(env);

            if (token is null)
                Console.WriteLine($"Warning: no stored session for {env}. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");
            else if (token.ExpiresAt < DateTimeOffset.UtcNow)
                Console.WriteLine($"Warning: token for {env} has expired. Run: symplr auth login --env {env.ToString().ToLowerInvariant()}");

            store.SetActive(env);
            Console.WriteLine($"Switched to {env}.");
        });
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
