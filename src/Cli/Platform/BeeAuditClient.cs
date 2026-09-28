using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;

namespace SymplrCli.Platform;

public record AuditRecordResponse(
    string?        TenantId,
    string?        ProductName,
    string?        Environment,
    string?        EventName,
    string?        ConsumerId,
    string?        CorrelationId,
    long           Counter,
    string?        Source,
    string?        Status,
    string?        ErrorCode,
    string?        ErrorMessage,
    DateTimeOffset Created);

public record AuditSearchResponse(AuditRecordResponse[]? Events);

public class BeeAuditClient
{
    private readonly HttpClient _http;

    private BeeAuditClient(HttpClient http) => _http = http;

    public static BeeAuditClient Create(string baseUrl, string token)
    {
        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new BeeAuditClient(http);
    }

    public Task<AuditSearchResponse?> GetAuditsAsync(
        string? tenantId      = null,
        string? productName   = null,
        string? environment   = null,
        string? consumerId    = null,
        string? correlationId = null,
        CancellationToken ct  = default)
    {
        var qs = BuildQuery(tenantId, productName, environment, consumerId, correlationId);
        return _http.GetFromJsonAsync($"v1/Audit{qs}", SymplrJsonContext.Default.AuditSearchResponse, ct);
    }

    private static string BuildQuery(
        string? tenantId,
        string? productName,
        string? environment,
        string? consumerId,
        string? correlationId)
    {
        var q = HttpUtility.ParseQueryString(string.Empty);
        if (tenantId      is not null) q["TenantId"]      = tenantId;
        if (productName   is not null) q["ProductName"]   = productName;
        if (environment   is not null) q["Environment"]   = environment;
        if (consumerId    is not null) q["ConsumerId"]    = consumerId;
        if (correlationId is not null) q["CorrelationId"] = correlationId;
        var s = q.ToString();
        return string.IsNullOrEmpty(s) ? "" : $"?{s}";
    }
}
