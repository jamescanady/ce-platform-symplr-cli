using System.Net.Http.Json;
using SymplrCli.Platform;

namespace SymplrCli.Auth;

public class ClientCredentialsFlowClient(HttpClient http)
{
    public async Task<TokenResponse> LoginAsync(
        EnvironmentConfig env,
        string clientId,
        string clientSecret,
        string? scope = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("grant_type",    "client_credentials"),
            new("client_id",     clientId),
            new("client_secret", clientSecret),
        };
        if (scope is not null)
            fields.Add(new("scope", scope));

        var response = await http.PostAsync(env.TokenEndpoint, new FormUrlEncodedContent(fields));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }
}
