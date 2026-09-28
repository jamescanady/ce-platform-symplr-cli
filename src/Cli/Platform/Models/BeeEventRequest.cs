using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymplrCli.Platform;

public record BeeEventRequest(
    [property: JsonPropertyName("eventName")]   string        EventName,
    [property: JsonPropertyName("tenantId")]    Guid          TenantId,
    [property: JsonPropertyName("productName")] string        ProductName,
    [property: JsonPropertyName("environment")] string        Environment,
    [property: JsonPropertyName("uri")]         string?       Uri,
    [property: JsonPropertyName("counter")]     long          Counter,
    [property: JsonPropertyName("timestamp")]   DateTimeOffset Timestamp,
    [property: JsonPropertyName("payload")]     JsonElement[] Payload);
