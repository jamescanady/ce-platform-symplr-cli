using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SymplrCli.Platform;

namespace SymplrCli.Auth;

public record TokenResponse(
    [property: JsonPropertyName("access_token")]  string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")]    int ExpiresIn);

public record DeviceAuthResponse(
    [property: JsonPropertyName("device_code")]               string DeviceCode,
    [property: JsonPropertyName("user_code")]                 string UserCode,
    [property: JsonPropertyName("verification_uri")]          string VerificationUri,
    [property: JsonPropertyName("verification_uri_complete")] string? VerificationUriComplete,
    [property: JsonPropertyName("expires_in")]                int ExpiresIn,
    [property: JsonPropertyName("interval")]                  int Interval);

internal record DeviceFlowError([property: JsonPropertyName("error")] string? Error);

public class DeviceFlowClient(HttpClient http)
{
    private const string ClientId = "ce-platform-symplr-cli";
    private static readonly string ScopeString = "tenant:configuration:api:read tenant:configuration:api:write";

    public async Task<TokenResponse?> LoginAsync(
        EnvironmentConfig env,
        Action<string, string> onPrompt,
        CancellationToken ct = default)
    {
        var deviceAuth = await RequestDeviceAuthorizationAsync(env, ct);
        onPrompt(deviceAuth.UserCode, deviceAuth.VerificationUri);
        return await PollForTokenAsync(env, deviceAuth, ct);
    }

    public async Task RevokeAsync(EnvironmentConfig env, string token)
    {
        var form = new FormUrlEncodedContent([
            new("token", token),
            new("client_id", ClientId),
        ]);
        await http.PostAsync(env.RevocationEndpoint, form);
    }

    private async Task<DeviceAuthResponse> RequestDeviceAuthorizationAsync(
        EnvironmentConfig env, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent([
            new("client_id", ClientId),
            new("scope",     ScopeString),
        ]);
        var response = await http.PostAsync(env.DeviceAuthEndpoint, form, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync(
            SymplrJsonContext.Default.DeviceAuthResponse, ct))!;
    }

    private async Task<TokenResponse?> PollForTokenAsync(
        EnvironmentConfig env, DeviceAuthResponse deviceAuth, CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(deviceAuth.Interval, 5));
        var expiry = DateTimeOffset.UtcNow.AddSeconds(deviceAuth.ExpiresIn);

        while (DateTimeOffset.UtcNow < expiry)
        {
            await Task.Delay(interval, ct);

            var form = new FormUrlEncodedContent([
                new("grant_type",  "urn:ietf:params:oauth:grant-type:device_code"),
                new("device_code", deviceAuth.DeviceCode),
                new("client_id",   ClientId),
            ]);

            var response = await http.PostAsync(env.TokenEndpoint, form, ct);

            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync(
                    SymplrJsonContext.Default.TokenResponse, ct);

            var error = await ParseErrorAsync(response);
            switch (error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    continue;
                case "expired_token":
                    return null;
                default:
                    throw new InvalidOperationException($"Device flow error: {error}");
            }
        }
        return null;
    }

    private static async Task<string> ParseErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.DeviceFlowError);
            return body?.Error ?? "unknown_error";
        }
        catch { return "unknown_error"; }
    }
}
