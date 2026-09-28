using System.Text.Json.Serialization;

namespace SymplrCli.Platform;

public record BeeDefaultPayloadItem(
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("id")]       Guid   Id);
