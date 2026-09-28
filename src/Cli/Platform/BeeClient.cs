using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymplrCli.Platform;

public record BeeDefaultPayloadItem(
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("id")]       Guid   Id);

public record BeeEventRequest(
    [property: JsonPropertyName("eventName")]   string        EventName,
    [property: JsonPropertyName("tenantId")]    Guid          TenantId,
    [property: JsonPropertyName("productName")] string        ProductName,
    [property: JsonPropertyName("environment")] string        Environment,
    [property: JsonPropertyName("uri")]         string?       Uri,
    [property: JsonPropertyName("counter")]     long          Counter,
    [property: JsonPropertyName("timestamp")]   DateTimeOffset Timestamp,
    [property: JsonPropertyName("payload")]     JsonElement[] Payload);

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
