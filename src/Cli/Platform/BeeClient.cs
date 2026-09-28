using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SymplrCli.Platform;

public class BeeClient
{
    private readonly HttpClient _http;

    private BeeClient(HttpClient http) => _http = http;

    public static BeeClient Create(string baseUrl, string token)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new BeeClient(http);
    }

    public Task<HttpResponseMessage> PostEventAsync(BeeEventRequest request, CancellationToken ct = default) =>
        _http.PostAsJsonAsync("v1/EventEngine/event", request, SymplrJsonContext.Default.BeeEventRequest, ct);
}
