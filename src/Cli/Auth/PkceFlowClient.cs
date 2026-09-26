using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using SymplrCli.Platform;

namespace SymplrCli.Auth;

public record TokenResponse(
    [property: JsonPropertyName("access_token")]  string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")]    int ExpiresIn);

public class PkceFlowClient(HttpClient http)
{
    private const string ClientId = "ce-platform-symplr-cli";
    private const string RedirectUri = "http://localhost:14779/callback";
    private static readonly string ScopeString = "tenant:configuration:api:read tenant:configuration:api:write";

    // Returns the token, or null if the user cancelled / flow timed out.
    // onBrowserPrompt receives the authorization URL so the caller can open the browser and display it.
    public async Task<TokenResponse?> LoginAsync(
        EnvironmentConfig env,
        Action<string> onBrowserPrompt,
        CancellationToken ct = default)
    {
        var (verifier, challenge) = GeneratePkce();
        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
        var redirectUri = RedirectUri;

        var authUrl = BuildAuthorizationUrl(env, challenge, state, redirectUri);
        onBrowserPrompt(authUrl);

        var code = await WaitForCallbackAsync(state, ct);
        if (code is null) return null;

        return await ExchangeCodeAsync(env, code, redirectUri, verifier, ct);
    }

    public async Task RevokeAsync(EnvironmentConfig env, string token)
    {
        var form = new FormUrlEncodedContent([
            new("token", token),
            new("client_id", ClientId),
        ]);
        await http.PostAsync(env.RevocationEndpoint, form);
    }

    private string BuildAuthorizationUrl(EnvironmentConfig env, string challenge, string state, string redirectUri)
    {
        var q = new Dictionary<string, string>
        {
            ["client_id"]             = ClientId,
            ["response_type"]         = "code",
            ["scope"]                 = ScopeString,
            ["redirect_uri"]          = redirectUri,
            ["state"]                 = state,
            ["code_challenge"]        = challenge,
            ["code_challenge_method"] = "S256",
        };
        var qs = string.Join("&", q.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"{env.SsoBaseUrl}/as/authorization.oauth2?{qs}";
    }

    private async Task<string?> WaitForCallbackAsync(string expectedState, CancellationToken ct)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:14779/");
        listener.Start();

        using var _ = ct.Register(() => { try { listener.Stop(); } catch { /* already stopped */ } });

        HttpListenerContext context;
        try { context = await listener.GetContextAsync(); }
        catch (HttpListenerException) { return null; }
        catch (ObjectDisposedException) { return null; }

        var query = context.Request.QueryString;

        var responseHtml = """
            <html><body style="font-family:sans-serif;padding:2rem">
            <h2>Authentication complete</h2>
            <p>You can close this tab and return to the terminal.</p>
            </body></html>
            """;
        var buffer = Encoding.UTF8.GetBytes(responseHtml);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, CancellationToken.None);
        context.Response.Close();

        if (query["error"] is { } error)
            throw new InvalidOperationException($"Authorization denied: {error} — {query["error_description"]}");

        if (query["state"] != expectedState)
            throw new InvalidOperationException("OAuth state mismatch — possible CSRF.");

        return query["code"];
    }

    private async Task<TokenResponse?> ExchangeCodeAsync(
        EnvironmentConfig env,
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct)
    {
        var form = new FormUrlEncodedContent([
            new("grant_type",    "authorization_code"),
            new("code",          code),
            new("redirect_uri",  redirectUri),
            new("client_id",     ClientId),
            new("code_verifier", codeVerifier),
        ]);
        var response = await http.PostAsync(env.TokenEndpoint, form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct);
    }

    // ─── PKCE helpers ────────────────────────────────────────────────────────

    private static (string Verifier, string Challenge) GeneratePkce()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
