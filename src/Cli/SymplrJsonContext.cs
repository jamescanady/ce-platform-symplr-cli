using System.Text.Json.Serialization;
using SymplrCli.Auth;
using SymplrCli.Commands;
using SymplrCli.Platform;

namespace SymplrCli;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(TenantResponse[]))]
[JsonSerializable(typeof(TenantResponse))]
[JsonSerializable(typeof(NamespaceResponse[]))]
[JsonSerializable(typeof(NamespaceResponse))]
[JsonSerializable(typeof(TenantNamespaceResponse[]))]
[JsonSerializable(typeof(ProductResponse[]))]
[JsonSerializable(typeof(ProductResponse))]
[JsonSerializable(typeof(TenantByProductResponse[]))]
[JsonSerializable(typeof(ProductEnvironmentResponse[]))]
[JsonSerializable(typeof(EventConsumerResponse[]))]
[JsonSerializable(typeof(EventConsumerResponse))]
[JsonSerializable(typeof(EventTypeResponse[]))]
[JsonSerializable(typeof(EventTypeResponse))]
[JsonSerializable(typeof(EventTypeConsumerResponse[]))]
[JsonSerializable(typeof(EventTypeConsumerResponse))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(DeviceAuthResponse))]
[JsonSerializable(typeof(DeviceFlowError))]
[JsonSerializable(typeof(ServiceVersionResponse))]
internal partial class SymplrJsonContext : JsonSerializerContext { }
