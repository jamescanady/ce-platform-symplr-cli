using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;

namespace SymplrCli.Platform;

public record TenantResponse(
    Guid? Id,
    string Name,
    string Description,
    string GlobalTenantCode,
    string TenantShortCode,
    bool IsDisabled,
    TenantProductResponse[]? Products);

public record TenantProductResponse(Guid? Id, string Name, string Description, bool IsDisabled);

public record NamespaceResponse(
    Guid Id,
    string? Name,
    string? Description,
    bool IsDefault,
    bool IsDisabled,
    DateTimeOffset CreatedDate,
    string? CreatedBy,
    DateTimeOffset LastModified,
    string? LastModifiedBy);

public record TenantNamespaceResponse(
    Guid TenantId,
    string? TenantName,
    string? TenantDescription,
    string? TenantShortCode,
    TenantNamespaceItem[]? Namespaces);

public record TenantNamespaceItem(
    Guid NamespaceId,
    string? Namespace,
    string? NamespaceDescription,
    NamespaceProductItem[]? Products);

public record NamespaceProductItem(
    Guid ProductId,
    string? ProductName,
    string? ProductDescription,
    string? EnvironmentName,
    Guid ProductEnvironmentId,
    Guid TenantProductEnvironmentId);

public record ProductResponse(Guid? Id, string Name, string Description, bool IsDisabled);

public record TenantByProductResponse(
    Guid Id,
    string? Name,
    string? Description,
    string? TenantGlobalId,
    string? TenantShortCode,
    string? TenantProductCode,
    Guid? NamespaceId,
    string? NameSpace,
    Guid ProductId,
    string? ProductName,
    string? EnvironmentName);

public record ProductEnvironmentResponse(Guid? Id, Guid ProductId, string? Name, bool IsDisabled);

public record EventConsumerResponse(
    Guid? Id,
    Guid TenantId,
    string Name,
    string? Description,
    string Endpoint,
    string? AuthorizationType,
    object? AuthorizationParameters,
    object? InvocationHttpParameters,
    bool IsDisabled,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified,
    int Version);

public record EventTypeResponse(
    Guid Id,
    Guid ProductId,
    string? Name,
    string? Description,
    bool IsDisabled,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified);

public record EventTypeConsumerResponse(
    Guid Id,
    Guid ConsumerId,
    string? ConsumerName,
    string? ConsumerDescription,
    string? ConsumerEndpoint,
    string? ConsumerAuthorizationType,
    Guid EventTypeId,
    string? EventTypeName,
    Guid TenantProductEnvironmentId,
    Guid TenantId,
    string? TenantName,
    Guid ProductId,
    string? ProductName,
    string? ProductString,
    string? EnvironmentName,
    bool IsDisabled,
    string? CreatedBy,
    DateTimeOffset Created,
    string? LastModifiedBy,
    DateTimeOffset LastModified);

public class TcmClient
{
    private readonly HttpClient _http;

    private TcmClient(HttpClient http) => _http = http;

    // BaseAddress must end with '/' and relative paths must not start with '/'
    // so that the path segment is appended, not replaced.
    public static TcmClient Create(string baseUrl, string accessToken)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return new TcmClient(http);
    }

    public Task<TenantResponse[]?> GetTenantsAsync(bool withProducts = false) =>
        GetAsync($"v1/Tenant?withProducts={withProducts}", SymplrJsonContext.Default.TenantResponseArray);

    public async Task<TenantResponse?> GetTenantAsync(Guid id, bool withProducts = false)
    {
        var response = await _http.GetAsync($"v1/Tenant/{id}?withProducts={withProducts}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.TenantResponse);
    }

    public Task<TenantResponse[]?> FindTenantsAsync(string needle) =>
        GetAsync($"v1/Tenant/find/{Uri.EscapeDataString(needle)}", SymplrJsonContext.Default.TenantResponseArray);

    public Task<TenantNamespaceResponse[]?> GetTenantNamespacesAsync(Guid tenantId) =>
        GetAsync($"v1/Tenant/{tenantId}/productEnvironmentsByNamespace", SymplrJsonContext.Default.TenantNamespaceResponseArray);

    public Task<NamespaceResponse[]?> GetNamespacesAsync(bool includeInactive = false) =>
        GetAsync($"v1/Namespace?includeInactive={includeInactive}", SymplrJsonContext.Default.NamespaceResponseArray);

    public async Task<NamespaceResponse?> GetNamespaceAsync(Guid id)
    {
        var response = await _http.GetAsync($"v1/Namespace/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.NamespaceResponse);
    }

    public Task<ProductResponse[]?> GetProductsAsync() =>
        GetAsync("v1/Product", SymplrJsonContext.Default.ProductResponseArray);

    public async Task<ProductResponse?> GetProductAsync(Guid id)
    {
        var response = await _http.GetAsync($"v1/Product/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.ProductResponse);
    }

    public Task<ProductResponse[]?> FindProductsAsync(string needle) =>
        GetAsync($"v1/Product/find/{Uri.EscapeDataString(needle)}", SymplrJsonContext.Default.ProductResponseArray);

    public Task<TenantByProductResponse[]?> GetTenantsByProductAsync(Guid id, string? tenantFilter = null, string? namespaceName = null)
    {
        var qs = new List<string>();
        if (tenantFilter is not null) qs.Add($"tenantFilter={Uri.EscapeDataString(tenantFilter)}");
        if (namespaceName is not null) qs.Add($"namespaceName={Uri.EscapeDataString(namespaceName)}");
        var query = qs.Count > 0 ? "?" + string.Join("&", qs) : "";
        return GetAsync($"v1/Product/{id}/Tenant{query}", SymplrJsonContext.Default.TenantByProductResponseArray);
    }

    public Task<ProductEnvironmentResponse[]?> GetProductEnvironmentsAsync(Guid productId) =>
        GetAsync($"v1/ProductEnvironment/Product/{productId}", SymplrJsonContext.Default.ProductEnvironmentResponseArray);

    public Task<EventConsumerResponse[]?> GetEventConsumersAsync() =>
        GetAsync("v1/EventConsumer", SymplrJsonContext.Default.EventConsumerResponseArray);

    public async Task<EventConsumerResponse?> GetEventConsumerAsync(Guid id)
    {
        var response = await _http.GetAsync($"v1/EventConsumer/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.EventConsumerResponse);
    }

    public Task<EventTypeResponse[]?> GetEventTypesAsync() =>
        GetAsync("v1/EventType", SymplrJsonContext.Default.EventTypeResponseArray);

    public async Task<EventTypeResponse?> GetEventTypeAsync(Guid id)
    {
        var response = await _http.GetAsync($"v1/EventType/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.EventTypeResponse);
    }

    public Task<EventTypeConsumerResponse[]?> GetEventTypeConsumersAsync() =>
        GetAsync("v1/EventTypeConsumer", SymplrJsonContext.Default.EventTypeConsumerResponseArray);

    public async Task<EventTypeConsumerResponse?> GetEventTypeConsumerAsync(Guid id)
    {
        var response = await _http.GetAsync($"v1/EventTypeConsumer/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(SymplrJsonContext.Default.EventTypeConsumerResponse);
    }

    public Task<EventTypeConsumerResponse[]?> GetEventTypeConsumersByConsumerAsync(Guid consumerId) =>
        GetAsync($"v1/EventTypeConsumer/eventConsumer/{consumerId}", SymplrJsonContext.Default.EventTypeConsumerResponseArray);

    private async Task<T?> GetAsync<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        var response = await _http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo);
    }
}
